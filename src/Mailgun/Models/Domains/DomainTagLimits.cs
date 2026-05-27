using System.Text.Json.Serialization;

namespace Mailgun.Models.Domains;

/// <summary>Response from <c>GET /v3/domains/{domain}/limits/tag</c>.</summary>
public sealed class DomainTagLimits
{
    /// <summary>Server-assigned identifier (optional in some accounts).</summary>
    [JsonPropertyName("id")] public string? Id { get; init; }

    /// <summary>Maximum distinct tags allowed against this domain.</summary>
    [JsonPropertyName("limit")] public long Limit { get; init; }

    /// <summary>Distinct tags currently in use against this domain.</summary>
    [JsonPropertyName("count")] public long Count { get; init; }
}
