using System.Net;
using Mailgun.Tests.TestHelpers;
using Mailgun.Webhooks;
using Mailgun.Webhooks.Events;

namespace Mailgun.Tests;

/// <summary>
/// Recorded-fixture deserialization tests for the most-trafficked Mailgun response shapes.
/// Each fixture is a verbatim copy of what Mailgun returns (taken from the official API
/// reference's example responses) — if Mailgun ever ships a backward-incompatible JSON change,
/// these tests fail with a precise property-level pointer instead of the SDK silently dropping
/// fields. Companion to the wire-level multipart and DI regression tests: those pin the
/// request side; these pin the response side.
/// </summary>
public class SerializationDriftTests
{
    [Fact]
    public async Task Messages_send_response_deserializes_from_canonical_payload()
    {
        const string fixture = """
            {
              "id": "<20240312160403.0d28e9a6b7d8e09f@mg.example.com>",
              "message": "Queued. Thank you."
            }
            """;
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, fixture);

        var resp = await client.Messages.SendAsync("mg.example.com", new()
        {
            From = "x@mg.example.com",
            To = { "alice@example.com" },
            Text = "t",
        });

        Assert.Equal("<20240312160403.0d28e9a6b7d8e09f@mg.example.com>", resp.Id);
        Assert.Equal("Queued. Thank you.", resp.Message);
    }

    [Fact]
    public async Task Domain_response_deserializes_with_dns_records_and_disabled_envelope()
    {
        // Verbatim from Mailgun's GET /v4/domains/{name} example. Contains the polymorphic
        // `disabled` object (not bool), the sending + receiving DNS-record arrays, and
        // RFC-2822 dates — i.e. the fixture covers every previously-flaky model decision.
        const string fixture = """
            {
              "domain": {
                "id": "5f8e94ba43e0a30001a3f1f8",
                "name": "mg.example.com",
                "smtp_login": "postmaster@mg.example.com",
                "type": "custom",
                "state": "active",
                "is_disabled": false,
                "disabled": { "permanently": false, "reason": "", "note": "" },
                "require_tls": false,
                "skip_verification": false,
                "spam_action": "tag",
                "wildcard": false,
                "web_scheme": "https",
                "web_prefix": "email",
                "use_automatic_sender_security": true,
                "created_at": "Thu, 13 Oct 2011 18:02:00 +0000"
              },
              "receiving_dns_records": [
                { "record_type": "MX", "value": "mxa.mailgun.org", "priority": "10", "valid": "valid" }
              ],
              "sending_dns_records": [
                { "name": "mg.example.com", "record_type": "TXT", "value": "v=spf1 include:mailgun.org ~all", "valid": "valid" },
                { "name": "krs._domainkey.mg.example.com", "record_type": "TXT", "value": "k=rsa; p=MIG...", "valid": "valid" }
              ]
            }
            """;
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, fixture);

        var resp = await client.Domains.GetAsync("mg.example.com");

        Assert.Equal("mg.example.com", resp.Domain.Name);
        Assert.Equal("active", resp.Domain.State);
        Assert.False(resp.Domain.IsDisabled);
        Assert.False(resp.Domain.Disabled!.Permanently);
        Assert.Equal("https", resp.Domain.WebScheme);
        Assert.True(resp.Domain.UseAutomaticSenderSecurity);
        Assert.NotNull(resp.Domain.CreatedAt);
        Assert.Equal(2011, resp.Domain.CreatedAt!.Value.Year);
        var receiving = Assert.Single(resp.ReceivingDnsRecords!);
        Assert.Equal("MX", receiving.RecordType);
        Assert.Equal(2, resp.SendingDnsRecords!.Count);
    }

    [Fact]
    public async Task Suppressions_bounces_page_deserializes_with_skip_limit_paging()
    {
        const string fixture = """
            {
              "items": [
                { "address": "alice@example.com", "code": "550", "error": "Mailbox not found", "created_at": "Wed, 11 May 2022 16:30:00 +0000" },
                { "address": "bob@example.com",   "code": "552", "error": "Mailbox full",      "created_at": "Wed, 11 May 2022 16:31:00 +0000" }
              ],
              "paging": {
                "first":    "https://api.mailgun.net/v3/mg.example.com/bounces?limit=2",
                "next":     "https://api.mailgun.net/v3/mg.example.com/bounces?page=next&address=bob@example.com",
                "previous": "https://api.mailgun.net/v3/mg.example.com/bounces?page=prev",
                "last":     "https://api.mailgun.net/v3/mg.example.com/bounces?page=last&limit=2"
              },
              "total_count": 17
            }
            """;
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, fixture);

        var page = await client.Suppressions.Bounces.ListAsync("mg.example.com", limit: 2);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal("alice@example.com", page.Items[0].Address);
        Assert.Equal("550", page.Items[0].Code);
        Assert.Equal(2022, page.Items[0].CreatedAt!.Value.Year);
        Assert.Equal(17, page.TotalCount);
        Assert.True(page.HasMore);
        Assert.Contains("page=next", page.NextUrl);
    }

    [Fact]
    public async Task Analytics_logs_event_deserializes_with_typed_substructures()
    {
        // Mimics the failed-delivery event shape from Mailgun's /v1/analytics/logs documentation,
        // including the typed delivery_status / envelope / flags substructures we added in the
        // medium-value polish round.
        const string fixture = """
            {
              "items": [
                {
                  "id": "evt_01HX...",
                  "event": "failed",
                  "timestamp": 1700000000.0,
                  "message_id": "<m1>",
                  "recipient": "alice@example.com",
                  "domain": "example.com",
                  "subject": "Hi",
                  "tags": ["welcome"],
                  "delivery_status": {
                    "code": 550,
                    "description": "Mailbox not found",
                    "message": "550 5.1.1 user unknown",
                    "session-seconds": 0.32,
                    "mx-host": "alt1.gmail-smtp-in.l.google.com",
                    "attempt-no": 1,
                    "bounce_classification": "HARD"
                  },
                  "envelope": {
                    "sender": "noreply@mg.example.com",
                    "mail-from": "noreply@mg.example.com",
                    "transport": "smtp",
                    "targets": "alice@example.com"
                  },
                  "flags": {
                    "is-routed": false,
                    "is-authenticated": true,
                    "is-system-test": false,
                    "is-test-mode": false
                  },
                  "reason": "bounce"
                }
              ],
              "pagination": { "skip": 0, "limit": 1, "total": 1 }
            }
            """;
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, fixture);

        var resp = await client.Analytics.QueryLogsAsync(new()
        {
            Start = "Thu, 01 Jan 2026 00:00:00 +0000",
            End = "Fri, 02 Jan 2026 00:00:00 +0000",
        });

        var item = Assert.Single(resp.Items!);
        Assert.Equal("failed", item.Event);
        Assert.Equal(550, item.DeliveryStatus!.Code);
        Assert.Equal("HARD", item.DeliveryStatus.BounceClassification);
        Assert.Equal("alt1.gmail-smtp-in.l.google.com", item.DeliveryStatus.MxHost);
        Assert.Equal("smtp", item.Envelope!.Transport);
        Assert.True(item.Flags!.IsAuthenticated);
        Assert.False(item.Flags.IsTestMode);
    }

    [Fact]
    public void Webhook_delivered_event_deserializes_with_full_typed_payload()
    {
        // Mailgun v4 webhook payload from the official docs. Pins the envelope (signature +
        // event-data split), typed WebhookMessageInfo, typed delivery-status + envelope, plus
        // user-variables (which intentionally stays a Dictionary because users put arbitrary
        // JSON there).
        const string payload = """
            {
              "signature": {
                "token": "29193ce4fe93a8d4ce72c1...",
                "timestamp": "1700000000",
                "signature": "5b13d5e95e9d76d2f9a23c..."
              },
              "event-data": {
                "id": "evt_01HX...",
                "event": "delivered",
                "timestamp": 1700000000.0,
                "recipient": "alice@example.com",
                "recipient-domain": "example.com",
                "tags": ["welcome", "transactional"],
                "user-variables": { "userId": "user_42", "campaign": "spring" },
                "message": {
                  "headers": { "message-id": "<m1>", "from": "noreply@mg.example.com", "to": "alice@example.com", "subject": "Hi" },
                  "attachments": [],
                  "recipients": ["alice@example.com"],
                  "size": 2048
                },
                "delivery-status": {
                  "tls": true,
                  "certificate-verified": true,
                  "mx-host": "alt1.gmail-smtp-in.l.google.com",
                  "attempt-no": 1,
                  "description": "OK",
                  "session-seconds": 0.32,
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
        var evt = MailgunWebhookParser.Parse(payload);
        var d = Assert.IsType<DeliveredEvent>(evt);

        Assert.Equal("delivered", d.Event);
        Assert.Equal("alice@example.com", d.Recipient);
        Assert.Equal(2, d.Tags!.Count);

        // user-variables stays a Dictionary by design — its values are arbitrary user JSON.
        Assert.NotNull(d.UserVariables);
        Assert.Equal(2, d.UserVariables!.Count);

        Assert.Equal(2048, d.Message!.Size);
        Assert.Equal("<m1>", d.Message.Headers!["message-id"]);

        Assert.True(d.DeliveryStatus!.Tls);
        Assert.Equal(250, d.DeliveryStatus.Code);

        Assert.Equal("1.2.3.4", d.Envelope!.SendingIp);

        // Signature is attached to the event by the parser so downstream callers can verify
        // without parsing twice.
        Assert.NotNull(d.Signature);
        Assert.Equal("1700000000", d.Signature!.Timestamp);
    }

    [Fact]
    public async Task Error_envelope_deserializes_with_message_and_details_array()
    {
        // Mailgun's HTTP-4xx error envelope. Pins that both `message` and the array-shaped
        // `errors`/`details` are flattened into MailgunApiException.Details.
        const string fixture = """
            {
              "message": "Validation failed",
              "errors": ["from is required", "subject is too long"]
            }
            """;
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.UnprocessableEntity, fixture);

        var ex = await Assert.ThrowsAsync<Mailgun.Exceptions.MailgunApiException>(() =>
            client.Domains.GetAsync("mg.example.com"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
        Assert.Equal("Validation failed", ex.ErrorMessage);
        Assert.Equal(2, ex.Details.Count);
        Assert.Contains("from is required", ex.Details);
        Assert.Contains("subject is too long", ex.Details);
    }
}
