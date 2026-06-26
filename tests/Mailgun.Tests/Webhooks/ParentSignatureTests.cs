using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mailgun.Webhooks;

namespace Mailgun.Tests.Webhooks;

/// <summary>
/// Tests for Mailgun subaccount <c>parent-signature</c> verification. A subaccount-domain event
/// carries both a child <c>signature</c> (signed with the subaccount's key) and a
/// <c>parent-signature</c> (signed with the parent account's key) over the SAME timestamp||token
/// message, so a parent account can verify all of its subaccounts' webhooks with one key.
/// </summary>
public class ParentSignatureTests
{
    private const string SubaccountKey = "subaccount-key-aaa";
    private const string ParentKey = "parent-key-bbb";

    // ── Parser surfaces parent-signature ──────────────────────────────────────────────────────

    [Fact]
    public void Parser_extracts_parent_signature_when_present()
    {
        var (ts, token, childSig) = Sign(SubaccountKey);
        var parentSig = SignWith(ParentKey, ts, token);
        var json = Envelope(ts, token, childSig, parentSig);

        var evt = MailgunWebhookParser.Parse(json);

        Assert.NotNull(evt.Signature);
        Assert.Equal(childSig, evt.Signature!.Signature);
        Assert.Equal(parentSig, evt.Signature.ParentSignature);
    }

    [Fact]
    public void Parser_leaves_parent_signature_null_when_absent()
    {
        var (ts, token, childSig) = Sign(SubaccountKey);
        var json = Envelope(ts, token, childSig, parentSignature: null);

        var evt = MailgunWebhookParser.Parse(json);

        Assert.NotNull(evt.Signature);
        Assert.Null(evt.Signature!.ParentSignature);
    }

    [Fact]
    public void TryExtractSignature_populates_parent_signature()
    {
        var (ts, token, childSig) = Sign(SubaccountKey);
        var parentSig = SignWith(ParentKey, ts, token);
        var bytes = Encoding.UTF8.GetBytes(Envelope(ts, token, childSig, parentSig));

        Assert.True(MailgunWebhookParser.TryExtractSignature(bytes.AsMemory(), out var sig));
        Assert.Equal(parentSig, sig.ParentSignature);
    }

    [Fact]
    public void TryExtractSignature_parent_null_when_absent()
    {
        var (ts, token, childSig) = Sign(SubaccountKey);
        var bytes = Encoding.UTF8.GetBytes(Envelope(ts, token, childSig, parentSignature: null));

        Assert.True(MailgunWebhookParser.TryExtractSignature(bytes.AsMemory(), out var sig));
        Assert.Null(sig.ParentSignature);
    }

    // ── Policy behavior: only the child signature present ─────────────────────────────────────

    [Fact]
    public void Non_subaccount_payload_passes_AcceptEither_and_ChildOnly_but_not_ParentOnly()
    {
        var (ts, token, childSig) = Sign(SubaccountKey, secondsAgo: 5);

        Assert.True(Verify(SubaccountKey, ts, token, childSig, null, WebhookSignaturePolicy.AcceptEither));
        Assert.True(Verify(SubaccountKey, ts, token, childSig, null, WebhookSignaturePolicy.ChildSignatureOnly));
        // No parent signature present → ParentSignatureOnly must reject.
        Assert.False(Verify(SubaccountKey, ts, token, childSig, null, WebhookSignaturePolicy.ParentSignatureOnly));
    }

    // ── Policy behavior: subaccount payload with both signatures ──────────────────────────────

    [Fact]
    public void Parent_account_verifies_subaccount_webhook_via_parent_signature()
    {
        var (ts, token, childSig) = Sign(SubaccountKey, secondsAgo: 5);
        var parentSig = SignWith(ParentKey, ts, token);

        // Receiver holds the PARENT key. The child signature won't match it, but the parent one does.
        Assert.True(Verify(ParentKey, ts, token, childSig, parentSig, WebhookSignaturePolicy.AcceptEither));
        Assert.True(Verify(ParentKey, ts, token, childSig, parentSig, WebhookSignaturePolicy.ParentSignatureOnly));
        Assert.False(Verify(ParentKey, ts, token, childSig, parentSig, WebhookSignaturePolicy.ChildSignatureOnly));
    }

