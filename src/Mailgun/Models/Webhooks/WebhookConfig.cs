using System.Text.Json.Serialization;

namespace Mailgun.Models.Webhooks;

/// <summary>A Mailgun webhook configuration (one event type, up to three destination URLs).</summary>
public sealed class WebhookConfig
{
    [JsonPropertyName("urls")] public List<string> Urls { get; init; } = new();
}

/// <summary>Full webhook configuration map for a domain (one entry per event type).</summary>
public sealed class WebhooksMap
{
    [JsonPropertyName("webhooks")] public Dictionary<string, WebhookConfig> Webhooks { get; init; } = new();
}

/// <summary>Single-webhook response envelope.</summary>
public sealed class WebhookResponse
{
    [JsonPropertyName("webhook")] public WebhookConfig Webhook { get; init; } = new();
}

/// <summary>
/// A single account-level webhook in the modern <c>/v1/webhooks</c> shape: each webhook has its own
/// id, an optional description, a set of subscribed event types, and a single destination URL.
/// Multiple webhooks can subscribe to the same event type (up to 3 URLs per event type total).
/// </summary>
public sealed class AccountWebhook
{
    /// <summary>The webhook id, populated whether Mailgun returned <c>id</c> (GET / list) or <c>webhook_id</c> (POST create).</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("event_types")] public List<string> EventTypes { get; init; } = new();
    [JsonPropertyName("url")] public string Url { get; init; } = string.Empty;

    // Mailgun's POST /v1/webhooks returns the identifier as "webhook_id", but GET /v1/webhooks
    // and PUT /v1/webhooks/{id} return "id". This write-only alias normalizes both into Id during
    // deserialization. If both fields are present in the same payload (shouldn't happen), the
    // first non-empty wins.
    [JsonPropertyName("webhook_id"), JsonInclude]
    internal string? WebhookIdAlias
    {
        set
        {
            if (!string.IsNullOrEmpty(value) && string.IsNullOrEmpty(Id))
                Id = value;
        }
    }
}

/// <summary>Envelope for <c>GET /v1/webhooks</c> (ID-based list).</summary>
public sealed class AccountWebhookListResponse
{
    [JsonPropertyName("webhooks")] public List<AccountWebhook> Webhooks { get; init; } = new();
}
