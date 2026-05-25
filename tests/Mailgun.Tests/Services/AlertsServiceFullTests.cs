using System.Net;
using Mailgun.Services;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class AlertsServiceFullTests
{
    [Fact]
    public async Task GetSettings_and_UpdateSettings_round_trip_json()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"events\":[]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"events\":[]}");

        var s = await client.Alerts.GetSettingsAsync();
        await client.Alerts.UpdateSettingsAsync(new AlertSettings { Events = new() });

        Assert.NotNull(s.Events);
        Assert.EndsWith("/v1/alerts/settings", handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
    }

    [Fact]
    public async Task ListSlackChannels_and_AddRemove_use_slack_channel_paths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Alerts.ListSlackChannelsAsync();
        await client.Alerts.AddSlackChannelAsync("https://hooks.slack.com/x");
        await client.Alerts.RemoveSlackChannelAsync("ch_1");

        Assert.EndsWith("/v1/alerts/slack/channels", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v1/alerts/slack/channels", handler.Requests[1].Uri.AbsolutePath);
        Assert.EndsWith("/v1/alerts/slack/channels/ch_1", handler.Requests[2].Uri.AbsolutePath);
        Assert.Contains("url=https%3A%2F%2Fhooks.slack.com%2Fx", handler.Requests[1].Body!, StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Delete, handler.Requests[2].Method);
    }

    [Fact]
    public async Task ListEmails_and_AddEmail_use_email_recipients_paths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Alerts.ListEmailsAsync();
        await client.Alerts.AddEmailAsync("ops@example.com");

        Assert.EndsWith("/v1/alerts/email/recipients", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v1/alerts/email/recipients", handler.Requests[1].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Contains("email=ops%40example.com", handler.Requests[1].Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListWebhooks_AddRemove_use_webhook_urls_paths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Alerts.ListWebhooksAsync();
        await client.Alerts.AddWebhookAsync("https://example.com/hook");
        await client.Alerts.RemoveWebhookAsync("wh_1");

        Assert.EndsWith("/v1/alerts/webhook/urls", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v1/alerts/webhook/urls/wh_1", handler.Requests[2].Uri.AbsolutePath);
        Assert.Contains("url=https%3A%2F%2Fexample.com%2Fhook", handler.Requests[1].Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnsubscribeEvent_posts_event_and_channel()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Alerts.UnsubscribeEventAsync("bounce", "email");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/alerts/events/unsubscribe", req.Uri.AbsolutePath);
        Assert.Contains("event=bounce", req.Body!, StringComparison.Ordinal);
        Assert.Contains("channel=email", req.Body!, StringComparison.Ordinal);
    }
}
