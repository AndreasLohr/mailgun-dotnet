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

/// <summary>Response from <c>DELETE /v3/domains/{domain}/credentials</c> (delete all).</summary>
public sealed class DeleteAllSmtpCredentialsResponse
{
    [JsonPropertyName("message")] public string? Message { get; init; }
    /// <summary>The number of credentials Mailgun deleted in this call.</summary>
    [JsonPropertyName("count")] public int Count { get; init; }
}
