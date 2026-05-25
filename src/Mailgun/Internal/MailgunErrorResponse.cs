using System.Text.Json.Serialization;

namespace Mailgun.Internal;

/// <summary>
/// Mailgun's error response envelope. The dominant shape is <c>{"message":"..."}</c> but some
/// endpoints add <c>details</c> or <c>errors</c>. Either string or array is accepted; the
/// SDK normalizes both into the typed exception's <c>Details</c> list.
/// </summary>
internal sealed class MailgunErrorResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("Message")]
    public string? MessageCapital { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("details")]
    public object? Details { get; set; }

    [JsonPropertyName("errors")]
    public object? Errors { get; set; }
}
