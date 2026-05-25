using System.Net;

namespace Mailgun.Exceptions;

/// <summary>
/// Thrown when the Mailgun API returns a non-success HTTP status code.
/// </summary>
public class MailgunApiException : MailgunException
{
    /// <summary>The HTTP status code returned by Mailgun.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The parsed <c>message</c> field from the Mailgun error envelope, if present.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Additional error details (e.g. validation errors), when Mailgun returns a <c>details</c> or <c>errors</c> array.</summary>
    public IReadOnlyList<string> Details { get; }

    /// <summary>The <c>X-Mailgun-Request-Id</c> header value, if present.</summary>
    public string? RequestId { get; }

    /// <summary>Rate-limit headers captured from the response, if any.</summary>
    public RateLimitInfo? RateLimit { get; }

    /// <summary>The raw response body (may be useful for debugging unparseable errors).</summary>
    public string? RawResponseBody { get; }

    /// <summary>Initializes a new <see cref="MailgunApiException"/>.</summary>
    public MailgunApiException(
        HttpStatusCode statusCode,
        string? errorMessage,
        IReadOnlyList<string> details,
        string? requestId = null,
        RateLimitInfo? rateLimit = null,
        string? rawResponseBody = null,
        Exception? innerException = null)
        : base(BuildMessage(statusCode, errorMessage), innerException)
    {
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
        Details = details;
        RequestId = requestId;
        RateLimit = rateLimit;
        RawResponseBody = rawResponseBody;
    }

    private static string BuildMessage(HttpStatusCode status, string? errorMessage) =>
        string.IsNullOrEmpty(errorMessage)
            ? $"Mailgun API returned HTTP {(int)status} {status}."
            : $"Mailgun API returned HTTP {(int)status} {status}: {errorMessage}";
}
