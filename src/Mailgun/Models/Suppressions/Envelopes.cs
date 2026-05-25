using System.Text.Json.Serialization;
using Mailgun.Pagination;

namespace Mailgun.Models.Suppressions;

internal sealed class BounceListEnvelope
{
    [JsonPropertyName("items")] public List<Bounce>? Items { get; set; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; set; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; set; }
}

internal sealed class ComplaintListEnvelope
{
    [JsonPropertyName("items")] public List<Complaint>? Items { get; set; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; set; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; set; }
}

internal sealed class UnsubscribeListEnvelope
{
    [JsonPropertyName("items")] public List<Unsubscribe>? Items { get; set; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; set; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; set; }
}

internal sealed class AllowlistListEnvelope
{
    [JsonPropertyName("items")] public List<AllowlistEntry>? Items { get; set; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; set; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; set; }
}
