using System.Text.Json.Serialization;

namespace Mailgun.Models.Messages;

/// <summary>A stored MIME message retrieved from Mailgun's queue (<c>GET /v3/domains/{domain}/messages/{storageKey}</c>).</summary>
public sealed class StoredMessage
{
    [JsonPropertyName("Subject")] public string? Subject { get; init; }
    [JsonPropertyName("From")] public string? From { get; init; }
    [JsonPropertyName("To")] public string? To { get; init; }
    [JsonPropertyName("Cc")] public string? Cc { get; init; }
    [JsonPropertyName("Bcc")] public string? Bcc { get; init; }
    [JsonPropertyName("Date")] public string? Date { get; init; }
    [JsonPropertyName("Message-Id")] public string? MessageId { get; init; }
    [JsonPropertyName("body-plain")] public string? BodyPlain { get; init; }
    [JsonPropertyName("body-html")] public string? BodyHtml { get; init; }
    [JsonPropertyName("stripped-text")] public string? StrippedText { get; init; }
    [JsonPropertyName("stripped-html")] public string? StrippedHtml { get; init; }
    [JsonPropertyName("stripped-signature")] public string? StrippedSignature { get; init; }
    [JsonPropertyName("attachments")] public List<StoredMessageAttachment>? Attachments { get; init; }
    [JsonPropertyName("message-headers")] public List<List<string>>? MessageHeaders { get; init; }
    [JsonPropertyName("recipients")] public string? Recipients { get; init; }
    [JsonPropertyName("sender")] public string? Sender { get; init; }
}

/// <summary>Attachment metadata in a stored message.</summary>
public sealed class StoredMessageAttachment
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("content-type")] public string? ContentType { get; init; }
    [JsonPropertyName("size")] public long? Size { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
}
