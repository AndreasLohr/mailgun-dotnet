using System.Text.Json;
using System.Text.Json.Serialization;
using Mailgun.Models.Domains;
using Mailgun.Serialization;
using Mailgun.Services;

namespace Mailgun.Tests.Serialization;

/// <summary>
/// Golden / contract tests that pin the exact wire format the SDK produces and accepts. System.Text.Json
/// serializes properties in declaration order with no whitespace, so request-body goldens are stable
/// exact-string assertions. These lock snake_case mapping, null omission, and the custom date
/// converters against accidental drift (a renamed property or a flipped null policy would break a
/// running integration silently otherwise).
/// </summary>
public class SerializationContractTests
{
    private static readonly JsonSerializerOptions Opts = MailgunJsonOptions.Default;

    // ── Request body goldens (serialize) ──────────────────────────────────────────────────────

    [Fact]
    public void CreateSubaccountRequest_serializes_to_exact_json()
    {
        var json = JsonSerializer.Serialize(new CreateSubaccountRequest { Name = "Acme" }, Opts);
        Assert.Equal("{\"name\":\"Acme\"}", json);
    }

    [Fact]
    public void CreateDynamicIpPoolRequest_omits_null_optional_fields()
    {
        var req = new CreateDynamicIpPoolRequest
        {
            Name = "prod",
            Description = "primary",
            SendStrategy = "ranked",
            // Ips + BackupIpPoolId left null → must be omitted (DefaultIgnoreCondition.WhenWritingNull).
        };
        var json = JsonSerializer.Serialize(req, Opts);
        Assert.Equal("{\"name\":\"prod\",\"description\":\"primary\",\"send_strategy\":\"ranked\"}", json);
    }

    [Fact]
    public void CreateDynamicIpPoolRequest_includes_ips_array_when_set()
    {
        var req = new CreateDynamicIpPoolRequest
        {
            Name = "prod",
            Ips = new() { "1.1.1.1", "2.2.2.2" },
        };
        var json = JsonSerializer.Serialize(req, Opts);
        Assert.Equal("{\"name\":\"prod\",\"ips\":[\"1.1.1.1\",\"2.2.2.2\"]}", json);
    }

    [Fact]
    public void SendAlertRule_minimal_serializes_required_fields_only()
    {
        var rule = new SendAlertRule
        {
            Name = "bounces-high",
            Metric = "bounces",
            Comparator = "gt",
            Limit = "30",
            Dimension = "domain",
        };
        var json = JsonSerializer.Serialize(rule, Opts);
        Assert.Equal(
            "{\"name\":\"bounces-high\",\"metric\":\"bounces\",\"comparator\":\"gt\",\"limit\":\"30\",\"dimension\":\"domain\"}",
            json);
    }

    [Fact]
    public void AlertSettingsEventRequest_serializes_event_type_channel_settings()
    {
        var req = new AlertSettingsEventRequest
        {
            EventType = "send_failure",
            Channel = "email",
            Settings = new() { ["recipients"] = "ops@example.com" },
        };
        var json = JsonSerializer.Serialize(req, Opts);
        Assert.Equal(
            "{\"event_type\":\"send_failure\",\"channel\":\"email\",\"settings\":{\"recipients\":\"ops@example.com\"}}",
            json);
    }

    // ── Naming-policy contract (properties without explicit [JsonPropertyName]) ────────────────

    private sealed class PolicyProbe
    {
        public string FirstName { get; set; } = "";
        public int RetryCount { get; set; }
        public string? OptionalNote { get; set; }
    }

    [Fact]
    public void Property_naming_policy_is_snake_case_lower_with_null_omission()
    {
        var json = JsonSerializer.Serialize(new PolicyProbe { FirstName = "x", RetryCount = 3 }, Opts);
        Assert.Equal("{\"first_name\":\"x\",\"retry_count\":3}", json);
        Assert.DoesNotContain("optional_note", json, StringComparison.Ordinal); // null omitted
    }

    // ── Date converter contracts ──────────────────────────────────────────────────────────────

    private sealed class Rfc2822Probe
    {
        [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
        public DateTimeOffset When { get; set; }
    }

