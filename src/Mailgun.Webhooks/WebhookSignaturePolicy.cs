namespace Mailgun.Webhooks;

/// <summary>
/// Selects which signature field(s) the SDK verifies against the configured signing key when a
/// Mailgun webhook payload may carry both a child <c>signature</c> (signed with the originating
/// domain's key) and a <c>parent-signature</c> (signed with the parent account's key, present only
/// for subaccount-domain events).
/// </summary>
/// <remarks>
/// All variants are equally safe — each still requires a valid HMAC produced with the configured
/// signing key, so none of them weakens forgery resistance. They differ only in <em>which</em>
/// signing key the receiver is expected to hold.
/// </remarks>
public enum WebhookSignaturePolicy
{
    /// <summary>
    /// Accept the webhook when the configured key verifies <strong>either</strong> the child
    /// <c>signature</c> or the <c>parent-signature</c>. This is the recommended default: it accepts a
    /// standalone account's own webhooks (child signature) and, for a parent account configured with
    /// its parent signing key, every subaccount's webhooks (parent signature) — with no behavioral
    /// change for non-subaccount payloads, which carry no parent signature.
    /// </summary>
    AcceptEither = 0,

    /// <summary>
    /// Verify only the child <c>signature</c> field (signed with the originating domain's own signing
    /// key). Use when the configured key is the exact domain/subaccount key and you want to reject a
    /// payload that only carries a parent signature.
    /// </summary>
    ChildSignatureOnly = 1,

    /// <summary>
    /// Verify only the <c>parent-signature</c> field. Use when the configured key is the parent
    /// account's signing key and you exclusively receive subaccount webhooks; a payload without a
    /// parent signature is rejected.
    /// </summary>
    ParentSignatureOnly = 2,
}
