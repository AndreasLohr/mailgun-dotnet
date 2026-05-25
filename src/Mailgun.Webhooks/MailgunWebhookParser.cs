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
    /// <summary>
    /// Parse from a UTF-8 byte buffer. <see cref="JsonDocument.Parse(ReadOnlyMemory{byte}, JsonDocumentOptions)"/>
    /// cannot take a span (a span can't be heap-stored), so this overload allocates a one-shot copy
    /// before parsing. Prefer the <see cref="Parse(ReadOnlyMemory{byte})"/> overload when you already
    /// hold the bytes in a heap-backed buffer — it parses in place with no copy.
    /// </summary>
    public static MailgunWebhookEvent Parse(ReadOnlySpan<byte> utf8Json)
    {
        using var doc = JsonDocument.Parse(utf8Json.ToArray());
        return ParseDocument(doc);
    }

    /// <summary>
    /// Parse from a heap-backed UTF-8 buffer without copying. Pass a <see cref="ReadOnlyMemory{T}"/>
    /// over the body bytes (e.g. <c>buffer.AsMemory()</c>) and <see cref="JsonDocument"/> parses in place.
    /// </summary>
    public static MailgunWebhookEvent Parse(ReadOnlyMemory<byte> utf8Json)
    {
        using var doc = JsonDocument.Parse(utf8Json);
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

    /// <summary>
    /// Pulls only the <c>signature</c> object out of a Mailgun webhook payload so callers can verify
    /// the HMAC before paying for full typed deserialization of <c>event-data</c>. Returns
    /// <c>false</c> for malformed JSON or a missing/incomplete signature block.
    /// </summary>
    public static bool TryExtractSignature(ReadOnlyMemory<byte> utf8Json, out WebhookSignature signature)
    {
        try
        {
            using var doc = JsonDocument.Parse(utf8Json);
            if (doc.RootElement.TryGetProperty("signature", out var sig)
                && sig.TryGetProperty("timestamp", out var t)
                && sig.TryGetProperty("token", out var tk)
                && sig.TryGetProperty("signature", out var s))
            {
                signature = new WebhookSignature
                {
                    Timestamp = t.GetString() ?? string.Empty,
                    Token = tk.GetString() ?? string.Empty,
                    Signature = s.GetString() ?? string.Empty,
                };
                return true;
            }
        }
        catch (JsonException)
        {
            // fall through to false
        }
        signature = new WebhookSignature();
        return false;
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
