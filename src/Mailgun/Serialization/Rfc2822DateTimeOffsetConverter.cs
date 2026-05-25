using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(seconds * 1000.0));
        }
        throw new JsonException("Could not parse Mailgun date value.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        // Mailgun's preferred wire format: RFC 1123 GMT, which is a valid subset of RFC 2822.
        writer.WriteStringValue(value.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture));
    }
}