    [Fact]
    public void Subaccount_key_verifies_its_own_child_signature_with_both_present()
    {
        var (ts, token, childSig) = Sign(SubaccountKey, secondsAgo: 5);
        var parentSig = SignWith(ParentKey, ts, token);

        // Receiver holds the SUBACCOUNT key. The child signature matches; the parent one does not.
        Assert.True(Verify(SubaccountKey, ts, token, childSig, parentSig, WebhookSignaturePolicy.AcceptEither));
        Assert.True(Verify(SubaccountKey, ts, token, childSig, parentSig, WebhookSignaturePolicy.ChildSignatureOnly));
        Assert.False(Verify(SubaccountKey, ts, token, childSig, parentSig, WebhookSignaturePolicy.ParentSignatureOnly));
    }

    [Fact]
    public void Unrelated_key_fails_every_policy_even_with_both_signatures()
    {
        var (ts, token, childSig) = Sign(SubaccountKey, secondsAgo: 5);
        var parentSig = SignWith(ParentKey, ts, token);

        foreach (var policy in new[]
                 {
                     WebhookSignaturePolicy.AcceptEither,
                     WebhookSignaturePolicy.ChildSignatureOnly,
                     WebhookSignaturePolicy.ParentSignatureOnly,
                 })
        {
            Assert.False(Verify("totally-unrelated-key", ts, token, childSig, parentSig, policy));
        }
    }

    [Fact]
    public void Tampered_parent_signature_fails_parent_policy()
    {
        var (ts, token, childSig) = Sign(SubaccountKey, secondsAgo: 5);
        var parentSig = SignWith(ParentKey, ts, token);
        var tampered = parentSig[..^1] + (parentSig[^1] == 'a' ? 'b' : 'a');

        Assert.False(Verify(ParentKey, ts, token, childSig, tampered, WebhookSignaturePolicy.ParentSignatureOnly));
    }

    [Fact]
    public void Freshness_still_enforced_on_policy_overload()
    {
        var (ts, token, childSig) = Sign(SubaccountKey, secondsAgo: 60 * 60); // 1 hour old
        var parentSig = SignWith(ParentKey, ts, token);

        Assert.False(MailgunWebhookSignatureValidator.IsValid(
            ParentKey, ts, token, childSig, parentSig,
            WebhookSignaturePolicy.AcceptEither, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void AcceptEither_default_matches_legacy_child_only_behavior_for_non_subaccount()
    {
        // The legacy IsValid(key, ts, token, sig, maxAge) and the new AcceptEither overload must
        // agree for a payload that carries no parent signature — proving backward compatibility.
        var (ts, token, childSig) = Sign(SubaccountKey, secondsAgo: 30);

        var legacy = MailgunWebhookSignatureValidator.IsValid(
            SubaccountKey, ts, token, childSig, TimeSpan.FromMinutes(15));
        var modern = MailgunWebhookSignatureValidator.IsValid(
            SubaccountKey, ts, token, childSig, parentSignature: null,
            WebhookSignaturePolicy.AcceptEither, TimeSpan.FromMinutes(15));

        Assert.True(legacy);
        Assert.Equal(legacy, modern);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────

    private static bool Verify(string key, string ts, string token, string sig, string? parentSig, WebhookSignaturePolicy policy) =>
        MailgunWebhookSignatureValidator.IsValid(key, ts, token, sig, parentSig, policy, TimeSpan.FromMinutes(15));

    private static (string Timestamp, string Token, string Signature) Sign(string key, int secondsAgo = 5)
    {
        var ts = DateTimeOffset.UtcNow.AddSeconds(-secondsAgo).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var token = "demo-token-9876";
        return (ts, token, SignWith(key, ts, token));
    }

    private static string SignWith(string key, string ts, string token)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(ts + token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Envelope(string ts, string token, string signature, string? parentSignature)
    {
        var parentLine = parentSignature is null ? "" : $",\"parent-signature\":\"{parentSignature}\"";
        return $$"""
        {
          "signature": { "timestamp": "{{ts}}", "token": "{{token}}", "signature": "{{signature}}"{{parentLine}} },
          "event-data": { "event": "delivered", "recipient": "a@x.com" }
        }
        """;
    }
}
