using System.Net;

namespace Mailgun.Exceptions;

/// <summary>
/// Thrown when Mailgun returns HTTP 429 Too Many Requests after the SDK exhausts its retry budget.
/// </summary>
public sealed class MailgunRateLimitException : MailgunApiException
{
    /// <summary>Initializes a new <see cref="MailgunRateLimitException"/>.</summary>
    public MailgunRateLimitException(
        string? errorMessage,
        IReadOnlyList<string> details,
        string? requestId = null,
        RateLimitInfo? rateLimit = null,
        string? rawResponseBody = null)
        : base(HttpStatusCode.TooManyRequests, errorMessage, details, requestId, rateLimit, rawResponseBody) { }
}
