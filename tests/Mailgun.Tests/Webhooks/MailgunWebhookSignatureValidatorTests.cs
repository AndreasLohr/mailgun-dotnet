using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mailgun.Webhooks;

namespace Mailgun.Tests.Webhooks;

public class MailgunWebhookSignatureValidatorTests
{
    private const string SigningKey = "key-fake-12345";

    [Fact]
    public void Valid_signature_passes()
    {
        var (ts, token, sig) = ComputeSignature(SigningKey, secondsAgo: 10);
        Assert.True(MailgunWebhookSignatureValidator.IsValid(SigningKey, ts, token, sig));
    }

    [Fact]
    public void Tampered_token_fails()
    {
        var (ts, token, sig) = ComputeSignature(SigningKey, secondsAgo: 10);
        Assert.False(MailgunWebhookSignatureValidator.IsValid(SigningKey, ts, token + "x", sig));
    }

    [Fact]
    public void Tampered_signature_fails()
    {
        var (ts, token, sig) = ComputeSignature(SigningKey, secondsAgo: 10);
        var tampered = sig[..^1] + (sig[^1] == 'a' ? 'b' : 'a');
        Assert.False(MailgunWebhookSignatureValidator.IsValid(SigningKey, ts, token, tampered));
    }

    [Fact]
    public void Wrong_key_fails()
    {
        var (ts, token, sig) = ComputeSignature(SigningKey, secondsAgo: 10);
        Assert.False(MailgunWebhookSignatureValidator.IsValid("key-different", ts, token, sig));
    }

    [Fact]
    public void Old_timestamp_outside_max_age_fails()
    {
        var (ts, token, sig) = ComputeSignature(SigningKey, secondsAgo: 60 * 60); // 1 hour
        Assert.False(MailgunWebhookSignatureValidator.IsValid(SigningKey, ts, token, sig, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Recent_timestamp_within_max_age_passes()
    {
        var (ts, token, sig) = ComputeSignature(SigningKey, secondsAgo: 60);
        Assert.True(MailgunWebhookSignatureValidator.IsValid(SigningKey, ts, token, sig, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Empty_signing_key_throws_instead_of_silently_validating()
    {
        // Regression: HMACSHA256 accepts a zero-length key. Previously the validator's
        // ArgumentNullException.ThrowIfNull check let "" through, which would have computed a
        // deterministic HMAC anyone could forge from the public timestamp+token. The validator
        // must reject the empty signing key explicitly.
        Assert.Throws<ArgumentException>(() =>
            MailgunWebhookSignatureValidator.IsValid("", "1", "tok", "sig"));
    }

    [Fact]
    public void Out_of_range_timestamp_returns_false_does_not_throw()
    {
        // Regression: DateTimeOffset.FromUnixTimeSeconds throws ArgumentOutOfRangeException for
        // values outside year 0001..9999. A crafted payload with a long-parseable but out-of-range
        // timestamp used to propagate the exception to a 500. Must return false instead.
        var (_, token, sig) = ComputeSignature(SigningKey, secondsAgo: 0);
        var futureGarbage = "99999999999999"; // ~year 5168700, outside DateTimeOffset's range
        Assert.False(MailgunWebhookSignatureValidator.IsValid(
            SigningKey, futureGarbage, token, sig, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Distinct_non_ascii_signing_keys_no_longer_collide_to_question_mark()
    {
        // Regression: previously the validator did Encoding.ASCII.GetBytes(signingKey), whose
        // best-fit fallback silently rewrote every non-ASCII code point to '?' (0x3F). That made
        // "key-é", "key-ñ", "key-中", and the literal "key-?" all HMAC-equivalent. A signature
        // computed for "key-?" would validate against any of the others — a real, if narrow,
        // crypto-correctness bug. With UTF-8 encoding each of these is byte-distinct and produces
        // a different HMAC.
        const string timestamp = "1700000000";
        const string token = "tok-abc-123";

        // Compute the signature the way Mailgun's server would, using the literal "key-?" key.
        using var hmacAscii = new HMACSHA256(Encoding.UTF8.GetBytes("key-?"));
        var sigForQuestionMarkKey = Convert.ToHexString(
            hmacAscii.ComputeHash(Encoding.UTF8.GetBytes(timestamp + token))).ToLowerInvariant();

        // The literal "key-?" still validates — happy path.
        Assert.True(MailgunWebhookSignatureValidator.IsValid("key-?", timestamp, token, sigForQuestionMarkKey));

        // Each of the previously-colliding Unicode keys must now FAIL validation.
        Assert.False(MailgunWebhookSignatureValidator.IsValid("key-é", timestamp, token, sigForQuestionMarkKey));
        Assert.False(MailgunWebhookSignatureValidator.IsValid("key-ñ", timestamp, token, sigForQuestionMarkKey));
        Assert.False(MailgunWebhookSignatureValidator.IsValid("key-中", timestamp, token, sigForQuestionMarkKey));
    }

    [Fact]
    public void Non_ascii_token_no_longer_collides_to_question_mark()
    {
        // Same hazard as the key-collision test above, but applied to the per-webhook token field.
        const string signingKey = "key-fake-12345";
        const string timestamp = "1700000000";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var sigForQuestionMarkToken = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp + "tok-?"))).ToLowerInvariant();

        Assert.True(MailgunWebhookSignatureValidator.IsValid(signingKey, timestamp, "tok-?", sigForQuestionMarkToken));
        Assert.False(MailgunWebhookSignatureValidator.IsValid(signingKey, timestamp, "tok-é", sigForQuestionMarkToken));
        Assert.False(MailgunWebhookSignatureValidator.IsValid(signingKey, timestamp, "tok-中", sigForQuestionMarkToken));
    }

    [Fact]
    public void Signature_of_wrong_length_returns_false_without_throwing()
    {
        // The fixed-time comparison short-circuits on a length mismatch before FixedTimeEquals
        // (which requires equal-length spans). A truncated or oversized signature must cleanly
        // return false, never throw.
        var (ts, token, _) = ComputeSignature(SigningKey, secondsAgo: 5);
        Assert.False(MailgunWebhookSignatureValidator.IsValid(SigningKey, ts, token, "deadbeef")); // too short
        Assert.False(MailgunWebhookSignatureValidator.IsValid(SigningKey, ts, token, new string('a', 128))); // too long
        Assert.False(MailgunWebhookSignatureValidator.IsValid(SigningKey, ts, token, "")); // empty
    }

    [Fact]
    public void Signature_comparison_is_case_and_whitespace_insensitive()
    {
        // Mailgun emits lowercase hex; the validator trims + lowercases the candidate before the
        // fixed-time compare, so an upper-cased or padded signature still verifies.
        var (ts, token, sig) = ComputeSignature(SigningKey, secondsAgo: 5);
        Assert.True(MailgunWebhookSignatureValidator.IsValid(SigningKey, ts, token, "  " + sig.ToUpperInvariant() + "  "));
    }

    private static (string Timestamp, string Token, string Signature) ComputeSignature(string key, int secondsAgo)
    {
        var ts = DateTimeOffset.UtcNow.AddSeconds(-secondsAgo).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var token = "demo-token-1234";
        using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.ASCII.GetBytes(ts + token));
        var sig = Convert.ToHexString(hash).ToLowerInvariant();
        return (ts, token, sig);
    }
}
