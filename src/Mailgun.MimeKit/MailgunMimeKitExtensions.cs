using Mailgun.Models.Messages;
using Mailgun.Services;
using MimeKit;

namespace Mailgun.MimeKit;

/// <summary>
/// <see cref="MimeMessage"/> interop for <see cref="IMessagesService"/>. Provides a one-call path
/// from a fully-constructed MimeKit message to Mailgun's <c>POST /v3/{domain}/messages.mime</c>
/// endpoint — the only way to send S/MIME-signed mail, calendar-invite alternative parts, or any
/// other RFC-2822 shape the form-encoded <c>/messages</c> endpoint can't represent.
/// </summary>
public static class MailgunMimeKitExtensions
{
    /// <summary>
    /// Sends a <see cref="MimeMessage"/> via Mailgun's <c>POST /v3/{domain}/messages.mime</c> endpoint.
    /// Envelope recipients are derived from the message's <c>To</c>, <c>Cc</c> and <c>Bcc</c> headers
    /// (Mailgun treats the <c>to</c> form field as the envelope <c>RCPT TO</c> list — what actually
    /// gets delivered, separate from any <c>To:</c> headers the recipient sees).
    /// </summary>
    /// <param name="messages">The Mailgun messages service.</param>
    /// <param name="domain">The sending domain.</param>
    /// <param name="message">The MimeKit message to send.</param>
    /// <param name="testMode">
    /// When <c>true</c>, Mailgun queues the message but does not deliver it (Mailgun's <c>o:testmode</c> flag).
    /// </param>
    /// <param name="envelopeRecipients">
    /// Optional explicit envelope-recipient override. When <c>null</c> (the default) the recipients
    /// are derived from <see cref="MimeMessage.To"/> + <see cref="MimeMessage.Cc"/> + <see cref="MimeMessage.Bcc"/>.
    /// Pass an explicit list for the legacy-SMTP pattern of BCC'ing a recipient not present in headers,
    /// or for fan-out to addresses the message body doesn't enumerate.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Mailgun send response (message id + queue confirmation).</returns>
    public static async Task<SendMessageResponse> SendMimeAsync(
        this IMessagesService messages,
        string domain,
        MimeMessage message,
        bool? testMode = null,
        IReadOnlyList<string>? envelopeRecipients = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(message);

        var to = envelopeRecipients ?? ExtractEnvelopeRecipients(message);
        if (to.Count == 0)
            throw new ArgumentException(
                "MimeMessage has no To/Cc/Bcc recipients and no explicit envelopeRecipients were supplied.",
                nameof(message));

        // MimeMessage.WriteToAsync streams the RFC-2822 form to a Stream. Buffer to a byte[] so we
        // can hand off to the existing SendMimeAsync(byte[]) overload — which already handles the
        // multipart upload and Mailgun's `message/rfc822` content-type wrapping.
        using var ms = new MemoryStream();
        await message.WriteToAsync(ms, cancellationToken).ConfigureAwait(false);
        var bytes = ms.ToArray();

        return await messages.SendMimeAsync(domain, to, bytes, testMode, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pulls the envelope-recipient list from a <see cref="MimeMessage"/>'s To + Cc + Bcc headers,
    /// case-insensitively deduplicated while preserving first-seen order. Exposed as internal so the
    /// test suite can pin this contract without parsing multipart wire format.
    /// </summary>
    /// <remarks>
    /// Mailgun rejects exact-duplicate envelope recipients silently (second copy is a no-op), but
    /// downstream proxies / per-recipient billing layers can count duplicates against quota, so the
    /// SDK trims defensively before the wire.
    /// </remarks>
    internal static IReadOnlyList<string> ExtractEnvelopeRecipients(MimeMessage message)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        foreach (var addr in message.To.Mailboxes.Concat(message.Cc.Mailboxes).Concat(message.Bcc.Mailboxes))
        {
            if (seen.Add(addr.Address))
                ordered.Add(addr.Address);
        }
        return ordered;
    }
}