    [Fact]
    public void Rfc2822_converter_writes_numeric_offset_form_and_reads_it_back()
    {
        var value = new DateTimeOffset(2011, 10, 13, 18, 2, 0, TimeSpan.Zero);
        var json = JsonSerializer.Serialize(new Rfc2822Probe { When = value }, Opts);
        // The SDK formats a zero offset as "-0000" (RFC 2822's "offset unknown / UTC" convention),
        // which Mailgun's stricter endpoints accept.
        Assert.Equal("{\"when\":\"Thu, 13 Oct 2011 18:02:00 -0000\"}", json);

        // And Mailgun's "GMT" suffix form parses back to the same instant.
        var parsed = JsonSerializer.Deserialize<Rfc2822Probe>(
            "{\"when\":\"Thu, 13 Oct 2011 18:02:00 GMT\"}", Opts);
        Assert.Equal(value, parsed!.When);
    }

    [Fact]
    public void Rfc2822_converter_reads_numeric_unix_value_too()
    {
        // Mailgun occasionally returns a number where a date is expected.
        var parsed = JsonSerializer.Deserialize<Rfc2822Probe>("{\"when\":1318528920}", Opts);
        Assert.Equal(new DateTimeOffset(2011, 10, 13, 18, 2, 0, TimeSpan.Zero), parsed!.When);
    }

    private sealed class UnixProbe
    {
        [JsonConverter(typeof(UnixTimestampDateTimeOffsetConverter))]
        public DateTimeOffset At { get; set; }
    }

    [Fact]
    public void Unix_converter_round_trips_seconds()
    {
        var value = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var json = JsonSerializer.Serialize(new UnixProbe { At = value }, Opts);
        Assert.Equal($"{{\"at\":{value.ToUnixTimeSeconds()}}}", json);

        var parsed = JsonSerializer.Deserialize<UnixProbe>(json, Opts);
        Assert.Equal(value, parsed!.At);
    }

    [Fact]
    public void Unix_converter_reads_string_seconds()
    {
        var parsed = JsonSerializer.Deserialize<UnixProbe>("{\"at\":\"1767225600\"}", Opts);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), parsed!.At);
    }

    [Fact]
    public void Unix_converter_out_of_range_throws_json_exception_not_overflow()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<UnixProbe>("{\"at\":99999999999999999}", Opts));
    }

    // ── Response deserialization goldens ──────────────────────────────────────────────────────

    [Fact]
    public void IpInfo_deserializes_rfc2822_warmup_date_and_snake_case_fields()
    {
        const string wire =
            "{\"ip\":\"159.0.0.1\",\"dedicated\":true,\"rdns\":\"mta.example.com\"," +
            "\"warmup_state\":\"warming\",\"warmup_started_at\":\"Thu, 13 Oct 2011 18:02:00 +0000\"}";
        var info = JsonSerializer.Deserialize<IpInfo>(wire, Opts)!;

        Assert.Equal("159.0.0.1", info.Ip);
        Assert.True(info.Dedicated);
        Assert.Equal("mta.example.com", info.Rdns);
        Assert.Equal("warming", info.WarmupState);
        Assert.Equal(new DateTimeOffset(2011, 10, 13, 18, 2, 0, TimeSpan.Zero), info.WarmupStartedAt);
    }

    [Fact]
    public void DomainTagLimits_deserializes_counts()
    {
        var limits = JsonSerializer.Deserialize<DomainTagLimits>(
            "{\"id\":\"l1\",\"limit\":4000,\"count\":12}", Opts)!;
        Assert.Equal(4000, limits.Limit);
        Assert.Equal(12, limits.Count);
    }

    [Fact]
    public void Read_is_case_sensitive_unknown_cased_keys_are_ignored()
    {
        // PropertyNameCaseInsensitive = false: a wrong-cased key does not bind. This locks the
        // contract so a future flip to case-insensitive (which can mask real API drift) is caught.
        var limits = JsonSerializer.Deserialize<DomainTagLimits>(
            "{\"Limit\":4000,\"COUNT\":12}", Opts)!;
        Assert.Equal(0, limits.Limit);
        Assert.Equal(0, limits.Count);
    }
}
