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
        // FromUnixTimeMilliseconds throws ArgumentOutOfRangeException on overflow (cast of a huge
        // double to long wraps to long.MinValue, which is outside the supported range). Wrap both
        // branches so callers see the documented JsonException.
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetDouble(out var seconds))
            {
                return FromUnixSecondsSafe(seconds);
            }
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (!string.IsNullOrEmpty(s) && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var asDouble))
            {
                return FromUnixSecondsSafe(asDouble);
            }
        }
        throw new JsonException("Could not parse Unix timestamp value.");
    }

    private static DateTimeOffset FromUnixSecondsSafe(double seconds)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(seconds * 1000.0));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new JsonException("Unix timestamp value is outside the supported DateTimeOffset range.", ex);
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.ToUnixTimeMilliseconds() / 1000.0);
}
