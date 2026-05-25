using System.Text.Json.Serialization;

namespace Mailgun.Models.Messages;

/// <summary>Response from <c>POST /v3/{domain}/messages</c> and <c>.mime</c>.</summary>
public sealed class SendMessageResponse
{
    /// <summary>Mailgun-assigned queued message id, surrounded by angle brackets.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable acknowledgement (typically <c>"Queued. Thank you."</c>).</summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}
