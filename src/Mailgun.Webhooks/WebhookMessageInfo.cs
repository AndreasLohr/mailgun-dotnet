using System.Text.Json.Serialization;

namespace Mailgun.Webhooks;

/// <summary>
/// The <c>message</c> envelope embedded in Mailgun webhook payloads. Holds the headers,
/// attachments, recipients, and storage metadata for the message that triggered the event.
/// </summary>
public sealed class WebhookMessageInfo
{
    /// <summary>RFC-822 headers as a name → value map. Mailgun emits at least <c>message-id</c>,
    /// <c>from</c>, <c>to</c>, <c>subject</c>.</summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; init; }

    /// <summary>Attachment metadata, when the source message had attachments.</summary>
    [JsonPropertyName("attachments")]
    public List<WebhookAttachmentInfo>? Attachments { get; init; }

    /// <summary>Total recipient count Mailgun fanned the message out to.</summary>
    [JsonPropertyName("recipients")]
    public List<string>? Recipients { get; init; }

    /// <summary>Total size of the source message in bytes (headers + body + attachments).</summary>
    [JsonPropertyName("size")]
    public long? Size { get; init; }
}

/// <summary>Attachment metadata inside <see cref="WebhookMessageInfo.Attachments"/>.</summary>
public sealed class WebhookAttachmentInfo
{
    [JsonPropertyName("filename")] public string? FileName { get; init; }
    [JsonPropertyName("content-type")] public string? ContentType { get; init; }
    [JsonPropertyName("size")] public long? Size { get; init; }
}
