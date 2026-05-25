using Mailgun.Webhooks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mailgun.AspNetCore;

/// <summary>
/// Endpoint routing helpers for receiving Mailgun webhooks in ASP.NET Core minimal-API apps.
/// </summary>
public static class MailgunWebhookEndpointExtensions
{
    /// <summary>
    /// Maps a Mailgun webhook receiver at the given <paramref name="pattern"/>. The endpoint:
    /// (1) reads the JSON body, (2) verifies the HMAC-SHA256 signature, (3) enforces the
    /// configured clock-skew window, (4) optionally checks the replay token cache,
    /// (5) parses to a typed <see cref="MailgunWebhookEvent"/> and invokes <paramref name="handler"/>.
    /// Returns 200 on success, 401 on invalid signature, 409 on replay, 400 on parse failure.
    /// </summary>
    public static IEndpointConventionBuilder MapMailgunWebhook(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        MailgunWebhookEndpointOptions options,
        Func<MailgunWebhookEvent, HttpContext, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);
        if (string.IsNullOrWhiteSpace(options.SigningKey))
            throw new ArgumentException("SigningKey is required.", nameof(options));

        return endpoints.MapPost(pattern, async (HttpContext context) =>
        {
            var ct = context.RequestAborted;
            var bytes = await ReadBodyAsync(context, ct).ConfigureAwait(false);
            MailgunWebhookEvent evt;
            try
            {
                evt = MailgunWebhookParser.Parse(bytes);
            }
            catch (Exception)
            {
                return Results.BadRequest();
            }

            if (evt.Signature is null)
            {
                return Results.Unauthorized();
            }
            var sig = evt.Signature;

            var valid = MailgunWebhookSignatureValidator.IsValid(
                options.SigningKey, sig.Timestamp, sig.Token, sig.Signature,
                options.MaxClockSkew);
            if (!valid)
            {
                return Results.Unauthorized();
            }

            if (options.TokenCache is { } cache && !cache.MarkSeen(sig.Token, options.MaxClockSkew))
            {
                return Results.Conflict();
            }

            await handler(evt, context, ct).ConfigureAwait(false);
            return Results.Ok();
        });
    }

    private static async Task<byte[]> ReadBodyAsync(HttpContext context, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await context.Request.Body.CopyToAsync(ms, ct).ConfigureAwait(false);
        return ms.ToArray();
    }
}
