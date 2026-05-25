using System.Net;
using Mailgun.Models.Messages;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

/// <summary>
/// Mailgun message-send accepts dozens of typed options that map to <c>o:</c>, <c>v:</c>,
/// <c>h:</c>, <c>t:</c>, and template fields. This battery exercises the full mapping for
/// both form-encoded and multipart code paths.
/// </summary>
public class MessageOptionsTests
{
    private static readonly string OkResponse = "{\"id\":\"<x>\",\"message\":\"ok\"}";

    [Fact]
    public async Task All_typed_options_map_to_correct_form_fields()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, OkResponse);

        await client.Messages.SendAsync("mg.example.com", new SendMessageRequest
        {
            From = "x@mg.example.com",
            To = { "to@example.com" },
            Cc = { "cc@example.com" },
            Bcc = { "bcc@example.com" },
            Subject = "subj",
            Text = "body",
            Html = "<p>body</p>",
            Tags = { "welcome", "promo" },
            Campaigns = { "camp_1" },
            Template = "tpl",
            TemplateVersion = "v3",
            TemplateText = true,
            TemplateVariables = { ["name"] = "alice" },
            RecipientVariables = "{\"to@example.com\":{\"id\":1}}",
            CustomHeaders = { ["X-Foo"] = "bar" },
            CustomVariables = { ["myvar"] = "val" },
            TestMode = true,
            Dkim = false,
            DeliveryTime = new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero),
            DeliveryTimeOptimizePeriod = "24h",
            TimeZoneLocalize = "08:00",
            Tracking = "yes",
            TrackingClicks = "htmlonly",
            TrackingOpens = true,
            RequireTls = true,
            SkipVerification = false,
            SendingIp = "1.2.3.4",
            SendingIpPool = "pool_x",
            TrackingPixelLocationTop = true,
            AdditionalOptions = { ["custom-flag"] = "y" },
        });

        var req = Assert.Single(handler.Requests);
        var body = req.Body!;
        Assert.Equal("application/x-www-form-urlencoded", req.ContentType);

        Assert.Contains("from=x%40mg.example.com", body, StringComparison.Ordinal);
        Assert.Contains("to=to%40example.com", body, StringComparison.Ordinal);
        Assert.Contains("cc=cc%40example.com", body, StringComparison.Ordinal);
        Assert.Contains("bcc=bcc%40example.com", body, StringComparison.Ordinal);
        Assert.Contains("subject=subj", body, StringComparison.Ordinal);
        Assert.Contains("text=body", body, StringComparison.Ordinal);
        Assert.Contains("html=%3Cp%3Ebody%3C%2Fp%3E", body, StringComparison.Ordinal);
        Assert.Contains("o%3Atag=welcome", body, StringComparison.Ordinal);
        Assert.Contains("o%3Atag=promo", body, StringComparison.Ordinal);
        Assert.Contains("o%3Acampaign=camp_1", body, StringComparison.Ordinal);
        Assert.Contains("o%3Atestmode=yes", body, StringComparison.Ordinal);
        Assert.Contains("o%3Adkim=no", body, StringComparison.Ordinal);
        Assert.Contains("o%3Adeliverytime-optimize-period=24h", body, StringComparison.Ordinal);
        Assert.Contains("o%3Atime-zone-localize=08%3A00", body, StringComparison.Ordinal);
        Assert.Contains("o%3Atracking=yes", body, StringComparison.Ordinal);
        Assert.Contains("o%3Atracking-clicks=htmlonly", body, StringComparison.Ordinal);
        Assert.Contains("o%3Atracking-opens=yes", body, StringComparison.Ordinal);
        Assert.Contains("o%3Arequire-tls=yes", body, StringComparison.Ordinal);
        Assert.Contains("o%3Askip-verification=no", body, StringComparison.Ordinal);
        Assert.Contains("o%3Asending-ip=1.2.3.4", body, StringComparison.Ordinal);
        Assert.Contains("o%3Asending-ip-pool=pool_x", body, StringComparison.Ordinal);
        Assert.Contains("o%3Atracking-pixel-location-top=yes", body, StringComparison.Ordinal);
        Assert.Contains("o%3Acustom-flag=y", body, StringComparison.Ordinal);
        Assert.Contains("template=tpl", body, StringComparison.Ordinal);
        Assert.Contains("t%3Aversion=v3", body, StringComparison.Ordinal);
        Assert.Contains("t%3Atext=yes", body, StringComparison.Ordinal);
        Assert.Contains("v%3Aname=alice", body, StringComparison.Ordinal);
        Assert.Contains("v%3Amyvar=val", body, StringComparison.Ordinal);
        Assert.Contains("h%3AX-Foo=bar", body, StringComparison.Ordinal);
        Assert.Contains("recipient-variables=", body, StringComparison.Ordinal);
        Assert.Contains("o%3Adeliverytime=", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendMime_attaches_message_as_multipart_part_with_rfc822_content_type()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, OkResponse);

        await client.Messages.SendMimeAsync(
            "mg.example.com",
            new[] { "alice@example.com" },
            System.Text.Encoding.ASCII.GetBytes("From: x\r\nSubject: y\r\n\r\nhello"),
            testMode: true);

        var req = Assert.Single(handler.Requests);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
        Assert.EndsWith("/v3/mg.example.com/messages.mime", req.Uri.AbsolutePath);
        Assert.Contains("o:testmode", req.Body!, StringComparison.Ordinal);
        Assert.Contains("message/rfc822", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendMime_requires_at_least_one_recipient()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Messages.SendMimeAsync("d", Array.Empty<string>(), new byte[] { 1 }));
    }

    [Fact]
    public async Task GetStored_and_DeleteStored_address_storage_key_correctly()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        _ = await client.Messages.GetStoredAsync("mg.example.com", "WyJhYmM");
        await client.Messages.DeleteStoredAsync("mg.example.com", "WyJhYmM");

        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/v3/domains/mg.example.com/messages/WyJhYmM", handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
    }
}
