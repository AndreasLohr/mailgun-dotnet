using System.Text.Json.Serialization;
using Mailgun.Serialization;

namespace Mailgun.Models.Suppressions;

/// <summary>A bounced address on a Mailgun domain (<c>/v3/{domain}/bounces</c>).</summary>
public sealed class Bounce
{
    [JsonPropertyName("address")] public string Address { get; init; } = string.Empty;
    [JsonPropertyName("code")] public string? Code { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>A complaint record (<c>/v3/{domain}/complaints</c>).</summary>
public sealed class Complaint
{
    [JsonPropertyName("address")] public string Address { get; init; } = string.Empty;
    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>An unsubscribe record (<c>/v3/{domain}/unsubscribes</c>).</summary>
public sealed class Unsubscribe
{
    [JsonPropertyName("address")] public string Address { get; init; } = string.Empty;
    [JsonPropertyName("tags")] public List<string>? Tags { get; init; }
    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>An allowlist (whitelist) record (<c>/v3/{domain}/whitelists</c>).</summary>
public sealed class AllowlistEntry
{
    /// <summary>Either <c>address</c> or <c>domain</c> will be populated depending on the entry type.</summary>
    [JsonPropertyName("value")] public string? Value { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
    [JsonPropertyName("createdAt")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? CreatedAt { get; init; }
}
