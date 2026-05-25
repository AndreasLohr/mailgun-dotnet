using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mailgun.Serialization;

/// <summary>
/// Reads a Mailgun ISO 8601 timestamp string into <see cref="DateTime"/>, mapping JSON <c>null</c>
/// and empty strings to <c>null</c>. Mailgun's <c>/v1/keys</c> endpoint, for example, returns
/// <c>"created_at": ""</c> on keys without a recorded creation time — System.Text.Json's default
/// binder rejects an empty string for <c>DateTime?</c>.
/// </summary>
internal sealed class NullableIsoDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
            {
                var s = reader.GetString();
                if (string.IsNullOrEmpty(s))
                    return null;
                // AssumeUniversal: Mailgun documents these timestamps as UTC, even though the string
                // they emit has no timezone designator.
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var parsed))
                    return parsed;
                throw new JsonException($"Could not parse '{s}' as a DateTime.");
            }
            default:
                throw new JsonException($"Expected string or null for DateTime, got {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
    }
}
