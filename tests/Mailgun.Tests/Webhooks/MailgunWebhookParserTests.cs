using Mailgun.Webhooks;
using Mailgun.Webhooks.Events;

namespace Mailgun.Tests.Webhooks;

public class MailgunWebhookParserTests
{
    private const string DeliveredPayload = """
        {
          "signature": {
            "timestamp": "1758000000",
            "token": "abc123",
            "signature": "dummyhex"
          },
          "event-data": {
            "event": "delivered",
            "id": "evt_001",
            "timestamp": 1758000000.0,
            "recipient": "alice@example.com",
            "recipient-domain": "example.com",
            "tags": ["welcome"]
          }
        }
        """;

    private const string FailedPermanent = """
        { "event-data": { "event":"failed", "severity":"permanent", "recipient":"x@y.com", "timestamp": 1.0 } }
        """;

    private const string FailedTemporary = """
        { "event-data": { "event":"failed", "severity":"temporary", "recipient":"x@y.com", "timestamp": 1.0 } }
        """;

    private const string UnknownEvent = """
        { "event-data": { "event":"weird_new_event", "timestamp": 1.0 } }
        """;

    [Fact]
    public void Parses_delivered_event_with_signature()
    {
        var evt = MailgunWebhookParser.Parse(DeliveredPayload);
        var d = Assert.IsType<DeliveredEvent>(evt);
        Assert.Equal("delivered", d.Event);
        Assert.Equal("alice@example.com", d.Recipient);
        Assert.NotNull(d.Signature);
        Assert.Equal("abc123", d.Signature!.Token);
    }

    [Fact]
    public void Routes_failed_permanent_to_PermanentFailEvent()
    {
        var evt = MailgunWebhookParser.Parse(FailedPermanent);
        Assert.IsType<PermanentFailEvent>(evt);
    }

    [Fact]
    public void Routes_failed_temporary_to_TemporaryFailEvent()
    {
        var evt = MailgunWebhookParser.Parse(FailedTemporary);
        Assert.IsType<TemporaryFailEvent>(evt);
    }

    [Fact]
    public void Unknown_event_falls_back_to_UnknownMailgunWebhookEvent()
    {
        var evt = MailgunWebhookParser.Parse(UnknownEvent);
        var u = Assert.IsType<UnknownMailgunWebhookEvent>(evt);
        Assert.Equal("weird_new_event", u.Event);
        Assert.Contains("weird_new_event", u.RawJson, StringComparison.Ordinal);
    }
}
