using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Mailgun.Webhooks;

/// <summary>
/// Verifies Mailgun webhook signatures per the documented algorithm:
/// <c>signature == HMAC-SHA256(signing_key, timestamp || token)</c> as a lowercase hex digest.
/// Also offers an end-to-end check that additionally enforces a maximum clock skew.
/// </summary>
public static class MailgunWebhookSignatureValidator
{
    /// <summary>The default permitted clock-skew window. Mailgun recommends being lenient because of delivery delays.</summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Verifies a Mailgun webhook signature without time-window enforcement. Use the overload
    /// that takes a <c>maxAge</c> argument to also reject stale signatures.
    /// </summary>
    public static bool IsValid(string signingKey, string timestamp, string token, string signature)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        ArgumentNullException.ThrowIfNull(timestamp);
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(signature);

        using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(signingKey));
        var data = Encoding.ASCII.GetBytes(timestamp + token);
        var hash = hmac.ComputeHash(data);

        // Mailgun's signature is lowercase hex.
        var expected = HexLower(hash);
        var actual = signature.Trim().ToLowerInvariant();

        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var actualBytes = Encoding.ASCII.GetBytes(actual);
        if (expectedBytes.Length != actualBytes.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    /// <summary>
    /// End-to-end webhook verification: HMAC check + timestamp freshness check.
    /// Rejects signatures whose timestamp is older than <paramref name="maxAge"/> (default 15 minutes).
    /// </summary>
    public static bool IsValid(
        string signingKey,
        string timestamp,
        string token,
        string signature,
        TimeSpan maxAge,
        DateTimeOffset? now = null)
    {
        if (!IsValid(signingKey, timestamp, token, signature))
            return false;
        if (!long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
            return false;
        var ts = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var current = now ?? DateTimeOffset.UtcNow;
        var age = current - ts;
        return age.Duration() <= maxAge;
    }

    private static string HexLower(byte[] data)
    {
#if NET8_0_OR_GREATER
        return Convert.ToHexString(data).ToLowerInvariant();
#else
        var sb = new StringBuilder(data.Length * 2);
        foreach (var b in data) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
#endif
    }
}
