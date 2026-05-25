using System.Net;
using Mailgun.Models.Messages;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class MessagesServiceTests
{
    [Fact]
    public async Task Plain_send_uses_form_urlencoded_and_posts_to_v3_domain_messages()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"id\":\"<20231013@mg.example.com>\",\"message\":\"Queued. Thank you.\"}");

        var resp = await client.Messages.SendAsync("mg.example.com", new SendMessageRequest
        {
            From = "Excited <mailgun@mg.example.com>",
            To = { "alice@example.com", "bob@example.com" },
            Subject = "Hi",
            Text = "Hello",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/mg.example.com/messages", req.Uri.AbsolutePath);
        Assert.Equal("application/x-www-form-urlencoded", req.ContentType);
        Assert.Contains("from=", req.Body, StringComparison.Ordinal);
        Assert.Contains("to=alice%40example.com", req.Body, StringComparison.Ordinal);
        Assert.Contains("to=bob%40example.com", req.Body, StringComparison.Ordinal);
        Assert.Equal("<20231013@mg.example.com>", resp.Id);
    }

    [Fact]
    public async Task Send_with_attachment_uses_multipart()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"x\",\"message\":\"ok\"}");

        await client.Messages.SendAsync("mg.example.com", new SendMessageRequest
        {
            From = "x@mg.example.com",
            To = { "y@example.com" },
            Subject = "x",
            Text = "y",
            Attachments = { new MessageAttachment("hello.txt", System.Text.Encoding.UTF8.GetBytes("hi"), "text/plain") },
        });

        var req = Assert.Single(handler.Requests);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_requires_recipient()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Messages.SendAsync("d", new SendMessageRequest { From = "x@example.com" }));
    }

    [Fact]
    public async Task Send_with_amp_html_serializes_amp_dash_html_field_in_form()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"x\",\"message\":\"ok\"}");

        await client.Messages.SendAsync("mg.example.com", new SendMessageRequest
        {
            From = "x@mg.example.com",
            To = { "y@example.com" },
            Subject = "s",
            Html = "<p>fallback</p>",
            AmpHtml = "<!doctype html><html amp4email>...</html>",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal("application/x-www-form-urlencoded", req.ContentType);
        // amp-html → URL-encoded as amp-html (the hyphen stays a hyphen; only the value is encoded).
        Assert.Contains("amp-html=", req.Body!, StringComparison.Ordinal);
        Assert.Contains("amp4email", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_with_amp_html_and_attachment_serializes_amp_dash_html_part_in_multipart()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"x\",\"message\":\"ok\"}");

        await client.Messages.SendAsync("mg.example.com", new SendMessageRequest
        {
            From = "x@mg.example.com",
            To = { "y@example.com" },
            Subject = "s",
            AmpHtml = "<!doctype html><html amp4email>amp body</html>",
            Attachments = { new MessageAttachment("a.txt", System.Text.Encoding.UTF8.GetBytes("hi"), "text/plain") },
        });

        var req = Assert.Single(handler.Requests);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
        // .NET's MultipartFormDataContent quoting varies (name=amp-html vs name="amp-html").
        // Asserting on the literal field name + content presence is enough to prove serialization.
        Assert.Contains("amp-html", req.Body!, StringComparison.Ordinal);
        Assert.Contains("amp4email", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_omits_amp_html_when_null()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"x\",\"message\":\"ok\"}");

        await client.Messages.SendAsync("mg.example.com", new SendMessageRequest
        {
            From = "x@mg.example.com",
            To = { "y@example.com" },
            Subject = "s",
            Html = "<p>only html</p>",
        });

        var req = Assert.Single(handler.Requests);
        Assert.DoesNotContain("amp-html", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Domain_path_segment_is_percent_encoded()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"x\",\"message\":\"ok\"}");

        await client.Messages.SendAsync("mg.example.com/test", new SendMessageRequest
        {
            From = "x@mg.example.com",
            To = { "y@example.com" },
            Text = "z",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Contains("mg.example.com%2Ftest", req.Uri.AbsoluteUri, StringComparison.Ordinal);
    }
}
