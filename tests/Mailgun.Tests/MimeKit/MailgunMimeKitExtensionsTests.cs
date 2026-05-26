using System.Net;
using Mailgun.MimeKit;
using Mailgun.Tests.TestHelpers;
using MimeKit;

namespace Mailgun.Tests.MimeKit;

public class MailgunMimeKitExtensionsTests
{
    private static MimeMessage NewMimeMessage(
        string from = "sender@example.com",
        string subject = "subject",
        string textBody = "body")
    {
        var m = new MimeMessage();
        m.From.Add(new MailboxAddress("Sender", from));
        m.Subject = subject;
        m.Body = new TextPart("plain") { Text = textBody };
        return m;
    }

    [Fact]
    public async Task SendMimeAsync_posts_multipart_to_v3_domain_messages_mime()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"id\":\"<msg@mg.example.com>\",\"message\":\"Queued. Thank you.\"}");

        var message = NewMimeMessage();
        message.To.Add(new MailboxAddress("Alice", "alice@example.com"));

        var resp = await client.Messages.SendMimeAsync("mg.example.com", message);

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/mg.example.com/messages.mime", req.Uri.AbsolutePath);
        Assert.Equal("multipart/form-data", req.ContentType);
        Assert.NotNull(resp.Id);
    }

    [Fact]
    public void ExtractEnvelopeRecipients_pulls_To_Cc_and_Bcc_addresses_in_header_order()
    {
        // Pin the extractor contract directly — avoids the multipart-body parsing fragility that
        // an end-to-end wire-format assertion would have. Mailgun's envelope `to` is what actually
        // gets delivered (the RCPT TO list); deriving it from the message headers is the whole
        // point of this overload existing.
        var message = NewMimeMessage();
        message.To.Add(new MailboxAddress("Alice", "alice@example.com"));
        message.Cc.Add(new MailboxAddress("Bob", "bob@example.com"));
        message.Bcc.Add(new MailboxAddress("Carol", "carol@example.com"));

        var recipients = MailgunMimeKitExtensions.ExtractEnvelopeRecipients(message);

        Assert.Equal(new[] { "alice@example.com", "bob@example.com", "carol@example.com" }, recipients);
    }

    [Fact]
    public void ExtractEnvelopeRecipients_deduplicates_addresses_case_insensitively_first_seen_wins()
    {
        // Same mailbox in To AND Bcc, with case-only difference, must produce one entry.
        // Mailgun silently dedupes downstream, but per-recipient billing on a proxy can count
        // duplicates against quota — the SDK trims defensively before the wire.
        var message = NewMimeMessage();
        message.To.Add(new MailboxAddress("Alice", "alice@example.com"));
        message.Bcc.Add(new MailboxAddress("Alice (bcc)", "ALICE@example.com"));

        var recipients = MailgunMimeKitExtensions.ExtractEnvelopeRecipients(message);

        // First-seen wins: lowercase form from the To header survives.
        Assert.Equal(new[] { "alice@example.com" }, recipients);
    }

    [Fact]
    public void ExtractEnvelopeRecipients_returns_empty_for_message_with_no_recipients()
    {
        var message = NewMimeMessage();
        Assert.Empty(MailgunMimeKitExtensions.ExtractEnvelopeRecipients(message));
    }

    [Fact]
    public async Task SendMimeAsync_with_explicit_envelopeRecipients_uses_them_for_envelope()
    {
        // Legacy-SMTP pattern: BCC a recipient who shouldn't appear in any visible header (audit
        // copies, dark-launches, etc.). When envelopeRecipients is supplied, the extension MUST
        // pass that list to the underlying byte[] overload — not the header-derived one. Test by
        // observing the resulting HTTP request: if dedup-against-headers happens, the explicit
        // address would be discarded; here it must reach the wire even though the headers list a
        // completely different person.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"<m>\",\"message\":\"ok\"}");

        var message = NewMimeMessage();
        message.To.Add(new MailboxAddress("Alice", "alice@example.com"));

        await client.Messages.SendMimeAsync(
            "mg.example.com",
            message,
            envelopeRecipients: new[] { "audit@example.com" });

        var body = handler.Requests[0].Body!;
        // The envelope-override address must reach the wire (it's the envelope `to`).
        Assert.Contains("audit@example.com", body, StringComparison.Ordinal);
        // The MIME body itself still embeds the To: Alice header — that's correct (headers are
        // separate from envelope recipients in SMTP), so we don't assert alice's absence.
    }

    [Fact]
    public async Task SendMimeAsync_with_testMode_emits_o_testmode_field()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"<m>\",\"message\":\"ok\"}");

        var message = NewMimeMessage();
        message.To.Add(new MailboxAddress("Alice", "alice@example.com"));

        await client.Messages.SendMimeAsync("mg.example.com", message, testMode: true);

        Assert.Contains("o:testmode", handler.Requests[0].Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendMimeAsync_throws_when_message_has_no_recipients_and_no_override()
    {
        var (client, _) = TestMailgunClient.Create();

        var message = NewMimeMessage();
        // No To, no Cc, no Bcc, no override → must throw.

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Messages.SendMimeAsync("mg.example.com", message));
        Assert.Contains("recipient", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMimeAsync_serialises_message_as_rfc822_with_body_on_wire()
    {
        // The full MIME serialisation reaches the wire — verify the body text is in the upload.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"<m>\",\"message\":\"ok\"}");

        var message = NewMimeMessage(subject: "S/MIME-ready subject", textBody: "DISTINCTIVE-BODY-MARKER");
        message.To.Add(new MailboxAddress("Alice", "alice@example.com"));

        await client.Messages.SendMimeAsync("mg.example.com", message);

        Assert.Contains("DISTINCTIVE-BODY-MARKER", handler.Requests[0].Body!, StringComparison.Ordinal);
        Assert.Contains("S/MIME-ready subject", handler.Requests[0].Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendMimeAsync_rejects_blank_domain()
    {
        var (client, _) = TestMailgunClient.Create();
        var message = NewMimeMessage();
        message.To.Add(new MailboxAddress("Alice", "alice@example.com"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Messages.SendMimeAsync("", message));
    }

    [Fact]
    public async Task SendMimeAsync_rejects_null_message()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.Messages.SendMimeAsync("mg.example.com", (MimeMessage)null!));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
