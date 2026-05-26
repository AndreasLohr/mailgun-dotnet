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
        // ThrowIfNullOrEmpty (not ThrowIfNull) — HMACSHA256 accepts a zero-length key and produces a
        // deterministic output that anyone can compute from public inputs, so an empty signing key
        // would let an attacker forge valid signatures. Fail loud at the API boundary instead.
        ArgumentException.ThrowIfNullOrEmpty(signingKey);
        ArgumentNullException.ThrowIfNull(timestamp);
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(signature);

        // UTF-8, not ASCII. Mailgun-issued signing keys / timestamps / tokens are all hex digits or
        // numeric strings — so every byte is identical to its ASCII form on the wire, no
        // compatibility risk. ASCII's default encoder is best-fit and silently maps any non-ASCII
        // code point to '?' (0x3F), so a signing key with a smart-quote / BOM / accidental non-ASCII
        // byte would collide with every other string that ASCII-encodes to the same '?'-laced bytes.
        // A crypto primitive must never lossy-encode its keying material.
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var data = Encoding.UTF8.GetBytes(timestamp + token);
        var hash = hmac.ComputeHash(data);

        // Mailgun's signature is lowercase hex.
        var expected = HexLower(hash);
        var actual = signature.Trim().ToLowerInvariant();

        // Comparison bytes are guaranteed hex (0-9, a-f) — ASCII and UTF-8 produce identical
        // single-byte sequences. Kept on UTF-8 for symmetry with the input-encoding above.
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
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
        // DateTimeOffset.FromUnixTimeSeconds throws ArgumentOutOfRangeException outside its supported
        // range (-62135596800..253402300799). A long-parsable but out-of-range value from a crafted
        // payload would otherwise propagate to a 500 instead of a clean 401. Range-check first.
        if (unixSeconds < UnixSecondsMin || unixSeconds > UnixSecondsMax)
            return false;
        var ts = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var current = now ?? DateTimeOffset.UtcNow;
        var age = current - ts;
        return age.Duration() <= maxAge;
    }

    // From DateTimeOffset.FromUnixTimeSeconds documentation: valid range is [year 0001, year 9999].
    private const long UnixSecondsMin = -62135596800L;
    private const long UnixSecondsMax = 253402300799L;

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
