using System.Text.Json;
using Mailgun.Webhooks.Events;
using Mailgun.Webhooks.Internal;

namespace Mailgun.Webhooks;

/// <summary>
/// Parses Mailgun's JSON webhook payload (v4 envelope: <c>{ "signature": {...}, "event-data": {...} }</c>)
/// into a strongly-typed <see cref="MailgunWebhookEvent"/>. Unknown event types fall back to
/// <see cref="UnknownMailgunWebhookEvent"/> so consumers stay forward-compatible.
/// </summary>
public static class MailgunWebhookParser
{
    /// <summary>Parse from a UTF-8 byte buffer. Prefer this overload to avoid an extra string allocation.</summary>
    public static MailgunWebhookEvent Parse(ReadOnlySpan<byte> utf8Json)
    {
        using var doc = JsonDocument.Parse(utf8Json.ToArray());
        return ParseDocument(doc);
    }

    /// <summary>Parse from a JSON string.</summary>
    public static MailgunWebhookEvent Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var doc = JsonDocument.Parse(json);
        return ParseDocument(doc);
    }

    /// <summary>Parse from a stream.</summary>
    public static async Task<MailgunWebhookEvent> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ParseDocument(doc);
    }

    private static MailgunWebhookEvent ParseDocument(JsonDocument doc)
    {
        var root = doc.RootElement;

        JsonElement eventData;
        WebhookSignature? signature = null;

        if (root.TryGetProperty("event-data", out var ed))
        {
            eventData = ed;
            if (root.TryGetProperty("signature", out var sig))
            {
                signature = new WebhookSignature
                {
                    Timestamp = sig.TryGetProperty("timestamp", out var t) ? (t.GetString() ?? string.Empty) : string.Empty,
                    Token = sig.TryGetProperty("token", out var tk) ? (tk.GetString() ?? string.Empty) : string.Empty,
                    Signature = sig.TryGetProperty("signature", out var s) ? (s.GetString() ?? string.Empty) : string.Empty,
                };
            }
        }
        else
        {
            eventData = root;
        }

        var eventType = eventData.TryGetProperty("event", out var evProp) ? evProp.GetString() : null;
        var rawEventData = eventData.GetRawText();
        var severity = eventData.TryGetProperty("severity", out var sv) ? sv.GetString() : null;

        MailgunWebhookEvent typed = (eventType, severity) switch
        {
            (MailgunEventTypes.Accepted, _) => Deserialize<AcceptedEvent>(rawEventData),
            (MailgunEventTypes.Delivered, _) => Deserialize<DeliveredEvent>(rawEventData),
            (MailgunEventTypes.Opened, _) => Deserialize<OpenedEvent>(rawEventData),
            (MailgunEventTypes.Clicked, _) => Deserialize<ClickedEvent>(rawEventData),
            (MailgunEventTypes.Unsubscribed, _) => Deserialize<UnsubscribedEvent>(rawEventData),
            (MailgunEventTypes.Complained, _) => Deserialize<ComplainedEvent>(rawEventData),
            ("failed", "permanent") => Deserialize<PermanentFailEvent>(rawEventData),
            ("failed", _) => Deserialize<TemporaryFailEvent>(rawEventData),
            _ => new UnknownMailgunWebhookEvent
            {
                Event = eventType ?? string.Empty,
                RawJson = rawEventData,
            },
        };

        typed.Signature = signature;
        return typed;
    }

    private static T Deserialize<T>(string json) where T : MailgunWebhookEvent
    {
        var parsed = JsonSerializer.Deserialize<T>(json, WebhookJsonOptions.Default);
        return parsed ?? throw new InvalidOperationException("Failed to deserialize Mailgun webhook event.");
    }
}
