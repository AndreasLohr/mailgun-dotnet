using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mailgun.Serialization;

/// <summary>
/// Reads/writes Mailgun's Unix-seconds timestamps (as JSON numbers).
/// Events and logs commonly use this format.
/// </summary>
public sealed class UnixTimestampDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetDouble(out var seconds))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds((long)(seconds * 1000.0));
            }
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (!string.IsNullOrEmpty(s) && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var asDouble))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds((long)(asDouble * 1000.0));
            }
        }
        throw new JsonException("Could not parse Unix timestamp value.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.ToUnixTimeMilliseconds() / 1000.0);
}
