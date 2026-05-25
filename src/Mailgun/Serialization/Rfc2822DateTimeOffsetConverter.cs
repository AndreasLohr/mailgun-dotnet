using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mailgun.Internal;

namespace Mailgun.Serialization;

/// <summary>
/// Reads/writes Mailgun's RFC 2822 date strings (<c>Thu, 13 Oct 2011 18:02:00 +0000</c>).
/// Mailgun also occasionally returns Unix-seconds as numbers; those are handled by
/// <see cref="UnixTimestampDateTimeOffsetConverter"/>.
/// </summary>
public sealed class Rfc2822DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const DateTimeStyles ParseStyles =
        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (!string.IsNullOrEmpty(s)
                && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, ParseStyles, out var parsed))
            {
                return parsed;
            }
        }
        else if (reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var seconds))
        {
            // FromUnixTimeMilliseconds throws ArgumentOutOfRangeException outside year 0001..9999;
            // wrap so callers see the documented JsonException instead.
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds((long)(seconds * 1000.0));
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new JsonException("Mailgun date numeric value is outside the supported DateTimeOffset range.", ex);
            }
        }
        throw new JsonException("Could not parse Mailgun date value.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        // Strict RFC-2822 numeric-offset form. See MailgunDate.FormatRfc2822 for the rationale —
        // .NET's "r" format emits "GMT" which Mailgun's stricter endpoints (e.g. /v1/analytics/logs)
        // reject as an invalid format.
        writer.WriteStringValue(MailgunDate.FormatRfc2822(value));
    }
}
