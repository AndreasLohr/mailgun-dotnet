using System.Text.Json.Serialization;

namespace Mailgun.Webhooks;

/// <summary>
/// The <c>delivery-status</c> envelope inside a Mailgun webhook event. Mailgun documents these
/// fields for <c>delivered</c>, <c>failed</c>, and (partially) <c>rejected</c> events.
/// </summary>
public sealed class WebhookDeliveryStatus
{
    /// <summary>True when the message was delivered over TLS.</summary>
    [JsonPropertyName("tls")] public bool? Tls { get; init; }

    /// <summary>True when the remote MX-host's TLS certificate was validated.</summary>
    [JsonPropertyName("certificate-verified")] public bool? CertificateVerified { get; init; }

    /// <summary>The receiving MX host Mailgun handed the message to.</summary>
    [JsonPropertyName("mx-host")] public string? MxHost { get; init; }

    /// <summary>1-based attempt number for this recipient.</summary>
    [JsonPropertyName("attempt-no")] public int? AttemptNumber { get; init; }

    /// <summary>Free-form human description from Mailgun, e.g. <c>"Mailbox does not exist"</c>.</summary>
    [JsonPropertyName("description")] public string? Description { get; init; }

    /// <summary>SMTP session duration in seconds.</summary>
    [JsonPropertyName("session-seconds")] public double? SessionSeconds { get; init; }

    /// <summary>True if SMTPUTF8 was used.</summary>
    [JsonPropertyName("utf8")] public bool? Utf8 { get; init; }

    /// <summary>SMTP code returned by the remote MTA (or Mailgun's internal classification code).</summary>
    [JsonPropertyName("code")] public int? Code { get; init; }

    /// <summary>Verbatim SMTP response from the remote MTA.</summary>
    [JsonPropertyName("message")] public string? Message { get; init; }

    /// <summary>Mailgun-assigned bounce classification (e.g. <c>HARD</c>, <c>SOFT</c>).</summary>
    [JsonPropertyName("bounce_classification")] public string? BounceClassification { get; init; }

    /// <summary>Enhanced-status-code field from Mailgun, when available.</summary>
    [JsonPropertyName("enhanced-code")] public string? EnhancedCode { get; init; }
}

/// <summary>The <c>envelope</c> sub-object on a Mailgun webhook event.</summary>
public sealed class WebhookEnvelope
{
    /// <summary>Transport class — typically <c>smtp</c>.</summary>
    [JsonPropertyName("transport")] public string? Transport { get; init; }

    /// <summary>Envelope sender (the <c>MAIL FROM</c> address).</summary>
    [JsonPropertyName("sender")] public string? Sender { get; init; }

    /// <summary>The Mailgun IP that performed the send.</summary>
    [JsonPropertyName("sending-ip")] public string? SendingIp { get; init; }

    /// <summary>Target SMTP host or recipient address.</summary>
    [JsonPropertyName("targets")] public string? Targets { get; init; }
}

/// <summary>The <c>geolocation</c> sub-object on Mailgun <c>opened</c> and <c>clicked</c> events.</summary>
public sealed class WebhookGeolocation
{
    [JsonPropertyName("country")] public string? Country { get; init; }
    [JsonPropertyName("region")] public string? Region { get; init; }
    [JsonPropertyName("city")] public string? City { get; init; }
}

/// <summary>The <c>client-info</c> sub-object on Mailgun <c>opened</c> and <c>clicked</c> events.</summary>
public sealed class WebhookClientInfo
{
    [JsonPropertyName("client-os")] public string? ClientOs { get; init; }
    [JsonPropertyName("device-type")] public string? DeviceType { get; init; }
    [JsonPropertyName("client-name")] public string? ClientName { get; init; }
    [JsonPropertyName("client-type")] public string? ClientType { get; init; }
    [JsonPropertyName("user-agent")] public string? UserAgent { get; init; }

    /// <summary>Bot classifier string — present when Mailgun thinks the open was from an automated client.</summary>
    [JsonPropertyName("bot")] public string? Bot { get; init; }

    /// <summary>The originating IP, sometimes duplicated here from the top-level event.</summary>
    [JsonPropertyName("ip")] public string? Ip { get; init; }
}
