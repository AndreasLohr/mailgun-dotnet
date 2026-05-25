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

            // Reject obviously oversized bodies before reading anything off the wire.
            if (context.Request.ContentLength is long declared && declared > options.MaxRequestBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var (bytes, exceededCap) = await ReadBodyAsync(context, options.MaxRequestBytes, ct).ConfigureAwait(false);
            if (exceededCap)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            // Verify the signature BEFORE typed deserialization. A cheap JsonDocument peek for the
            // signature object lets us reject unauthenticated/forged requests without paying the cost
            // of typed event-data parsing on attacker-controlled input.
            if (!MailgunWebhookParser.TryExtractSignature(bytes, out var sig))
            {
                return Results.Unauthorized();
            }

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

            MailgunWebhookEvent evt;
            try
            {
                evt = MailgunWebhookParser.Parse(bytes);
            }
            catch (Exception)
            {
                return Results.BadRequest();
            }

            await handler(evt, context, ct).ConfigureAwait(false);
            return Results.Ok();
        });
    }

    private static async Task<(ReadOnlyMemory<byte> Bytes, bool ExceededCap)> ReadBodyAsync(
        HttpContext context, int maxBytes, CancellationToken ct)
    {
        // Read up to maxBytes+1 so we can detect a body that's exactly at the cap vs. exceeds it.
        var ms = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await context.Request.Body.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
        {
            if (ms.Length + read > maxBytes)
            {
                return (default, true);
            }
            await ms.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }
        return (ms.ToArray().AsMemory(), false);
    }
}
