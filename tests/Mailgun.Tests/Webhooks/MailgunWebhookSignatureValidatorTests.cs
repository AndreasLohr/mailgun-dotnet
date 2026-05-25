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
