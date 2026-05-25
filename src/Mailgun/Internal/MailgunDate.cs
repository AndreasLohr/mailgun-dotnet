using System.Globalization;

namespace Mailgun.Internal;

/// <summary>
/// Centralized formatter for Mailgun-bound RFC-2822 timestamps.
/// </summary>
/// <remarks>
/// .NET's <c>"r"</c> standard format string emits RFC-1123 with the textual zone <c>GMT</c>
/// (e.g. <c>Mon, 18 May 2026 17:31:27 GMT</c>). RFC 2822 §3.3 prefers a NUMERIC offset
/// (<c>-0000</c> or <c>+0000</c> for unknown/UTC) and Mailgun's <c>/v1/analytics/logs</c> endpoint
/// rejects <c>GMT</c> outright as "Invalid format for parameter start". This helper emits the
/// strict numeric-offset form so every Mailgun-bound timestamp passes the strictest parser on
/// their API. The literal <c>-0000</c> follows Mailgun's documented examples.
/// </remarks>
internal static class MailgunDate
{
    public static string FormatRfc2822(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss '-0000'", CultureInfo.InvariantCulture);
}
