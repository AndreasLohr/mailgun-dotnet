using System.Text.Json;
using System.Text.Json.Serialization;
using Mailgun.Serialization;

namespace Mailgun.Tests.Serialization;

public class ConverterTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions();
        o.Converters.Add(new Rfc2822DateTimeOffsetConverter());
        return o;
    }

    private sealed class Rfc2822Holder
    {
        [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
        public DateTimeOffset At { get; set; }
    }

    private sealed class UnixHolder
    {
        [JsonConverter(typeof(UnixTimestampDateTimeOffsetConverter))]
        public DateTimeOffset At { get; set; }
    }

    [Fact]
    public void Rfc2822_parses_canonical_mailgun_date_string()
    {
        var json = "{\"At\":\"Thu, 13 Oct 2011 18:02:00 +0000\"}";
        var parsed = JsonSerializer.Deserialize<Rfc2822Holder>(json)!;
        Assert.Equal(new DateTimeOffset(2011, 10, 13, 18, 2, 0, TimeSpan.Zero), parsed.At);
    }

    [Fact]
    public void Rfc2822_parses_with_offset_and_normalizes_to_utc()
    {
        var json = "{\"At\":\"Thu, 13 Oct 2011 20:02:00 +0200\"}";
        var parsed = JsonSerializer.Deserialize<Rfc2822Holder>(json)!;
        Assert.Equal(new DateTimeOffset(2011, 10, 13, 18, 2, 0, TimeSpan.Zero), parsed.At);
    }

    [Fact]
    public void Rfc2822_writes_rfc2822_numeric_offset_string_in_utc()
    {
        // Strict RFC-2822 numeric offset, UTC-normalized. Mailgun's /v1/analytics/logs rejects the
        // RFC-1123 "GMT" textual zone, so the converter emits "-0000" instead.
        var holder = new Rfc2822Holder { At = new DateTimeOffset(2026, 5, 16, 14, 30, 0, TimeSpan.FromHours(2)) };
        var json = JsonSerializer.Serialize(holder);
        Assert.Contains("16 May 2026 12:30:00 -0000", json, StringComparison.Ordinal);
        Assert.DoesNotContain("GMT", json, StringComparison.Ordinal);
        Assert.DoesNotContain("14:30", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Rfc2822_accepts_numeric_json_value()
    {
        // The converter currently treats a numeric value as seconds (multiplied by 1000 to ms),
        // so 1.318536120e9 → 1318536120000ms → 2011-10-13T18:02:00Z. The exact instant matters less
        // than the converter producing a stable, well-defined parse.
        var json = "{\"At\": 1318536120}";
        var parsed = JsonSerializer.Deserialize<Rfc2822Holder>(json)!;
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1318536120_000), parsed.At);
    }

    [Fact]
    public void Rfc2822_throws_on_unparseable_string()
    {
        var json = "{\"At\":\"not-a-date\"}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Rfc2822Holder>(json));
    }

    [Fact]
    public void Rfc2822_throws_JsonException_not_AOORE_on_numeric_overflow()
    {
        // Regression: (long)(huge_double * 1000.0) wraps to long.MinValue, which then makes
        // DateTimeOffset.FromUnixTimeMilliseconds throw ArgumentOutOfRangeException. The
        // converter must catch and rethrow as JsonException to match its documented contract.
        var json = "{\"At\":1e308}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Rfc2822Holder>(json));
    }

    [Fact]
    public void Unix_throws_JsonException_not_AOORE_on_numeric_overflow()
    {
        var json = "{\"At\":1e308}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UnixHolder>(json));
    }

    [Fact]
    public void Unix_timestamp_parses_number_and_round_trips()
    {
        var holder = new UnixHolder { At = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000) };
        var json = JsonSerializer.Serialize(holder);
        Assert.Contains("1700000000", json, StringComparison.Ordinal);
        var roundtrip = JsonSerializer.Deserialize<UnixHolder>(json)!;
        Assert.Equal(holder.At, roundtrip.At);
    }

    [Fact]
    public void Unix_timestamp_accepts_decimal_seconds_for_subsecond_precision()
    {
        var json = "{\"At\":1758000000.5}";
        var parsed = JsonSerializer.Deserialize<UnixHolder>(json)!;
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1758000000500), parsed.At);
    }

    [Fact]
    public void Unix_timestamp_accepts_string_form()
    {
        var json = "{\"At\":\"1700000000\"}";
        var parsed = JsonSerializer.Deserialize<UnixHolder>(json)!;
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), parsed.At);
    }
}
