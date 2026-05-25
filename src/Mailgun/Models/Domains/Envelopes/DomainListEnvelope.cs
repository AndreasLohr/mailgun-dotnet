using System.Text.Json.Serialization;
using Mailgun.Pagination;

namespace Mailgun.Models.Domains.Envelopes;

internal sealed class DomainListEnvelope
{
    [JsonPropertyName("items")] public List<Domain>? Items { get; set; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; set; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; set; }
}

internal sealed class SmtpCredentialsEnvelope
{
    [JsonPropertyName("items")] public List<SmtpCredential>? Items { get; set; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; set; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; set; }
}
