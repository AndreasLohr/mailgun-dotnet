using Mailgun.Webhooks;
using Mailgun.Webhooks.Events;

namespace Mailgun.Tests.Webhooks;

/// <summary>
/// Pins down the strongly-typed webhook sub-objects added to replace the previous
/// <c>Dictionary&lt;string, object&gt;?</c> properties on <c>DeliveryStatus</c>, <c>Envelope</c>,
/// <c>Geolocation</c>, and <c>ClientInfo</c>. Each field is documented by Mailgun and now
/// deserializes into a real C# property with a documented type.
/// </summary>
public class TypedSubObjectsTests
{
    [Fact]
    public void Delivered_event_typed_delivery_status_and_envelope_round_trip()
    {
        const string payload = """
            {
              "event-data": {
                "event": "delivered",
                "timestamp": 1700000000.0,
                "recipient": "alice@example.com",
                "delivery-status": {
                  "tls": true,
                  "certificate-verified": true,
                  "mx-host": "aspmx.l.google.com",
                  "attempt-no": 1,
                  "description": "OK",
                  "session-seconds": 0.45,
                  "utf8": false,
                  "code": 250,
                  "message": "OK"
                },
                "envelope": {
                  "transport": "smtp",
                  "sender": "noreply@mg.example.com",
                  "sending-ip": "1.2.3.4",
                  "targets": "alice@example.com"
                }
              }
            }
            """;
        var d = Assert.IsType<DeliveredEvent>(MailgunWebhookParser.Parse(payload));

        Assert.NotNull(d.DeliveryStatus);
        Assert.True(d.DeliveryStatus!.Tls);
        Assert.True(d.DeliveryStatus.CertificateVerified);
        Assert.Equal("aspmx.l.google.com", d.DeliveryStatus.MxHost);
        Assert.Equal(1, d.DeliveryStatus.AttemptNumber);
        Assert.Equal(250, d.DeliveryStatus.Code);
        Assert.Equal(0.45, d.DeliveryStatus.SessionSeconds);

        Assert.NotNull(d.Envelope);
        Assert.Equal("smtp", d.Envelope!.Transport);
        Assert.Equal("noreply@mg.example.com", d.Envelope.Sender);
        Assert.Equal("1.2.3.4", d.Envelope.SendingIp);
    }

    [Fact]
    public void Opened_event_typed_geolocation_and_client_info_round_trip()
    {
        const string payload = """
            {
              "event-data": {
                "event": "opened",
                "timestamp": 1700000000.0,
                "recipient": "alice@example.com",
                "ip": "203.0.113.5",
                "geolocation": { "country": "US", "region": "CA", "city": "San Francisco" },
                "client-info": {
                  "client-os": "macOS",
                  "device-type": "desktop",
                  "client-name": "Safari",
                  "client-type": "browser",
                  "user-agent": "Mozilla/5.0",
                  "bot": ""
                }
              }
            }
            """;
        var o = Assert.IsType<OpenedEvent>(MailgunWebhookParser.Parse(payload));

        Assert.NotNull(o.Geolocation);
        Assert.Equal("US", o.Geolocation!.Country);
        Assert.Equal("CA", o.Geolocation.Region);
        Assert.Equal("San Francisco", o.Geolocation.City);

        Assert.NotNull(o.ClientInfo);
        Assert.Equal("macOS", o.ClientInfo!.ClientOs);
        Assert.Equal("desktop", o.ClientInfo.DeviceType);
        Assert.Equal("Safari", o.ClientInfo.ClientName);
        Assert.Equal("Mozilla/5.0", o.ClientInfo.UserAgent);
        Assert.Equal("", o.ClientInfo.Bot);
    }

    [Fact]
    public void PermanentFail_carries_severity_permanent_and_typed_substructures()
    {
        const string payload = """
            {
              "event-data": {
                "event": "failed",
                "severity": "permanent",
                "reason": "suppress-bounce",
                "timestamp": 1700000000.0,
                "recipient": "alice@example.com",
                "delivery-status": { "code": 550, "message": "Mailbox not found", "bounce_classification": "HARD" },
                "envelope": { "transport": "smtp", "sender": "noreply@mg.example.com" }
              }
            }
            """;
        var f = Assert.IsType<PermanentFailEvent>(MailgunWebhookParser.Parse(payload));
        Assert.Equal(MailgunFailureSeverities.Permanent, f.Severity);
        Assert.Equal("suppress-bounce", f.Reason);
        Assert.Equal(550, f.DeliveryStatus!.Code);
        Assert.Equal("HARD", f.DeliveryStatus.BounceClassification);
        Assert.Equal("noreply@mg.example.com", f.Envelope!.Sender);
    }

    [Fact]
    public void TemporaryFail_carries_severity_temporary_and_typed_substructures()
    {
        const string payload = """
            {
              "event-data": {
                "event": "failed",
                "severity": "temporary",
                "reason": "old",
                "timestamp": 1700000000.0,
                "recipient": "alice@example.com",
                "delivery-status": { "code": 451, "message": "Try again later" },
                "envelope": { "transport": "smtp" }
              }
            }
            """;
        var f = Assert.IsType<TemporaryFailEvent>(MailgunWebhookParser.Parse(payload));
        Assert.Equal(MailgunFailureSeverities.Temporary, f.Severity);
        Assert.Equal("old", f.Reason);
        Assert.Equal(451, f.DeliveryStatus!.Code);
    }

    [Fact]
    public void Failure_severity_constants_carry_distinct_string_values()
    {
        // Regression for the original "PermanentFail" and "TemporaryFail" both being "failed":
        // the discriminator is now expressed as (event, severity), and the severity constants
        // have distinct values.
        Assert.Equal(MailgunEventTypes.Failed, "failed");
        Assert.Equal(MailgunFailureSeverities.Permanent, "permanent");
        Assert.Equal(MailgunFailureSeverities.Temporary, "temporary");
        Assert.NotEqual(MailgunFailureSeverities.Permanent, MailgunFailureSeverities.Temporary);
    }
}
