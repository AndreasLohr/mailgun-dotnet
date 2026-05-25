using System.Text.Json.Serialization;

namespace Mailgun.Webhooks.Events;

/// <summary>
/// Wire-level Mailgun webhook <c>event</c> field values. Only six distinct values exist on the
/// wire (<c>accepted</c>, <c>delivered</c>, <c>opened</c>, <c>clicked</c>, <c>unsubscribed</c>,
/// <c>complained</c>, <c>failed</c>) — failure is split into <c>PermanentFailEvent</c> and
/// <c>TemporaryFailEvent</c> at the SDK layer based on the <c>severity</c> field. See
/// <see cref="MailgunFailureSeverities"/> for that secondary discriminator.
/// </summary>
public static class MailgunEventTypes
{
    public const string Accepted = "accepted";
    public const string Delivered = "delivered";
    public const string Opened = "opened";
    public const string Clicked = "clicked";
    public const string Unsubscribed = "unsubscribed";
    public const string Complained = "complained";

    /// <summary>
    /// Both permanent and temporary failures share the wire value <c>"failed"</c>. The SDK
    /// uses the <c>severity</c> field to discriminate; see <see cref="MailgunFailureSeverities"/>.
    /// </summary>
    public const string Failed = "failed";
}

/// <summary>Secondary discriminator on <c>failed</c> events. Mailgun sets <c>severity = "permanent"</c>
/// for hard bounces / blocks and <c>severity = "temporary"</c> for retryable failures.</summary>
public static class MailgunFailureSeverities
{
    public const string Permanent = "permanent";
    public const string Temporary = "temporary";
}

/// <summary>Mailgun <c>accepted</c> event.</summary>
public sealed class AcceptedEvent : MailgunWebhookEvent { }

/// <summary>Mailgun <c>delivered</c> event.</summary>
public sealed class DeliveredEvent : MailgunWebhookEvent
{
    /// <summary>SMTP code + description from the remote MTA.</summary>
    [JsonPropertyName("delivery-status")] public WebhookDeliveryStatus? DeliveryStatus { get; init; }

    /// <summary>Envelope (sender / sending-ip / transport / targets) of the delivered message.</summary>
    [JsonPropertyName("envelope")] public WebhookEnvelope? Envelope { get; init; }
}

/// <summary>Mailgun <c>opened</c> event.</summary>
public sealed class OpenedEvent : MailgunWebhookEvent { }

/// <summary>Mailgun <c>clicked</c> event.</summary>
public sealed class ClickedEvent : MailgunWebhookEvent
{
    /// <summary>The URL that was clicked.</summary>
    [JsonPropertyName("url")] public string? Url { get; init; }
}

/// <summary>Mailgun <c>unsubscribed</c> event.</summary>
public sealed class UnsubscribedEvent : MailgunWebhookEvent { }

/// <summary>Mailgun <c>complained</c> event.</summary>
public sealed class ComplainedEvent : MailgunWebhookEvent { }

/// <summary>
/// Mailgun <c>failed</c> event with <c>severity = permanent</c> — hard bounces, blocked addresses,
/// permanent policy rejections. Won't be retried.
/// </summary>
public sealed class PermanentFailEvent : MailgunWebhookEvent
{
    /// <summary>Always the literal <see cref="MailgunFailureSeverities.Permanent"/> on this type.</summary>
    [JsonPropertyName("severity")] public string? Severity { get; init; }

    /// <summary>Human-readable reason supplied by Mailgun (e.g. <c>"bounce"</c>, <c>"suppress-bounce"</c>).</summary>
    [JsonPropertyName("reason")] public string? Reason { get; init; }

    /// <summary>SMTP code + description from the remote MTA, when available.</summary>
    [JsonPropertyName("delivery-status")] public WebhookDeliveryStatus? DeliveryStatus { get; init; }

    /// <summary>Envelope of the failed delivery attempt.</summary>
    [JsonPropertyName("envelope")] public WebhookEnvelope? Envelope { get; init; }
}

/// <summary>
/// Mailgun <c>failed</c> event with <c>severity = temporary</c> — soft bounces, deferrals, transient
/// errors. Mailgun will keep retrying internally; this notification is informational.
/// </summary>
public sealed class TemporaryFailEvent : MailgunWebhookEvent
{
    /// <summary>Always the literal <see cref="MailgunFailureSeverities.Temporary"/> on this type.</summary>
    [JsonPropertyName("severity")] public string? Severity { get; init; }

    /// <summary>Human-readable reason supplied by Mailgun (e.g. <c>"old"</c>, <c>"generic"</c>).</summary>
    [JsonPropertyName("reason")] public string? Reason { get; init; }

    /// <summary>SMTP code + description from the remote MTA, when available.</summary>
    [JsonPropertyName("delivery-status")] public WebhookDeliveryStatus? DeliveryStatus { get; init; }

    /// <summary>Envelope of the failed delivery attempt.</summary>
    [JsonPropertyName("envelope")] public WebhookEnvelope? Envelope { get; init; }
}

/// <summary>Forward-compatible fallback for unknown event types.</summary>
public sealed class UnknownMailgunWebhookEvent : MailgunWebhookEvent
{
    /// <summary>The raw JSON of the event-data object, for callers that need to inspect unmapped fields.</summary>
    public string RawJson { get; init; } = string.Empty;
}
