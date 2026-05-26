using System.Diagnostics;
using System.Net;

namespace Mailgun.Http;

/// <summary>
/// <see cref="DelegatingHandler"/> that retries 429 (honoring <c>X-RateLimit-Reset</c>) and idempotent
/// 5xx responses up to a configurable maximum, with exponential backoff as a fallback.
/// </summary>
internal sealed class RateLimitHandler : DelegatingHandler
{
    private readonly int _maxRetries;
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    public RateLimitHandler(int maxRetries)
    {
        _maxRetries = Math.Max(0, maxRetries);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!ShouldRetry(response.StatusCode, request.Method, request.RequestUri) || attempt >= _maxRetries)
            {
                return response;
            }

            // One retry counter increment per *decided* retry. Read the route template from the
            // request options where SendCoreAsync stamped it — the handler has no other channel.
            var routeTemplate = request.Options.TryGetValue(MailgunMeter.RouteTemplateKey, out var rt) ? rt : string.Empty;
            var reason = response.StatusCode == HttpStatusCode.TooManyRequests ? "429" : "5xx";
            MailgunMeter.RequestRetries.Add(1, new TagList
            {
                { "http.request.method", request.Method.Method },
                { "http.route", routeTemplate },
                { "retry.reason", reason },
                { "server.address", request.RequestUri?.Host ?? string.Empty },
            });

            var delay = ComputeDelay(response, attempt);
            response.Dispose();
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            attempt++;
        }
    }

    private static bool ShouldRetry(HttpStatusCode status, HttpMethod method, Uri? uri)
    {
        if (status == HttpStatusCode.TooManyRequests)
            return true;
        if ((int)status >= 500)
            return IsIdempotent(method) && !IsActionEndpoint(uri);
        return false;
    }

    private static bool IsIdempotent(HttpMethod method) =>
        method == HttpMethod.Get
        || method == HttpMethod.Head
        || method == HttpMethod.Put
        || method == HttpMethod.Delete
        || method == HttpMethod.Options;

    /// <summary>
    /// True when the request hits a Mailgun action endpoint that generates a side-effect every call,
    /// regardless of HTTP method semantics. <c>POST /v1/dkim_management/domains/{name}/rotate</c>
    /// is the canonical case — each call generates a brand-new DKIM key. Retrying on a transient
    /// 5xx after the server already rotated would silently double-rotate, leaving the caller without
    /// the first key's public material and the DNS state pointing at a superseded key.
    /// </summary>
    /// <remarks>
    /// The matching is anchored to the LAST path segment to avoid false positives — substring
    /// matching catches domain names like <c>refresh-club.com</c> or <c>rotate-tracking.com</c>
    /// that contain these tokens but appear in middle path segments. We also exclude segments
    /// containing a dot (i.e. domain-shaped) so a single-segment path of <c>/v3/domains/{name}</c>
    /// where the name happens to start with <c>refresh-</c> doesn't false-positive either.
    /// </remarks>
    private static bool IsActionEndpoint(Uri? uri)
    {
        if (uri is null) return false;
        var path = uri.AbsolutePath.AsSpan();
        var lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0) return false;
        var lastSegment = path[(lastSlash + 1)..];
        return StartsWithActionVerb(lastSegment, "rotate")
            || StartsWithActionVerb(lastSegment, "regenerate")
            || StartsWithActionVerb(lastSegment, "refresh");
    }

    private static bool StartsWithActionVerb(ReadOnlySpan<char> segment, string verb)
    {
        // Domain-shaped segments (contain a dot) are never action verbs.
        if (segment.Contains('.')) return false;
        if (!segment.StartsWith(verb, StringComparison.OrdinalIgnoreCase)) return false;
        // Match either an exact-segment (e.g. "rotate") or a hyphen-suffix segment
        // (e.g. "rotate-dkim-key"). "rotations" or "refreshing" don't qualify.
        return segment.Length == verb.Length || segment[verb.Length] == '-';
    }

    private static TimeSpan ComputeDelay(HttpResponseMessage response, int attempt)
    {
        // Prefer Mailgun's X-RateLimit-Reset (Unix ms) when available.
        var rl = MailgunResponseMetadata.ParseRateLimit(response);
        if (rl?.Reset is { } reset)
        {
            var d = reset - DateTimeOffset.UtcNow;
            if (d > TimeSpan.Zero)
                return Clamp(d);
        }

        // Then standard Retry-After (some Mailgun edges still emit it).
        if (response.Headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta is { } delta && delta > TimeSpan.Zero)
                return Clamp(delta);
            if (retryAfter.Date is { } date)
            {
                var d = date - DateTimeOffset.UtcNow;
                if (d > TimeSpan.Zero)
                    return Clamp(d);
            }
        }

        // Fallback: exponential 2^attempt seconds with ±20 % jitter so a fleet of clients sharing the
        // same outage don't all retry on the exact same second (thundering-herd avoidance).
        var seconds = Math.Min(MaxBackoff.TotalSeconds, Math.Pow(2, attempt));
        var jitterFactor = 1.0 + ((Random.Shared.NextDouble() * 0.4) - 0.2);
        return TimeSpan.FromSeconds(Math.Max(0.0, seconds * jitterFactor));
    }

    private static TimeSpan Clamp(TimeSpan t) => t > MaxBackoff ? MaxBackoff : t;
}
