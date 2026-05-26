using System.Text;
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

    [Fact]
    public void TryExtractSignature_returns_false_when_signature_field_is_not_a_string()
    {
        // Regression: JsonElement.GetString() throws InvalidOperationException for non-String /
        // non-Null ValueKinds. The previous implementation only caught JsonException, so an
        // attacker-crafted (or buggy upstream) payload with a number/array/object where a string
        // is expected let that exception escape the Try-method.
        const string json = """
            {
              "signature": {
                "timestamp": 1700000000,
                "token": "abc",
                "signature": "def"
              },
              "event-data": { "event": "delivered" }
            }
            """;

        var ok = MailgunWebhookParser.TryExtractSignature(
            Encoding.UTF8.GetBytes(json),
            out var sig);

        Assert.False(ok);
        Assert.Equal(string.Empty, sig.Timestamp);
        Assert.Equal(string.Empty, sig.Token);
        Assert.Equal(string.Empty, sig.Signature);
    }

    [Fact]
    public void TryExtractSignature_returns_false_for_null_or_missing_fields()
    {
        const string json = """
            {
              "signature": { "timestamp": null, "token": "t", "signature": "s" },
              "event-data": { "event": "delivered" }
            }
            """;
        var ok = MailgunWebhookParser.TryExtractSignature(Encoding.UTF8.GetBytes(json), out _);
        Assert.False(ok);
    }

    [Fact]
    public void Parse_handles_non_string_signature_fields_without_throwing()
    {
        // Same wrong-typed payload as above must also not crash the typed Parse(...) path with
        // InvalidOperationException — the parser degrades to empty signature strings, which lets
        // downstream HMAC verification reject the payload cleanly.
        const string json = """
            {
              "signature": { "timestamp": 1700000000, "token": ["a"], "signature": "s" },
              "event-data": { "event": "delivered" }
            }
            """;
        var evt = MailgunWebhookParser.Parse(json);
        Assert.NotNull(evt.Signature);
        Assert.Equal(string.Empty, evt.Signature!.Timestamp);
        Assert.Equal(string.Empty, evt.Signature.Token);
    }
}
