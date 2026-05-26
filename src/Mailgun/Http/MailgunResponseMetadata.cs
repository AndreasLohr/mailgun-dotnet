using System.Globalization;
using System.Net;
using Mailgun.Exceptions;

namespace Mailgun.Http;

/// <summary>
/// Metadata about the most recent HTTP response from Mailgun (status, request id, rate-limit headers).
/// Available via <see cref="MailgunClient.LastResponseMetadata"/>.
/// </summary>
public sealed class MailgunResponseMetadata
{
    /// <summary>HTTP status code returned by Mailgun.</summary>
    public HttpStatusCode StatusCode { get; init; }

    /// <summary>Value of the <c>X-Mailgun-Request-Id</c> header, if present.</summary>
    public string? RequestId { get; init; }

    /// <summary>Rate-limit information parsed from the response headers, if any.</summary>
    public RateLimitInfo? RateLimit { get; init; }

    /// <summary>Convenience accessor for <see cref="Exceptions.RateLimitInfo.Remaining"/>.</summary>
    public int? RateLimitRemaining => RateLimit?.Remaining;

    /// <summary>Convenience accessor for <see cref="Exceptions.RateLimitInfo.Limit"/>.</summary>
    public int? RateLimitLimit => RateLimit?.Limit;

    /// <summary>Convenience accessor for <see cref="Exceptions.RateLimitInfo.Reset"/>.</summary>
    public DateTimeOffset? RateLimitReset => RateLimit?.Reset;

    internal static MailgunResponseMetadata FromHttpResponse(HttpResponseMessage response)
    {
        return new MailgunResponseMetadata
        {
            StatusCode = response.StatusCode,
            RequestId = GetHeader(response, "X-Mailgun-Request-Id") ?? GetHeader(response, "X-Request-Id"),
            RateLimit = ParseRateLimit(response),
        };
    }

    internal static RateLimitInfo? ParseRateLimit(HttpResponseMessage response)
    {
        var limit = TryParseInt(GetHeader(response, "X-RateLimit-Limit"));
        var remaining = TryParseInt(GetHeader(response, "X-RateLimit-Remaining"));
        var reset = TryParseUnixMillis(GetHeader(response, "X-RateLimit-Reset"));
        if (limit is null && remaining is null && reset is null)
            return null;
        return new RateLimitInfo(limit, remaining, reset);
    }

    private static string? GetHeader(HttpResponseMessage r, string name) =>
        r.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static int? TryParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static DateTimeOffset? TryParseUnixMillis(string? value)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
            return null;
        // Mailgun documents X-RateLimit-Reset as Unix milliseconds. Guard against seconds-form by
        // checking magnitude (anything < year 2001 in millis is almost certainly seconds).
        // DateTimeOffset.FromUnixTime{Seconds,Milliseconds} both throw ArgumentOutOfRangeException
        // for values outside year 0001..9999, so a malformed-but-long-parseable header (e.g.
        // X-RateLimit-Reset: 99999999999999) would otherwise crash the metadata-parse path. A
        // TryParse-named function must not throw; swallow + null.
        try
        {
            return ms < 1_000_000_000_000L
                ? DateTimeOffset.FromUnixTimeSeconds(ms)
                : DateTimeOffset.FromUnixTimeMilliseconds(ms);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
