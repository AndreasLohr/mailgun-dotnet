using System.Text.Json.Serialization;

namespace Mailgun.Webhooks;

/// <summary>
/// Base type for all parsed Mailgun webhook events. Carries the common envelope plus the parsed
/// signature triple for downstream verification when verification was deferred until after parse.
/// </summary>
public abstract class MailgunWebhookEvent
{
    /// <summary>Mailgun's event type — <c>accepted</c>, <c>delivered</c>, <c>opened</c>, etc.</summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    /// <summary>Mailgun's event id.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>Unix-second timestamp.</summary>
    [JsonPropertyName("timestamp")]
    public double Timestamp { get; init; }

    /// <summary>The source message's headers, attachments, recipients, and size.</summary>
    [JsonPropertyName("message")]
    public WebhookMessageInfo? Message { get; init; }

    /// <summary>Recipient address (delivered/opened/clicked/etc.).</summary>
    [JsonPropertyName("recipient")]
    public string? Recipient { get; init; }

    /// <summary>Recipient domain.</summary>
    [JsonPropertyName("recipient-domain")]
    public string? RecipientDomain { get; init; }

    /// <summary>Tags attached to the message.</summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; init; }

    /// <summary>
    /// Mailgun user variables (Mailgun <c>v:</c> fields the sender attached to the source message).
    /// Stays <c>Dictionary&lt;string, object&gt;?</c> intentionally — user variables are arbitrary
    /// caller-defined JSON values with no fixed schema.
    /// </summary>
    [JsonPropertyName("user-variables")]
    public Dictionary<string, object>? UserVariables { get; init; }

    /// <summary>Geographic location for <c>opened</c> / <c>clicked</c> events.</summary>
    [JsonPropertyName("geolocation")]
    public WebhookGeolocation? Geolocation { get; init; }

    /// <summary>Client info (user-agent, device, bot classifier) for <c>opened</c> / <c>clicked</c> events.</summary>
    [JsonPropertyName("client-info")]
    public WebhookClientInfo? ClientInfo { get; init; }

    /// <summary>IP address (clicks/opens).</summary>
    [JsonPropertyName("ip")]
    public string? Ip { get; init; }

    /// <summary>
    /// The signature envelope from the v4 webhook body. Settable so the parser can attach it
    /// after deserializing the inner event-data object — most other properties are immutable.
    /// </summary>
    public WebhookSignature? Signature { get; set; }
}

/// <summary>The <c>signature</c> envelope from Mailgun's v4 webhook payload.</summary>
public sealed class WebhookSignature
{
    [JsonPropertyName("timestamp")] public string Timestamp { get; init; } = string.Empty;
    [JsonPropertyName("token")] public string Token { get; init; } = string.Empty;
    [JsonPropertyName("signature")] public string Signature { get; init; } = string.Empty;
}
