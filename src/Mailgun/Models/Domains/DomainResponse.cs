using System.Text.Json.Serialization;

namespace Mailgun.Models.Domains;

/// <summary>Mailgun's domain-single response envelope: <c>{ domain: {...}, receiving_dns_records: [...], sending_dns_records: [...] }</c>.</summary>
public sealed class DomainResponse
{
    [JsonPropertyName("domain")] public Domain Domain { get; init; } = new();
    [JsonPropertyName("receiving_dns_records")] public List<DnsRecord>? ReceivingDnsRecords { get; init; }
    [JsonPropertyName("sending_dns_records")] public List<DnsRecord>? SendingDnsRecords { get; init; }
}

/// <summary>A DNS record Mailgun expects you to provision.</summary>
public sealed class DnsRecord
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("record_type")] public string? RecordType { get; init; }
    [JsonPropertyName("value")] public string? Value { get; init; }
    [JsonPropertyName("valid")] public string? Valid { get; init; }
    [JsonPropertyName("priority")] public string? Priority { get; init; }
    [JsonPropertyName("is_active")] public bool? IsActive { get; init; }
    [JsonPropertyName("cached")] public List<string>? Cached { get; init; }
}
