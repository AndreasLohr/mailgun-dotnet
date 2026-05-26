using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mailgun.Exceptions;
using Mailgun.Http;
using Mailgun.Models.Domains;
using Mailgun.Models.MailingLists;
using Mailgun.Models.Messages;
using Mailgun.Models.Templates;
using Mailgun.Serialization;
using Mailgun.Services;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests;

/// <summary>
/// Round-9 Stryker mutation triage findings: tests added to kill survivors that flagged real
/// coverage gaps rather than equivalent mutations. Each test names the survivor it targets so a
/// future Stryker run shows the score lift directly. No production-code bugs were found during
/// triage — every test here is a coverage addition, not a regression fix.
/// </summary>
public class MutationTriageRegressionTests
{
    // ───── AsyncPageable: pagination terminator (covers `||` vs `&&` mutation at line 41) ─────

    [Fact]
    public async Task AsyncPageable_breaks_on_empty_items_page_even_when_NextUrl_is_present()
    {
        // Pin the OR semantics in `if (!page.HasMore || string.IsNullOrEmpty(page.NextUrl))`.
        // HasMore is defined as `Items.Count > 0 && !empty(NextUrl)`, so the unique-to-`||` case
        // is an EMPTY items page with a non-null NextUrl — HasMore is false (items empty), and
        // the OR-terminator must still break.  The `&&` mutation would require BOTH `!HasMore`
        // and `empty(NextUrl)` true, so it'd happily follow the NextUrl into infinite recursion
        // through any empty-but-paginating response.  Only one queued response — if the SDK
        // follows the URL, MockHttpMessageHandler throws InvalidOperationException.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "items": [],
              "paging": {"next": "https://api.mailgun.test/v3/mg/bounces?skip=99"},
              "total_count": 0
            }
            """);

        var seen = new List<string>();
        await foreach (var b in client.Suppressions.Bounces.ListAllAsync("mg"))
            seen.Add(b.Address);

        Assert.Empty(seen);
        Assert.Single(handler.Requests);
    }

    // ───── MailingLists.BulkAddMembers: 1000-member boundary (MailingListsService.cs:147) ─────

    [Fact]
    public async Task BulkAddMembers_accepts_exactly_1000_members()
    {
        // Stryker mutated `> 1000` to `>= 1000`. The boundary case `Count == 1000` must succeed.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"Mailing list has been updated\"}");

        var members = Enumerable.Range(0, 1000)
            .Select(i => new AddMemberRequest { Address = $"user{i}@example.com" })
            .ToList();

        await client.MailingLists.BulkAddMembersAsync("list@mg.example.com", members);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task BulkAddMembers_rejects_1001_members()
    {
        var (client, _) = TestMailgunClient.Create();

        var members = Enumerable.Range(0, 1001)
            .Select(i => new AddMemberRequest { Address = $"user{i}@example.com" })
            .ToList();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.MailingLists.BulkAddMembersAsync("list@mg.example.com", members));
        Assert.Contains("1000", ex.Message, StringComparison.Ordinal);
    }

    // ───── BuildException: null-coalescing priority for error message (MailgunHttpClient.cs:513) ─────

    [Fact]
    public async Task ErrorResponse_with_all_three_message_fields_picks_Message_first()
    {
        // The SDK's BuildException coalesces `parsed.Message ?? parsed.MessageCapital ?? parsed.Error`.
        // Stryker mutates the chain order; tests using error JSON with only ONE of the three
        // populated would let any ordering pass. Pin the priority by giving all three distinct
        // values and asserting on which one wins.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, """
            {"message":"lowercase wins","Message":"uppercase ignored","error":"error-field ignored"}
            """);

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Equal("lowercase wins", ex.ErrorMessage);
    }

    [Fact]
    public async Task ErrorResponse_with_only_MessageCapital_falls_through_to_it()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, """
            {"Message":"uppercase only"}
            """);

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Equal("uppercase only", ex.ErrorMessage);
    }

    [Fact]
    public async Task ErrorResponse_with_only_error_field_falls_through_to_it()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, """
            {"error":"legacy-error-field-only"}
            """);

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Equal("legacy-error-field-only", ex.ErrorMessage);
    }

    // ───── AppendDetails / FlattenJson: covered code paths (MailgunHttpClient.cs:540-577) ─────

    [Fact]
    public async Task ErrorResponse_details_array_of_raw_strings_appears_in_Details()
    {
        // AppendDetails has a `case string s when !string.IsNullOrWhiteSpace(s):` arm that only
        // triggers when the JSON deserialiser hands us a raw string in the details array.
        // System.Text.Json deserialises details as object?; if Mailgun emits string elements,
        // they'd reach this arm. Without a test, that branch is NoCoverage.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, """
            {"message":"failed","details":["first detail line","second detail line"]}
            """);

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        // Mailgun's details surface through ex.Details — confirm both lines made it through the
        // string-arm of AppendDetails / FlattenJson.
        Assert.Contains(ex.Details, d => d.Contains("first detail line", StringComparison.Ordinal));
        Assert.Contains(ex.Details, d => d.Contains("second detail line", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ErrorResponse_details_with_nested_non_string_recurses_through_FlattenJson()
    {
        // The else-branch of FlattenJson recurses on non-string nested values inside a JSON object.
        // Without a fixture that nests an array/object inside the details object, the recursion
        // line is NoCoverage.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, """
            {"message":"failed","details":{"inner":{"deeply":"nested"}}}
            """);

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Contains(ex.Details, d => d.Contains("nested", StringComparison.Ordinal));
    }

    // ───── SendAlertsService: Get / Update / Delete CRUD (lines 90-116 NoCoverage) ─────

    [Fact]
    public async Task SendAlerts_GetAsync_targets_v1_thresholds_alerts_send_by_name()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"name\":\"bounce-spike\",\"metric\":\"failed_count\",\"comparator\":\"gt\",\"limit\":\"100\",\"dimension\":\"domain\"}");

        var rule = await client.SendAlerts.GetAsync("bounce-spike");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v1/thresholds/alerts/send/bounce-spike", req.Uri.AbsolutePath);
        Assert.Equal("bounce-spike", rule.Name);
    }

    [Fact]
    public async Task SendAlerts_UpdateAsync_PUTs_json_to_named_resource()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"name\":\"r1\",\"metric\":\"failed_count\",\"comparator\":\"gt\",\"limit\":\"50\",\"dimension\":\"domain\"}");

        await client.SendAlerts.UpdateAsync("r1", new SendAlertRule
        {
            Name = "r1", Metric = "failed_count", Comparator = "gt",
            Limit = "50", Dimension = "domain",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.Equal("application/json", req.ContentType);
        Assert.EndsWith("/v1/thresholds/alerts/send/r1", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task SendAlerts_DeleteAsync_targets_named_resource()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.NoContent, string.Empty);

        await client.SendAlerts.DeleteAsync("r1");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v1/thresholds/alerts/send/r1", req.Uri.AbsolutePath);
    }

    // ───── LimitsService: Get / Update / Delete CRUD (similar NoCoverage) ─────

    [Fact]
    public async Task Limits_GetAsync_targets_v1_thresholds_limits_by_name()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"name\":\"daily-cap\",\"metric\":\"sent_count\",\"comparator\":\"gt\",\"limit\":\"10000\",\"dimension\":\"domain\"}");

        var rule = await client.Limits.GetAsync("daily-cap");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v1/thresholds/limits/daily-cap", req.Uri.AbsolutePath);
        Assert.Equal("daily-cap", rule.Name);
    }

    [Fact]
    public async Task Limits_UpdateAsync_PUTs_json_to_named_resource()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"name\":\"r1\",\"metric\":\"sent_count\",\"comparator\":\"gt\",\"limit\":\"5000\",\"dimension\":\"domain\"}");

        await client.Limits.UpdateAsync("r1", new LimitRule
        {
            Name = "r1", Metric = "sent_count", Comparator = "gt",
            Limit = "5000", Dimension = "domain",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v1/thresholds/limits/r1", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task Limits_DeleteAsync_targets_named_resource()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.NoContent, string.Empty);

        await client.Limits.DeleteAsync("r1");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v1/thresholds/limits/r1", req.Uri.AbsolutePath);
    }

    // ───── IpPoolsService: edge-case guards (lines 129, 132, 193 NoCoverage) ─────

    [Fact]
    public async Task IpPools_CreateAsync_rejects_blank_Name()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.IpPools.CreateAsync(new CreateIpPoolRequest { Name = "", Description = "d" }));
    }

    [Fact]
    public async Task IpPools_CreateAsync_rejects_blank_Description()
    {
        // Mailgun's POST /v3/ip_pools docs both `name` AND `description` as required. The SDK
        // mirrors that with two independent ArgumentException guards. Previous tests only covered
        // the Name guard; this pins the Description guard so a future refactor can't silently drop
        // it.
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.IpPools.CreateAsync(new CreateIpPoolRequest { Name = "pool", Description = "" }));
    }

    [Fact]
    public async Task IpPools_AddIpsAsync_rejects_empty_list()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.IpPools.AddIpsAsync("pool", Array.Empty<string>()));
    }

    // ───── MailgunHttpClient: malformed pagination URL throws (line 238-239 NoCoverage) ─────
    // (Note: the "blank base URL" ctor guard at line 40 is genuinely unreachable defensive code —
    //  MailgunRegion is a non-nullable enum and ResolveBaseUrl() always falls through to
    //  Us/Eu URLs. Its NoCoverage mutation is equivalent; no test added.)

    [Fact]
    public async Task Pagination_link_that_is_not_a_valid_absolute_URL_throws_serialisation_exception()
    {
        // ValidatePaginationUrl rejects non-https + off-origin links (covered by existing tests)
        // and also rejects links that simply aren't valid absolute URIs. This pins that last arm.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "items": [{"address":"a@example.com"}],
              "paging": {"next":"not-a-url"},
              "total_count": 2
            }
            """);

        await Assert.ThrowsAsync<MailgunSerializationException>(async () =>
        {
            await foreach (var _ in client.Suppressions.Bounces.ListAllAsync("mg"))
            {
            }
        });
    }

    // ───── Tests that intentionally exercise the OnResponse-throws activity-event tags
    //       (MailgunHttpClient.cs:374-378 NoCoverage) — kept minimal so the inner exception
    //       branch's string mutations get reached even though we don't assert event content. ─────

    [Fact]
    public async Task OnResponse_callback_that_throws_does_not_break_the_request_and_reaches_activity_branch()
    {
        var handler = new TestHelpers.MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
            OnResponse = _ => throw new InvalidOperationException("callback bomb"),
        });

        // Request still succeeds — the SDK swallows the callback's exception, traces a warning,
        // and stamps an activity event with exception.type/message. We don't assert on the
        // activity here (an ActivityListener would; the existing OnResponseCallbackTests cover
        // the swallow contract); this test just ensures the throwing-callback branch is reached
        // so Stryker's mutations on its string literals are covered.
        var page = await client.Routes.ListAsync();
        Assert.Empty(page.Items);
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════
    //   Serialisation converter coverage — round 9 deferred batch
    // ═════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Helper that round-trips a single value through a JsonConverter<T>.</summary>
    private static T? RoundTrip<T>(JsonConverter<T> converter, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        reader.Read();
        return converter.Read(ref reader, typeof(T), new JsonSerializerOptions());
    }

    private static string WriteValue<T>(JsonConverter<T> converter, T value)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
            converter.Write(writer, value, new JsonSerializerOptions());
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    // ───── NullableIsoDateTimeConverter ─────

    [Fact]
    public void NullableIsoDateTime_reads_JSON_null_as_null()
    {
        var c = new NullableIsoDateTimeConverter();
        Assert.Null(RoundTrip(c, "null"));
    }

    [Fact]
    public void NullableIsoDateTime_reads_empty_string_as_null()
    {
        // Mailgun's /v1/keys endpoint emits "" for keys without a recorded creation time. Default
        // System.Text.Json binding rejects that for DateTime?; this converter must coerce to null.
        var c = new NullableIsoDateTimeConverter();
        Assert.Null(RoundTrip(c, "\"\""));
    }

    [Fact]
    public void NullableIsoDateTime_reads_valid_ISO_string()
    {
        var c = new NullableIsoDateTimeConverter();
        var parsed = RoundTrip(c, "\"2026-05-26T10:00:00\"");
        Assert.NotNull(parsed);
        Assert.Equal(new DateTime(2026, 5, 26, 10, 0, 0, DateTimeKind.Utc), parsed!.Value);
    }

    [Fact]
    public void NullableIsoDateTime_throws_on_unparseable_string()
    {
        var c = new NullableIsoDateTimeConverter();
        var ex = Assert.Throws<JsonException>(() => RoundTrip(c, "\"definitely-not-a-date\""));
        Assert.Contains("Could not parse", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullableIsoDateTime_throws_on_non_string_non_null_token()
    {
        var c = new NullableIsoDateTimeConverter();
        var ex = Assert.Throws<JsonException>(() => RoundTrip(c, "12345"));
        Assert.Contains("Expected string or null", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullableIsoDateTime_writes_null_as_JSON_null()
    {
        var c = new NullableIsoDateTimeConverter();
        Assert.Equal("null", WriteValue<DateTime?>(c, null));
    }

    [Fact]
    public void NullableIsoDateTime_writes_value_as_ISO_string()
    {
        var c = new NullableIsoDateTimeConverter();
        // Universal-time normalisation: a non-UTC kind gets converted to UTC before formatting.
        var input = new DateTime(2026, 5, 26, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal("\"2026-05-26T10:00:00\"", WriteValue<DateTime?>(c, input));
    }

    // ───── PolymorphicDomainDisabledConverter ─────

    [Fact]
    public void PolymorphicDomainDisabled_reads_JSON_null_as_null()
    {
        var c = new PolymorphicDomainDisabledConverter();
        Assert.Null(RoundTrip(c, "null"));
    }

    [Fact]
    public void PolymorphicDomainDisabled_reads_true_as_null()
    {
        // Legacy boolean form: `"disabled": true` → no envelope; caller reads Domain.IsDisabled.
        var c = new PolymorphicDomainDisabledConverter();
        Assert.Null(RoundTrip(c, "true"));
    }

    [Fact]
    public void PolymorphicDomainDisabled_reads_false_as_null()
    {
        var c = new PolymorphicDomainDisabledConverter();
        Assert.Null(RoundTrip(c, "false"));
    }

    [Fact]
    public void PolymorphicDomainDisabled_reads_object_form_as_DomainDisabledInfo()
    {
        var c = new PolymorphicDomainDisabledConverter();
        var info = RoundTrip(c, """{"permanently":true,"reason":"bounce-rate","note":"see ticket"}""");
        Assert.NotNull(info);
        Assert.True(info!.Permanently);
        Assert.Equal("bounce-rate", info.Reason);
        Assert.Equal("see ticket", info.Note);
    }

    [Fact]
    public void PolymorphicDomainDisabled_throws_on_unexpected_token()
    {
        var c = new PolymorphicDomainDisabledConverter();
        var ex = Assert.Throws<JsonException>(() => RoundTrip(c, "[1,2,3]"));
        Assert.Contains("Unexpected token", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PolymorphicDomainDisabled_writes_null_as_JSON_null()
    {
        var c = new PolymorphicDomainDisabledConverter();
        Assert.Equal("null", WriteValue<DomainDisabledInfo?>(c, null));
    }

    [Fact]
    public void PolymorphicDomainDisabled_writes_object_form()
    {
        var c = new PolymorphicDomainDisabledConverter();
        var json = WriteValue<DomainDisabledInfo?>(c, new DomainDisabledInfo { Permanently = false, Reason = "spam", Note = null });
        Assert.Contains("\"permanently\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"reason\":\"spam\"", json, StringComparison.Ordinal);
    }

    // ───── UnixTimestampDateTimeOffsetConverter ─────

    [Fact]
    public void UnixTimestamp_reads_string_form_when_Mailgun_emits_as_string()
    {
        // Mailgun's events surface usually emits Unix seconds as a JSON number, but some envelopes
        // (e.g. legacy logs) emit them as JSON-string. The converter accepts both forms; without
        // a test the string branch is NoCoverage.
        var c = new UnixTimestampDateTimeOffsetConverter();
        var parsed = RoundTrip(c, "\"1700000000.0\"");
        Assert.Equal(new DateTimeOffset(2023, 11, 14, 22, 13, 20, TimeSpan.Zero), parsed);
    }

    [Fact]
    public void UnixTimestamp_throws_on_empty_string()
    {
        var c = new UnixTimestampDateTimeOffsetConverter();
        var ex = Assert.Throws<JsonException>(() => RoundTrip(c, "\"\""));
        Assert.Contains("Could not parse Unix timestamp", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnixTimestamp_throws_on_unparseable_string()
    {
        var c = new UnixTimestampDateTimeOffsetConverter();
        Assert.Throws<JsonException>(() => RoundTrip(c, "\"not-a-number\""));
    }

    [Fact]
    public void UnixTimestamp_throws_on_non_number_non_string_token()
    {
        var c = new UnixTimestampDateTimeOffsetConverter();
        Assert.Throws<JsonException>(() => RoundTrip(c, "{}"));
    }

    [Fact]
    public void UnixTimestamp_throws_on_overflow_value_with_descriptive_message()
    {
        // The "outside supported range" branch (line 44 of the converter) fires when the seconds
        // value overflows DateTimeOffset's range (~year 0001..9999). Use a value far beyond that
        // so the conversion to long ms wraps to long.MinValue.
        var c = new UnixTimestampDateTimeOffsetConverter();
        var ex = Assert.Throws<JsonException>(() => RoundTrip(c, "1e18"));
        Assert.Contains("outside the supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════
    //   RateLimitHandler — Retry-After header parsing (Delta + Date forms)
    // ═════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RateLimitHandler_honours_Retry_After_Delta_seconds_form()
    {
        // The handler's ComputeDelay prefers X-RateLimit-Reset; the next-best signal is a standard
        // Retry-After header in delta-seconds form (e.g. `Retry-After: 1`). Existing tests cover
        // X-RateLimit-Reset only — the Delta path was NoCoverage at round 8.
        var primary = new MockHttpMessageHandler();
        primary.EnqueueResponse(HttpStatusCode.TooManyRequests, "{\"message\":\"slow\"}",
            headers: new Dictionary<string, string> { { "Retry-After", "1" } });
        primary.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        var rateLimit = (DelegatingHandler)Activator.CreateInstance(
            typeof(MailgunClient).Assembly.GetType("Mailgun.Http.RateLimitHandler")!,
            args: new object[] { 1 })!;
        rateLimit.InnerHandler = primary;
        using var http = new HttpClient(rateLimit) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
        });

        // The retry honours the 1-second Retry-After; both responses are consumed.
        var page = await client.Routes.ListAsync();
        Assert.Empty(page.Items);
        Assert.Equal(2, primary.Requests.Count);
    }

    [Fact]
    public async Task RateLimitHandler_honours_Retry_After_HTTP_date_form()
    {
        // `Retry-After: <RFC 1123 date>` — second-best signal after Delta. Set it to a date one
        // second in the future and verify the retry actually happens.
        var primary = new MockHttpMessageHandler();
        var futureRfc1123 = DateTimeOffset.UtcNow.AddSeconds(1).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        primary.EnqueueResponse(HttpStatusCode.TooManyRequests, "{\"message\":\"slow\"}",
            headers: new Dictionary<string, string> { { "Retry-After", futureRfc1123 } });
        primary.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        var rateLimit = (DelegatingHandler)Activator.CreateInstance(
            typeof(MailgunClient).Assembly.GetType("Mailgun.Http.RateLimitHandler")!,
            args: new object[] { 1 })!;
        rateLimit.InnerHandler = primary;
        using var http = new HttpClient(rateLimit) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
        });

        var page = await client.Routes.ListAsync();
        Assert.Empty(page.Items);
        Assert.Equal(2, primary.Requests.Count);
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════
    //   AppendDetails / FlattenJson recursion edge cases (round 9 deferred batch)
    // ═════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ErrorResponse_details_object_with_string_values_emits_key_value_lines()
    {
        // FlattenJson's object-with-string-value arm emits "key: value" lines. Without a fixture
        // that nests strings inside the details object, this branch can survive mutation.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, """
            {"message":"validation","details":{"field":"address","reason":"invalid"}}
            """);

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        // Each "key: value" pair surfaces in Details.
        Assert.Contains(ex.Details, d => d.Contains("field", StringComparison.Ordinal) && d.Contains("address", StringComparison.Ordinal));
        Assert.Contains(ex.Details, d => d.Contains("reason", StringComparison.Ordinal) && d.Contains("invalid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ErrorResponse_details_array_of_objects_recurses_through_each()
    {
        // The array-of-objects shape exercises both the array-iteration arm AND the object-arm
        // of FlattenJson per element. A flat details:[strings] doesn't reach the per-element
        // object recursion; only an array of objects does.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, """
            {"message":"bulk","details":[{"row":1,"err":"missing"},{"row":2,"err":"invalid"}]}
            """);

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Contains(ex.Details, d => d.Contains("missing", StringComparison.Ordinal));
        Assert.Contains(ex.Details, d => d.Contains("invalid", StringComparison.Ordinal));
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════
    //   Final remaining coverage gaps (round 9 cleanup batch)
    // ═════════════════════════════════════════════════════════════════════════════════════════

    // ───── TemplatesService.CreateVersionAsync — Headers null vs populated (line 129) ─────

    [Fact]
    public async Task Templates_CreateVersion_with_Headers_serialises_them_as_form_field()
    {
        // The `if (request.Headers is not null)` arm controls whether the `headers` form field is
        // added. Previous tests covered the unset-Headers path; pin the populated-Headers path so
        // the boolean-flip mutation gets killed.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"template\":{\"name\":\"t1\",\"version\":{\"tag\":\"v1\",\"template\":\"hi\"}}}");

        await client.Templates.CreateVersionAsync("t1", new CreateTemplateVersionRequest
        {
            Tag = "v1",
            Template = "hello {{name}}",
            Headers = new Dictionary<string, string>
            {
                ["X-Campaign"] = "spring-2026",
                ["X-Reply-To"] = "support@example.com",
            },
        });

        var req = Assert.Single(handler.Requests);
        Assert.Contains("headers=", req.Body!, StringComparison.Ordinal);
        // The headers value is the JSON-serialised dictionary, URL-encoded into the form body.
        // Don't pin exact encoding; assert both keys reach the wire.
        Assert.Contains("X-Campaign", req.Body!, StringComparison.Ordinal);
        Assert.Contains("X-Reply-To", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Templates_CreateVersion_without_Headers_omits_form_field()
    {
        // The complement of the test above: when Headers is null (the default), no `headers=`
        // form field should appear. The boolean mutation would invert this.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"template\":{\"name\":\"t1\",\"version\":{\"tag\":\"v1\",\"template\":\"hi\"}}}");

        await client.Templates.CreateVersionAsync("t1", new CreateTemplateVersionRequest
        {
            Tag = "v1",
            Template = "hello",
            // Headers intentionally not set — the conditional must skip the form field.
        });

        var req = Assert.Single(handler.Requests);
        Assert.DoesNotContain("headers=", req.Body!, StringComparison.Ordinal);
    }

    // ───── IpPoolsService.UpdateAsync — UnlinkDomains foreach (line 152) ─────

    [Fact]
    public async Task IpPools_UpdateAsync_with_UnlinkDomains_emits_unlink_domain_form_fields()
    {
        // The foreach over `request.UnlinkDomains` adding `unlink_domain` multipart fields was
        // NoCoverage at round 8 — no test populated that list. Pin it now.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        var request = new UpdateIpPoolRequest { Name = "pool1" };
        request.UnlinkDomains.Add("a.example.com");
        request.UnlinkDomains.Add("b.example.com");

        await client.IpPools.UpdateAsync("p1", request);

        var req = Assert.Single(handler.Requests);
        // The unlink_domain field appears once per domain in the multipart body.
        Assert.Contains("unlink_domain", req.Body!, StringComparison.Ordinal);
        Assert.Contains("a.example.com", req.Body!, StringComparison.Ordinal);
        Assert.Contains("b.example.com", req.Body!, StringComparison.Ordinal);
    }

    // ───── BuildJsonContent — JsonException wrapping (line 475) ─────

    /// <summary>Self-referencing test fixture — System.Text.Json's default config throws on cycles.</summary>
    private sealed class CyclicForTriage
    {
        public CyclicForTriage? Self { get; set; }
        public string Name { get; set; } = "x";
    }

    /// <summary>Counts calls and returns a queued sequence of status codes — for retry-loop tests.</summary>
    private sealed class CountingTriageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses;
        public int CallCount { get; private set; }

        public CountingTriageHandler(params HttpStatusCode[] statuses) =>
            _statuses = new Queue<HttpStatusCode>(statuses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var status = _statuses.Count > 0 ? _statuses.Dequeue() : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("{\"items\":[],\"total_count\":0}", Encoding.UTF8, "application/json"),
            });
        }
    }

    [Fact]
    public async Task BuildJsonContent_wraps_serializer_failure_into_MailgunSerializationException()
    {
        // BuildJsonContent's try/catch normalises a JsonException from JsonSerializer.Serialize into
        // a MailgunSerializationException (so SDK consumers can catch one exception type for
        // serialisation failures). Without a test that actually triggers the catch, that branch is
        // NoCoverage; the new `var json = string.Empty;` defensive initialiser added during the
        // Stryker safe-mode fix masked any compile-error tells. Use a cyclic object — the default
        // System.Text.Json options reject these with a JsonException.
        using var http = new MailgunHttpClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = new HttpClient(new MockHttpMessageHandler()) { BaseAddress = new Uri("https://api.mailgun.test/") },
        });

        var cyclic = new CyclicForTriage();
        cyclic.Self = cyclic;

        var ex = await Assert.ThrowsAsync<MailgunSerializationException>(() =>
            http.PostJsonBodyAsync<object>("v1/foo", cyclic, CancellationToken.None));
        Assert.IsType<JsonException>(ex.InnerException);
    }

    // ───── RateLimitHandler.Clamp — bound check at MaxBackoff (line 136) ─────

    [Fact]
    public void RateLimitHandler_Clamp_caps_TimeSpans_above_MaxBackoff_to_60_seconds()
    {
        // Clamp is `t > MaxBackoff ? MaxBackoff : t` with MaxBackoff = 60s. Pinning via reflection
        // — there's no public path to drive Clamp without actually waiting 60 seconds in a test.
        // The boundary mutations `> → <` and `false → true` differ at 30s; the `> → >=` mutation
        // is provably equivalent at the boundary (both sides return MaxBackoff). One reflection
        // call kills the three mutators that aren't equivalent.
        var rateLimitType = typeof(MailgunClient).Assembly.GetType("Mailgun.Http.RateLimitHandler")!;
        var clamp = rateLimitType.GetMethod("Clamp", BindingFlags.NonPublic | BindingFlags.Static)!;

        var below = (TimeSpan)clamp.Invoke(null, new object[] { TimeSpan.FromSeconds(30) })!;
        var boundary = (TimeSpan)clamp.Invoke(null, new object[] { TimeSpan.FromSeconds(60) })!;
        var above = (TimeSpan)clamp.Invoke(null, new object[] { TimeSpan.FromSeconds(120) })!;

        // Below the cap: pass-through (kills `false-conditional` mutation and `< → >` flip).
        Assert.Equal(TimeSpan.FromSeconds(30), below);
        // At the cap: still pass-through (60 > 60 is false, returns t).
        Assert.Equal(TimeSpan.FromSeconds(60), boundary);
        // Above the cap: clamped (kills `true-conditional` flip).
        Assert.Equal(TimeSpan.FromSeconds(60), above);
    }

    // ───── RateLimitHandler.IsActionEndpoint — single-segment path equal to the verb ─────

    [Fact]
    public async Task RateLimitHandler_IsActionEndpoint_treats_path_ending_in_bare_rotate_as_action()
    {
        // Three Stryker mutations on lines 88, 98, 102 are killed by a single test: a URL whose
        // last segment is EXACTLY a known action verb ("rotate"), with no hyphen suffix or dot.
        //   * Line 88 mutation `lastSlash < 0 → <= 0`: for `/rotate`, lastSlash=0 → original
        //     proceeds (action detected); mutation early-returns false (action NOT detected).
        //   * Line 98 mutation `Contains('.') → true`: makes StartsWithActionVerb always return
        //     false → action NOT detected.
        //   * Line 102 mutation `==` → `!=`: at length-equal-verb-length, original takes the
        //     `seg.Length == verb.Length` true branch and short-circuits; mutation falls through
        //     to `seg[verb.Length] == '-'` — but `seg.Length == verb.Length` means that index is
        //     out of bounds, so the mutation would throw IndexOutOfRange (which doesn't bubble
        //     through cleanly, so the action heuristic effectively breaks).
        // Original: PUT /rotate on 5xx → action → NO retry → 1 call total.
        // Any of the mutations: NOT action → 5xx is retried → > 1 call.
        var counter = new CountingTriageHandler(HttpStatusCode.InternalServerError, HttpStatusCode.OK);
        var rateLimit = (DelegatingHandler)Activator.CreateInstance(
            typeof(MailgunClient).Assembly.GetType("Mailgun.Http.RateLimitHandler")!,
            args: new object[] { 3 })!;
        rateLimit.InnerHandler = counter;
        using var http = new HttpClient(rateLimit);
        using var req = new HttpRequestMessage(HttpMethod.Put,
            new Uri("https://api.mailgun.test/rotate"));

        using var resp = await http.SendAsync(req);

        // One call only — the bare-verb last segment trips the action heuristic.
        Assert.Equal(1, counter.CallCount);
    }

    // ═════════════════════════════════════════════════════════════════════════════════════════
    //   Final NoCoverage cleanup — kill the remaining 14 mutants reported by Stryker
    // ═════════════════════════════════════════════════════════════════════════════════════════

    // ───── MailgunHttpClient L374-378 — OnResponse callback exception activity event tags ─────

    [Fact]
    public async Task OnResponse_callback_exception_stamps_activity_event_with_exception_type_and_message_tags()
    {
        // The 4 mutants on lines 374-378 of MailgunHttpClient.cs target the activity event the
        // SDK adds when an OnResponse callback throws: event name `mailgun.on_response.exception`,
        // tags collection literal, and the `exception.type` + `exception.message` tag keys.
        // The existing OnResponseCallbackTests cover the swallow-the-exception contract but don't
        // assert on activity event tag contents, leaving those mutants NoCoverage.
        const string UniqueErrorMessage = "callback-bomb-for-triage-test-a3f9e2";

        var capturedActivities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == MailgunActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = a => capturedActivities.Add(a),
        };
        ActivitySource.AddActivityListener(listener);

        var mockHandler = new MockHttpMessageHandler();
        mockHandler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");
        using var http = new HttpClient(mockHandler) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
            OnResponse = _ => throw new InvalidOperationException(UniqueErrorMessage),
        });

        await client.Routes.ListAsync();

        // Find the activity that recorded the callback exception. The unique message lets us pick
        // it out of any other activities running in parallel under the same listener.
        var matching = capturedActivities
            .Where(a => a.Events.Any(e =>
                e.Name == "mailgun.on_response.exception"
                && e.Tags.Any(t => t.Key == "exception.message" && t.Value as string == UniqueErrorMessage)))
            .ToList();

        var activity = Assert.Single(matching);
        var evt = activity.Events.Single(e => e.Name == "mailgun.on_response.exception");
        var tags = evt.Tags.ToDictionary(t => t.Key, t => t.Value);
        Assert.Equal("System.InvalidOperationException", tags["exception.type"]);
        Assert.Equal(UniqueErrorMessage, tags["exception.message"]);
    }

    // ───── MailgunResponseMetadata L74 — TryParseUnixMillis catch ArgumentOutOfRangeException ─────

    [Fact]
    public async Task X_RateLimit_Reset_with_out_of_range_value_yields_null_reset_not_thrown_exception()
    {
        // TryParseUnixMillis wraps FromUnixTimeSeconds / FromUnixTimeMilliseconds in try/catch
        // because both throw ArgumentOutOfRangeException for values outside [year 1, year 9999].
        // A crafted X-RateLimit-Reset header value past year 9999 would otherwise crash the
        // metadata-parse path. The catch block at line 74 was NoCoverage because no existing test
        // gave it an out-of-range value.
        MailgunResponseMetadata? captured = null;
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}",
            headers: new Dictionary<string, string>
            {
                // 17 digits — long-parseable but far past the FromUnixTimeMilliseconds upper bound.
                { "X-RateLimit-Reset", "99999999999999999" },
            });
        using var http = new HttpClient(mockHandler) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
            OnResponse = m => captured = m,
        });

        await client.Routes.ListAsync();

        // The header was present and long-parseable; the catch caught the AOORE; Reset is null.
        Assert.NotNull(captured);
        Assert.Null(captured!.RateLimit?.Reset);
    }

    // ───── AccountServices L90 — Subaccounts.GetAsync URL ─────

    [Fact]
    public async Task Subaccounts_GetAsync_targets_v5_accounts_subaccounts_with_id_segment()
    {
        // The 2 mutants on line 90 target the path string and the route template literal for
        // Subaccounts.GetAsync. No existing test covered this method, leaving both NoCoverage.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"acct_abc\",\"name\":\"team-a\"}");

        var sub = await client.Subaccounts.GetAsync("acct_abc");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v5/accounts/subaccounts/acct_abc", req.Uri.AbsolutePath);
        Assert.Equal("acct_abc", sub.Id);
    }

    // ───── SendAlertRule L182, L185 — DTO Dimension / Comparator default string.Empty ─────

    [Fact]
    public void ThresholdFilter_Dimension_default_initialiser_is_string_Empty()
    {
        // Pins the `= string.Empty` property initialiser on ThresholdFilter.Dimension. Stryker
        // mutates this to a non-empty literal; the direct property read is the only way to
        // observe the difference, since ThresholdFilter is a DTO with no validation hook on
        // intake. (The SendAlertRule.Create test below doesn't catch this — that class is a
        // different type.)
        Assert.Equal(string.Empty, new ThresholdFilter().Dimension);
    }

    [Fact]
    public void ThresholdFilter_Comparator_default_initialiser_is_string_Empty()
    {
        Assert.Equal(string.Empty, new ThresholdFilter().Comparator);
    }

    [Fact]
    public async Task SendAlerts_Create_with_only_Dimension_unset_throws_on_default_empty_value()
    {
        // The Dimension property's default initialiser (`= string.Empty;`) is what the SDK's
        // ValidateRequired check throws against when a caller doesn't set Dimension explicitly.
        // The mutation replaces `string.Empty` with `"Stryker was here!"` — without this test
        // the validation would silently pass for unset Dimension. Set all OTHER required fields
        // so the validation order reaches the Dimension check.
        var (client, _) = TestMailgunClient.Create();

        var rule = new SendAlertRule
        {
            Name = "n",
            Metric = "m",
            Comparator = "c",
            Limit = "1",
            // Dimension intentionally unset — relies on the default.
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.SendAlerts.CreateAsync(rule));
        Assert.Contains("dimension", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAlerts_Create_with_only_Comparator_unset_throws_on_default_empty_value()
    {
        var (client, _) = TestMailgunClient.Create();

        var rule = new SendAlertRule
        {
            Name = "n",
            Metric = "m",
            Limit = "1",
            Dimension = "d",
            // Comparator intentionally unset.
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.SendAlerts.CreateAsync(rule));
        Assert.Contains("comparator", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ───── TemplatesService L113 / L152 — InvalidOperationException on missing Version ─────

    [Fact]
    public async Task Templates_GetVersionAsync_throws_descriptive_error_when_response_has_no_version_object()
    {
        // The `?? throw new InvalidOperationException(...)` on Template.Version uses a specific
        // message — the string-mutation on line 113 would silently clear it. Verify the message
        // is what we promise.
        var (client, handler) = TestMailgunClient.Create();
        // Mailgun returns Template envelope but with version=null — exercises the ?? throw path.
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"template\":{\"name\":\"t1\",\"version\":null}}");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.Templates.GetVersionAsync("t1", "v1"));
        Assert.Contains("Mailgun did not return a version object", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Templates_UpdateVersionAsync_throws_descriptive_error_when_response_has_no_version_object()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"template\":{\"name\":\"t1\",\"version\":null}}");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.Templates.UpdateVersionAsync("t1", "v1", new UpdateTemplateVersionRequest { Template = "body" }));
        Assert.Contains("Mailgun did not return a version object", ex.Message, StringComparison.Ordinal);
    }

    // ───── WebhooksService L105 — UpdateAccountWebhookAsync empty event-types throws with message ─────

    [Fact]
    public async Task UpdateAccountWebhook_with_empty_event_types_throws_with_descriptive_message()
    {
        // The string-mutation on line 105 would silently empty the error message. The existing
        // CreateAccountWebhook_rejects_empty_event_types test asserts the exception type but not
        // the message text, and there's no Update-side coverage at all. Pin both.
        var (client, _) = TestMailgunClient.Create();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Webhooks.UpdateAccountWebhookAsync("wh-1", "https://x", Array.Empty<string>()));
        Assert.Contains("event type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
