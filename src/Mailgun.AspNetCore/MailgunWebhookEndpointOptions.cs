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
    /// Anti-replay token cache. Rejects requests whose signature token has already been seen within
    /// <see cref="MaxClockSkew"/>. Defaults to an in-process <see cref="InMemoryWebhookTokenCache"/>
    /// so replay protection is <strong>on by default</strong> for single-instance receivers.
    /// </summary>
    /// <remarks>
    /// The in-memory default protects a single process only. Behind more than one instance, swap in a
    /// distributed cache (see <c>mailgun-dotnet.Webhooks.DistributedCache</c>) so a token replayed
    /// against a different instance is still caught. Set this to <c>null</c> to disable replay
    /// protection entirely (not recommended).
    /// </remarks>
    public IWebhookTokenCache? TokenCache { get; set; } = new InMemoryWebhookTokenCache();

    /// <summary>
    /// Hard cap on the request body size in bytes. Anything larger is rejected with <c>413 Payload Too Large</c>
    /// before the SDK reads or parses any of it. Mailgun's real webhook payloads are typically a few KB;
    /// the default of 256 KB leaves generous headroom while preventing unauthenticated POSTs from
    /// streaming arbitrarily large bodies into RAM.
    /// </summary>
    public int MaxRequestBytes { get; set; } = 256 * 1024;
}
