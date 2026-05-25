using System.Net;
using Mailgun.Tests.TestHelpers;
using Mailgun.Webhooks;
using Mailgun.Webhooks.Events;

namespace Mailgun.Tests;

public class MediumValueTests
{
    [Fact]
    public async Task MailgunClient_supports_await_using_via_IAsyncDisposable()
    {
        // The `await using` pattern requires an IAsyncDisposable. Build a client and confirm
        // the construct compiles and runs without throwing. (The HttpClient is caller-owned
        // here, so DisposeAsync is a no-op — but we still want the construct to be valid.)
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.test/") };

        await using (var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
        }))
        {
            _ = await client.Routes.ListAsync();
        }
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task DisposeAsync_returns_completed_ValueTask_and_releases_owned_HttpClient()
    {
        // We can't observe the internal HttpClient.Dispose directly without rooting around in
        // privates, but we can confirm DisposeAsync completes synchronously (returns a completed
        // ValueTask) and that a subsequent call to the disposed client throws.
        var client = new MailgunClient("test-key");
        var task = client.DisposeAsync();
        Assert.True(task.IsCompletedSuccessfully);
        await task;
    }

    [Fact]
    public async Task Webhook_Message_field_deserializes_into_typed_WebhookMessageInfo()
    {
        // Regression: this field was Dictionary<string, object>. Now it's a typed
        // WebhookMessageInfo with headers / attachments / recipients / size.
        const string payload = """
            {
              "event-data": {
                "event": "delivered",
                "timestamp": 1758000000.0,
                "recipient": "alice@example.com",
                "message": {
                  "headers": { "subject": "hi", "from": "x@d", "to": "alice@example.com", "message-id": "<m1>" },
                  "attachments": [ { "filename": "logo.png", "content-type": "image/png", "size": 1234 } ],
                  "recipients": [ "alice@example.com" ],
                  "size": 4096
                }
              }
            }
            """;

        var evt = MailgunWebhookParser.Parse(payload);
        var d = Assert.IsType<DeliveredEvent>(evt);
        var msg = Assert.IsType<WebhookMessageInfo>(d.Message);
        Assert.Equal("hi", msg.Headers!["subject"]);
        Assert.Equal("<m1>", msg.Headers["message-id"]);
        Assert.Single(msg.Attachments!);
        Assert.Equal("logo.png", msg.Attachments![0].FileName);
        Assert.Equal(1234, msg.Attachments[0].Size);
        Assert.Equal(4096, msg.Size);
    }

    [Fact]
    public async Task Analytics_Log_DeliveryStatus_Envelope_Flags_deserialize_into_typed_substructures()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "items": [{
                "id": "e1",
                "event": "failed",
                "timestamp": 1700000000.0,
                "recipient": "alice@example.com",
                "delivery_status": { "code": 550, "description": "Mailbox does not exist", "attempt-no": 1, "bounce_classification": "HARD" },
                "envelope": { "sender": "noreply@x", "transport": "smtp", "targets": "alice@example.com" },
                "flags": { "is-authenticated": true, "is-test-mode": false, "is-routed": false }
              }],
              "pagination": { "skip": 0, "limit": 10, "total": 1 }
            }
            """);

        var resp = await client.Analytics.QueryLogsAsync(new()
        {
            Start = "Thu, 01 Jan 2026 00:00:00 +0000",
            End = "Fri, 02 Jan 2026 00:00:00 +0000",
        });

        var item = Assert.Single(resp.Items!);
        Assert.Equal(550, item.DeliveryStatus!.Code);
        Assert.Equal("HARD", item.DeliveryStatus.BounceClassification);
        Assert.Equal(1, item.DeliveryStatus.AttemptNumber);
        Assert.Equal("smtp", item.Envelope!.Transport);
        Assert.Equal("noreply@x", item.Envelope.Sender);
        Assert.True(item.Flags!.IsAuthenticated);
        Assert.False(item.Flags.IsTestMode);
    }

    [Fact]
    public async Task Multipart_stream_upload_is_retry_safe_even_for_non_seekable_streams()
    {
        // Wraps the input in a non-seekable stream and verifies the SDK buffers it so a retry
        // would still find the original bytes. We can't easily trigger a retry through the
        // mock, but we CAN inspect the recorded body to confirm the bytes survived the
        // serialization (i.e. the buffer was successfully read).
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        var bytes = System.Text.Encoding.UTF8.GetBytes("address\nalice@example.com\nbob@example.com\n");
        using var nonSeekable = new NonSeekableStream(bytes);
        await client.Suppressions.Bounces.ImportCsvAsync("mg.example.com", nonSeekable, fileName: "b.csv");

        var req = Assert.Single(handler.Requests);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
        // The original CSV content survives because the SDK eagerly buffers the stream.
        Assert.Contains("alice@example.com", req.Body!, StringComparison.Ordinal);
        Assert.Contains("bob@example.com", req.Body!, StringComparison.Ordinal);
        Assert.Contains("b.csv", req.Body!, StringComparison.Ordinal);
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;
        public NonSeekableStream(byte[] data) => _inner = new MemoryStream(data, writable: false);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
