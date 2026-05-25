using System.Text.Json.Serialization;

namespace Mailgun.Pagination;

/// <summary>Mailgun's <c>paging</c> envelope (URL-based pagination).</summary>
public sealed class PagingLinks
{
    /// <summary>URL of the first page.</summary>
    [JsonPropertyName("first")] public string? First { get; set; }
    /// <summary>URL of the previous page.</summary>
    [JsonPropertyName("previous")] public string? Previous { get; set; }
    /// <summary>URL of the next page.</summary>
    [JsonPropertyName("next")] public string? Next { get; set; }
    /// <summary>URL of the last page.</summary>
    [JsonPropertyName("last")] public string? Last { get; set; }
}
