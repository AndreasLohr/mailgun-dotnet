using System.Diagnostics;
using System.Net;
using Mailgun;
using Mailgun.AspNetCore;
using Mailgun.Exceptions;
using Mailgun.Http;
using Mailgun.Tests.TestHelpers;
using Mailgun.Webhooks;

namespace Mailgun.Tests;

/// <summary>
/// Regression tests for the security review (June 2026). One region per finding:
///   #1 PII redaction of the OpenTelemetry url.full span tag
///   #2 HTTPS enforcement on BaseUrl (loopback + opt-in exempt)
///   #3 CR/LF / control-character rejection on the subaccount id (X-Mailgun-On-Behalf-Of)
///   #4 response-body size cap
///   #6a webhook anti-replay cache is on by default
/// (#5 AllowAutoRedirect=false is a one-line owned-handler config not exercisable through the mock
///  transport; it's covered by code review.)
/// </summary>
public class SecurityHardeningTests
{
    // ── #1 PII never reaches url.full ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Span_url_full_is_redacted_to_route_template_for_address_in_path()
    {
        var (listener, bag, tag) = RegisterListener();
        using var l = listener;

        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"address\":\"user@example.com\"}");

        using (var scope = new Activity("test-scope").AddBaggage("test.id", tag).Start())
        {
            _ = await client.Suppressions.Bounces.GetAsync("mg.example.com", "user@example.com");
        }

        var activity = Assert.Single(bag);
        var tags = activity.TagObjects.ToDictionary(t => t.Key, t => t.Value?.ToString());
        var urlFull = tags["url.full"]!;
        // The recipient address must NOT appear in any form (raw or URL-encoded).
        Assert.DoesNotContain("user@example.com", urlFull, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("user%40example.com", urlFull, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("user", urlFull, StringComparison.OrdinalIgnoreCase);
        // It IS the parameterized template.
        Assert.Equal("https://api.mailgun.test/v3/{domain}/bounces/{address}", urlFull);
        Assert.Equal("v3/{domain}/bounces/{address}", tags["http.route"]);
    }

    [Fact]
    public async Task Span_url_full_is_redacted_for_address_in_query()
    {
        var (listener, bag, tag) = RegisterListener();
        using var l = listener;

        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"address\":\"user@example.com\",\"result\":\"deliverable\"}");

        using (var scope = new Activity("test-scope").AddBaggage("test.id", tag).Start())
        {
            _ = await client.Validate.ValidateAsync("user@example.com");
        }

        var activity = Assert.Single(bag);
        var tags = activity.TagObjects.ToDictionary(t => t.Key, t => t.Value?.ToString());
        var urlFull = tags["url.full"]!;
        Assert.DoesNotContain("user", urlFull, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.com", urlFull, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("?", urlFull, StringComparison.Ordinal); // query stripped
        Assert.Equal("https://api.mailgun.test/v4/address/validate", urlFull);
    }

    // ── #2 HTTPS enforcement on BaseUrl ───────────────────────────────────────────────────────

    [Fact]
    public void Insecure_http_base_url_throws_at_construction()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new MailgunClient(new MailgunClientOptions { ApiKey = "k", BaseUrl = "http://api.mailgun.net" }));
        Assert.Contains("HTTPS", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Insecure_http_base_url_allowed_with_explicit_optin()
    {
        // Should not throw.
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "http://gateway.internal",
            AllowInsecureBaseUrl = true,
        });
        Assert.NotNull(client);
    }

    [Theory]
    [InlineData("http://localhost:8080")]
    [InlineData("http://127.0.0.1:1234")]
    public void Loopback_http_base_url_is_allowed_without_optin(string baseUrl)
    {
        using var client = new MailgunClient(new MailgunClientOptions { ApiKey = "k", BaseUrl = baseUrl });
        Assert.NotNull(client);
    }

    [Fact]
    public void Default_base_url_is_https_and_constructs_cleanly()
    {
        using var client = new MailgunClient(new MailgunClientOptions { ApiKey = "k" });
        Assert.NotNull(client);
    }

    // ── #3 CR/LF rejection on the subaccount id ───────────────────────────────────────────────

    [Fact]
    public void ForSubaccount_rejects_control_characters()
    {
        using var client = new MailgunClient(new MailgunClientOptions { ApiKey = "k" });
        // Build the bad ids with explicit char codes so the test source itself stays free of any
        // embedded control bytes: CR, LF, CRLF (header-split vector), NUL, and TAB.
        var badIds = new[]
        {
            "acct" + (char)13 + (char)10 + "X-Injected: evil",
            "acct" + (char)10 + "foo",
            "acct" + (char)13 + "bar",
            "acct" + (char)0 + "nul",
            "acct" + (char)9 + "tab",
        };
        foreach (var badId in badIds)
        {
            Assert.Throws<ArgumentException>(() => client.ForSubaccount(badId));
        }
    }

    [Fact]
    public void OnBehalfOf_with_crlf_throws_at_construction()
    {
        var bad = "tenant" + (char)13 + (char)10 + "Host: evil";
        Assert.Throws<ArgumentException>(() =>
            new MailgunClient(new MailgunClientOptions { ApiKey = "k", OnBehalfOf = bad }));
    }

    [Fact]
    public async Task Valid_subaccount_id_still_sends_on_behalf_of_header()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        var sub = client.ForSubaccount("acct_clean123");
        _ = await sub.Domains.ListAsync();

        var req = Assert.Single(handler.Requests);
        Assert.True(req.Headers.TryGetValue("X-Mailgun-On-Behalf-Of", out var v));
        Assert.Equal("acct_clean123", v);
    }

    // ── #4 response-body size cap ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Oversize_response_body_throws_serialization_exception()
    {
        var handler = new MockHttpMessageHandler();
        // 2000-byte body against a 100-byte cap.
        handler.EnqueueResponse(HttpStatusCode.OK, new string('x', 2000));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
            MaxResponseContentBytes = 100,
        });

        await Assert.ThrowsAsync<MailgunSerializationException>(() => client.Domains.ListAsync());
    }

    [Fact]
    public async Task Response_body_within_cap_succeeds()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
            MaxResponseContentBytes = 1024 * 1024,
        });

        var page = await client.Domains.ListAsync();
        Assert.NotNull(page);
    }

    [Fact]
    public void Nonpositive_max_response_bytes_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new MailgunClient(new MailgunClientOptions { ApiKey = "k", MaxResponseContentBytes = 0 }));
    }

    // ── #6a webhook anti-replay default ───────────────────────────────────────────────────────

    [Fact]
    public void Webhook_endpoint_options_default_to_in_memory_replay_protection()
    {
        var options = new MailgunWebhookEndpointOptions();
        Assert.NotNull(options.TokenCache);
        Assert.IsType<InMemoryWebhookTokenCache>(options.TokenCache);
    }

    [Fact]
    public void Webhook_endpoint_options_replay_cache_can_be_disabled_explicitly()
    {
        var options = new MailgunWebhookEndpointOptions { TokenCache = null };
        Assert.Null(options.TokenCache);
    }

    private static (ActivityListener listener, System.Collections.Concurrent.ConcurrentBag<Activity> bag, string tag) RegisterListener()
    {
        var tag = Guid.NewGuid().ToString();
        var bag = new System.Collections.Concurrent.ConcurrentBag<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == MailgunActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = a =>
            {
                if (a.GetBaggageItem("test.id") == tag)
                    bag.Add(a);
            },
        };
        ActivitySource.AddActivityListener(listener);
        return (listener, bag, tag);
    }
}
