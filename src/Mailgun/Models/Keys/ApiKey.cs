using System.Text.Json.Serialization;
using Mailgun.Serialization;

namespace Mailgun.Models.Keys;

/// <summary>An API key as returned by <c>/v1/keys</c>.</summary>
public sealed class ApiKey
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("role")] public string? Role { get; init; }
    [JsonPropertyName("kind")] public string? Kind { get; init; }
    [JsonPropertyName("user_name")] public string? UserName { get; init; }
    [JsonPropertyName("user_email")] public string? UserEmail { get; init; }
    [JsonPropertyName("requestor")] public string? Requestor { get; init; }
    [JsonPropertyName("domain_name")] public string? DomainName { get; init; }

    // Mailgun's /v1/keys timestamps are ISO 8601 without a timezone designator
    // (e.g. "2026-03-17T19:24:58") and the endpoint sometimes emits "" for missing values.
    // NullableIsoDateTimeConverter handles both: empty / null → null, ISO 8601 (no TZ) → UTC.
    [JsonPropertyName("expires_at"), JsonConverter(typeof(NullableIsoDateTimeConverter))]
    public DateTime? ExpiresAt { get; init; }

    [JsonPropertyName("created_at"), JsonConverter(typeof(NullableIsoDateTimeConverter))]
    public DateTime? CreatedAt { get; init; }

    [JsonPropertyName("updated_at"), JsonConverter(typeof(NullableIsoDateTimeConverter))]
    public DateTime? UpdatedAt { get; init; }

    /// <summary>True when the key has been disabled. Mailgun's wire name is <c>is_disabled</c>.</summary>
    [JsonPropertyName("is_disabled")] public bool? IsDisabled { get; init; }
}

/// <summary>Mailgun's response when a new key is created — includes the secret token (one-time visible).</summary>
public sealed class CreatedApiKey
{
    [JsonPropertyName("key")] public string Key { get; init; } = string.Empty;
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("role")] public string? Role { get; init; }
    [JsonPropertyName("kind")] public string? Kind { get; init; }
}

/// <summary>Response from <c>POST /v1/keys/public</c> — regenerate the account public API key.</summary>
public sealed class RegeneratedPublicKey
{
    /// <summary>The new account public key.</summary>
    [JsonPropertyName("key")] public string Key { get; init; } = string.Empty;
    /// <summary>Server-supplied status message.</summary>
    [JsonPropertyName("message")] public string? Message { get; init; }
}

/// <summary>Parameters for <c>POST /v1/keys</c>.</summary>
public sealed class CreateApiKeyRequest
{
    /// <summary>Human-readable label for the key.</summary>
    public string? Description { get; set; }

    /// <summary>Role — e.g. <c>billing</c>, <c>admin</c>, <c>support</c>, <c>analyst</c>, <c>developer</c>, <c>sending</c>.</summary>
    public string? Role { get; set; }

    /// <summary>Optional kind — <c>web</c>, <c>sending</c>, or others; defaults to <c>sending</c> when scope is per-domain.</summary>
    public string? Kind { get; set; }

    /// <summary>Domain to scope a sending key to (required for sending keys).</summary>
    public string? Domain { get; set; }

    /// <summary>Optional expiry.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
}
