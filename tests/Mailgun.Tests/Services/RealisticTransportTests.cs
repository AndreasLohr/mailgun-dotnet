using System.Net;
using Mailgun.Models.Messages;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

/// <summary>
/// A small set of end-to-end-shaped tests that exercise the upload paths through a
/// <see cref="MockHttpMessageHandler"/> with <c>YieldBeforeReadingBody</c> = true. This is the
/// cheap "integration-style" coverage the field-level review asked for: every other test in the
/// suite uses the default synchronous-body-read mode, which captures bytes before any caller-side
/// disposal can fire — masking timing bugs like the multipart use-after-dispose race. With the
/// yield enabled, the handler is on the same side of the race as a real <c>HttpClientHandler</c>,
/// so a future regression to the buggy <c>using var mp = ...; return _http.Post(...)</c> pattern
/// would surface here as either an empty body or an <c>ObjectDisposedException</c> / collection-
/// modified error from the still-disposed <c>MultipartFormDataContent</c>.
/// </summary>
public class RealisticTransportTests
{
    private static (MailgunClient client, MockHttpMessageHandler handler) CreateWithYield()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.YieldBeforeReadingBody = true;
        return (client, handler);
    }

    [Fact]
    public async Task Message_with_attachment_survives_a_yielding_transport()
    {
        var (client, handler) = CreateWithYield();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"<x>\",\"message\":\"queued\"}");

        await client.Messages.SendAsync("mg.example.com", new SendMessageRequest
        {
            From = "x@mg.example.com",
            To = { "alice@example.com" },
            Subject = "s",
            Text = "t",
            Attachments = { new MessageAttachment("hello.txt", System.Text.Encoding.UTF8.GetBytes("hi there"), "text/plain") },
        });

        var req = Assert.Single(handler.Requests);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
        Assert.Contains("hello.txt", req.Body!, StringComparison.Ordinal);
        Assert.Contains("hi there", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bounces_ImportCsv_survives_a_yielding_transport()
    {
        var (client, handler) = CreateWithYield();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"queued\"}");

        const string csv = "address,code\nalice@example.com,550\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        await client.Suppressions.Bounces.ImportCsvAsync("mg.example.com", stream);

        var req = Assert.Single(handler.Requests);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
        Assert.Contains(csv, req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailingLists_BulkAddMembersCsv_survives_a_yielding_transport()
    {
        var (client, handler) = CreateWithYield();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"queued\"}");

        const string csv = "address,name\nalice@example.com,Alice\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        await client.MailingLists.BulkAddMembersCsvAsync("list@y", stream, upsert: true);

        var req = Assert.Single(handler.Requests);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
        Assert.Contains(csv, req.Body!, StringComparison.Ordinal);
        Assert.Contains("upsert", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Form_encoded_send_also_survives_a_yielding_transport()
    {
        // The yielding handler is a regression net for the entire HTTP path, not just multipart.
        // Form-encoded POSTs use FormUrlEncodedContent which is built around an immutable byte
        // array — but if the SDK ever switches to a stream-backed form encoder, this test would
        // catch a similar disposal-race.
        var (client, handler) = CreateWithYield();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"<x>\",\"message\":\"queued\"}");

        await client.Messages.SendAsync("mg.example.com", new SendMessageRequest
        {
            From = "x@mg.example.com",
            To = { "alice@example.com" },
            Subject = "s",
            Text = "t",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal("application/x-www-form-urlencoded", req.ContentType);
        Assert.Contains("from=x%40mg.example.com", req.Body!, StringComparison.Ordinal);
    }
}
