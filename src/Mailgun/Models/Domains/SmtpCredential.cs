using System.Text.Json.Serialization;
using Mailgun.Serialization;

namespace Mailgun.Models.Domains;

/// <summary>An SMTP credential for a Mailgun domain.</summary>
public sealed class SmtpCredential
{
    [JsonPropertyName("login")] public string Login { get; init; } = string.Empty;
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("size_bytes")] public long? SizeBytes { get; init; }
}
