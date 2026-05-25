using Mailgun.Webhooks;

namespace Mailgun.AspNetCore;

/// <summary>
/// Options for <see cref="MailgunWebhookEndpointExtensions.MapMailgunWebhook"/>.
/// </summary>
public sealed class MailgunWebhookEndpointOptions
{
    /// <summary>
    /// HTTP Webhook Signing Key (from <c>GET /v5/accounts/http_signing_key</c>). Required.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Maximum acceptable clock skew between Mailgun's timestamp and now. Defaults to 15 minutes.</summary>
    public TimeSpan MaxClockSkew { get; set; } = MailgunWebhookSignatureValidator.DefaultMaxAge;

    /// <summary>
    /// Optional anti-replay token cache. When supplied, the endpoint rejects requests whose
    /// signature token has already been seen within <see cref="MaxClockSkew"/>.
    /// </summary>
    public IWebhookTokenCache? TokenCache { get; set; }
}
