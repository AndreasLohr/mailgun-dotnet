using System.Net;
using Mailgun.Models.Messages;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

/// <summary>
/// Mirrors MessageOptionsTests but exercises the multipart code path (triggered when attachments
/// or inline assets are present). Mailgun's option matrix is the same — but the BuildMultipart
/// branch has its own mutation surface separate from BuildForm.
/// </summary>
public class MessageMultipartOptionsTests
{
    [Fact]
    public async Task All_typed_options_propagate_through_multipart_when_attachment_present()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"<x>\",\"message\":\"ok\"}");

        await client.Messages.SendAsync("mg.example.com", new SendMessageRequest
        {
            From = "x@mg.example.com",
            To = { "y@example.com" },
            Cc = { "cc@example.com" },
            Bcc = { "bcc@example.com" },
            Subject = "s",
            Text = "t",
            Html = "<p>h</p>",
            Tags = { "t1" },
            Campaigns = { "c1" },
            Template = "tpl",
            TemplateVersion = "v1",
            TemplateText = true,
            TemplateVariables = { ["k"] = "v" },
            RecipientVariables = "{\"y@example.com\":{}}",
            CustomHeaders = { ["X-A"] = "a" },
            CustomVariables = { ["myv"] = "x" },
            TestMode = true,
            Dkim = false,
            DeliveryTime = new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero),
            DeliveryTimeOptimizePeriod = "24h",
            TimeZoneLocalize = "08:00",
            Tracking = "yes",
            TrackingClicks = "htmlonly",
            TrackingOpens = false,
            RequireTls = true,
            SkipVerification = false,
            SendingIp = "1.2.3.4",
            SendingIpPool = "pool",
            TrackingPixelLocationTop = true,
            AdditionalOptions = { ["extra"] = "v" },
            Attachments = { new MessageAttachment("a.txt", new byte[] { 1, 2 }, "text/plain") },
            Inline = { new MessageAttachment("logo.png", new byte[] { 3 }, "image/png") },
        });

        var req = Assert.Single(handler.Requests);
        var body = req.Body!;
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);

        // Every typed option must appear as a multipart text part name. .NET emits the part name
        // unquoted when token-safe, quoted (with backslash-escaped quotes) when not. We accept either.
        foreach (var name in new[]
        {
            "from", "to", "cc", "bcc",
            "subject", "text", "html",
            "o:tag", "o:campaign",
            "o:testmode", "o:dkim",
            "o:deliverytime", "o:deliverytime-optimize-period",
            "o:time-zone-localize", "o:tracking",
            "o:tracking-clicks", "o:tracking-opens",
            "o:require-tls", "o:skip-verification",
            "o:sending-ip", "o:sending-ip-pool",
            "o:tracking-pixel-location-top", "o:extra",
            "template", "t:version", "t:text",
            "v:k", "v:myv", "h:X-A",
            "recipient-variables",
            "attachment", "inline",
        })
        {
            var unquoted = $"name={name}";
            var quoted = $"name=\"{name}\"";
            Assert.True(
                body.Contains(unquoted, StringComparison.Ordinal) || body.Contains(quoted, StringComparison.Ordinal),
                $"multipart body missing part name '{name}'");
        }
        Assert.Contains("a.txt", body, StringComparison.Ordinal);
        Assert.Contains("logo.png", body, StringComparison.Ordinal);

        // Bool values follow the yes/no convention even in multipart text parts.
        Assert.Contains("yes", body, StringComparison.Ordinal);
        Assert.Contains("no", body, StringComparison.Ordinal);

        // Content types of file parts must be propagated.
        Assert.Contains("text/plain", body, StringComparison.Ordinal);
        Assert.Contains("image/png", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inline_only_also_triggers_multipart_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"<x>\",\"message\":\"ok\"}");

        var req = new SendMessageRequest
        {
            From = "x@d",
            To = { "y@d" },
            Subject = "s",
            Text = "t",
            Inline = { new MessageAttachment("logo.png", new byte[] { 1 }, "image/png") },
        };
        Assert.True(req.RequiresMultipart);

        await client.Messages.SendAsync("d", req);

        Assert.StartsWith("multipart/form-data", handler.Requests[0].ContentType, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plain_send_without_binaries_is_form_encoded_not_multipart()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"<x>\",\"message\":\"ok\"}");

        var r = new SendMessageRequest { From = "x@d", To = { "y@d" }, Text = "t" };
        Assert.False(r.RequiresMultipart);

        await client.Messages.SendAsync("d", r);

        Assert.Equal("application/x-www-form-urlencoded", handler.Requests[0].ContentType);
    }

    [Fact]
    public async Task SendingQueues_GET_returns_typed_status()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"regular\":{\"is_disabled\":false},\"scheduled\":{\"is_disabled\":true,\"disabled\":{\"reason\":\"r\"}}}");

        var s = await client.Messages.GetSendingQueuesAsync("d");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/domains/d/sending_queues", req.Uri.AbsolutePath);
        Assert.False(s.Regular!.IsDisabled);
        Assert.True(s.Scheduled!.IsDisabled);
        Assert.Equal("r", s.Scheduled.Disabled!.Reason);
    }

    [Fact]
    public async Task DeleteScheduledEnvelopes_calls_DELETE_on_envelopes_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Messages.DeleteScheduledEnvelopesAsync("d");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v3/domains/d/envelopes", req.Uri.AbsolutePath);
    }
}
