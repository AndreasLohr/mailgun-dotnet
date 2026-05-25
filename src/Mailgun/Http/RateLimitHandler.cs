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

            if (!ShouldRetry(response.StatusCode, request.Method) || attempt >= _maxRetries)
            {
                return response;
            }

            var delay = ComputeDelay(response, attempt);
            response.Dispose();
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            attempt++;
        }
    }

    private static bool ShouldRetry(HttpStatusCode status, HttpMethod method)
    {
        if (status == HttpStatusCode.TooManyRequests)
            return true;
        if ((int)status >= 500)
            return IsIdempotent(method);
        return false;
    }

    private static bool IsIdempotent(HttpMethod method) =>
        method == HttpMethod.Get
        || method == HttpMethod.Head
        || method == HttpMethod.Put
        || method == HttpMethod.Delete
        || method == HttpMethod.Options;

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

        // Fallback: exponential 2^attempt seconds.
        var seconds = Math.Min(MaxBackoff.TotalSeconds, Math.Pow(2, attempt));
        return TimeSpan.FromSeconds(seconds);
    }

    private static TimeSpan Clamp(TimeSpan t) => t > MaxBackoff ? MaxBackoff : t;
}
