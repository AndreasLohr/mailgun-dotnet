namespace Mailgun.Exceptions;

/// <summary>
/// Mailgun rate-limit headers as captured from a single HTTP response.
/// </summary>
/// <param name="Limit">Value of the <c>X-RateLimit-Limit</c> header, if present.</param>
/// <param name="Remaining">Value of the <c>X-RateLimit-Remaining</c> header, if present.</param>
/// <param name="Reset">Value of the <c>X-RateLimit-Reset</c> header (Unix milliseconds) parsed as a <see cref="DateTimeOffset"/>, if present.</param>
public sealed record RateLimitInfo(int? Limit, int? Remaining, DateTimeOffset? Reset);
