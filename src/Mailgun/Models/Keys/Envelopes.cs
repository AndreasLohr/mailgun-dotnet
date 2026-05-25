using System.Text.Json.Serialization;
using Mailgun.Pagination;

namespace Mailgun.Models.Keys;

internal sealed class KeyListEnvelope
{
    [JsonPropertyName("items")] public List<ApiKey>? Items { get; set; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; set; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; set; }
}
