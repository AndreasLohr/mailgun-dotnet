using System.Text.Json;
using System.Text.Json.Serialization;
using Mailgun.Models.Domains;

namespace Mailgun.Serialization;

// Made internal-visible to the tests project so SerializationDriftTests can drive the converter
// directly without round-tripping through the full Domain envelope.

/// <summary>
/// Mailgun emits <c>"disabled"</c> on a domain in two shapes:
/// <list type="bullet">
///   <item><description><c>false</c> / <c>true</c> — a bare boolean on the legacy/active-domain wire format.</description></item>
///   <item><description><c>{ "permanently": …, "reason": …, "note": … }</c> — the modern object form on a disabled domain.</description></item>
/// </list>
/// System.Text.Json's default binder rejects the boolean form when the property is typed as
/// <see cref="DomainDisabledInfo"/>. This converter accepts both shapes: object form deserializes
/// normally; boolean form maps to <c>null</c> (the canonical "no disabled-info envelope" answer —
/// callers should look at <see cref="Domain.IsDisabled"/> for the boolean status).
/// </summary>
internal sealed class PolymorphicDomainDisabledConverter : JsonConverter<DomainDisabledInfo?>
{
    public override DomainDisabledInfo? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
            case JsonTokenType.True:
            case JsonTokenType.False:
                return null;
            case JsonTokenType.StartObject:
                return JsonSerializer.Deserialize<DomainDisabledInfo>(ref reader, options);
            default:
                throw new JsonException($"Unexpected token {reader.TokenType} for 'disabled' field; expected null, boolean, or object.");
        }
    }

    public override void Write(Utf8JsonWriter writer, DomainDisabledInfo? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else JsonSerializer.Serialize(writer, value, options);
    }
}
