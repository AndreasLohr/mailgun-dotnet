using System.Net;
using Mailgun.Exceptions;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Http;

/// <summary>
/// Security regression: the SDK attaches Basic-auth to every request, so it must never follow a
/// server-supplied <c>paging.next</c> link to a host outside the configured/known Mailgun origins —
/// otherwise auto-pagination could exfiltrate the API key to an attacker-chosen endpoint. These
/// tests assert the auth header IS attached to legitimate same-origin requests but that no request
/// (and therefore no credential) ever reaches an off-origin pagination target.
/// </summary>
public class PaginationCredentialLeakTests
{
    [Fact]
    public async Task Legitimate_request_carries_auth_but_off_origin_next_link_is_never_contacted()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "items": [{"address":"a@example.com"}],
              "paging": {"next":"https://attacker.example/steal?leak=1"},
              "total_count": 2
            }
            """);

        await Assert.ThrowsAsync<MailgunSerializationException>(async () =>
        {
            await foreach (var _ in client.Suppressions.Bounces.ListAllAsync("mg"))
            {
            }
        });

        // Exactly one request was made — the legitimate first page — and it went to the configured
        // host carrying the Authorization header. The attacker host was never contacted.
        var only = Assert.Single(handler.Requests);
        Assert.Equal("api.mailgun.test", only.Uri.Host);
        Assert.True(only.Headers.ContainsKey("Authorization"),
            "the first, same-origin request must carry the Basic auth header");
        Assert.DoesNotContain(handler.Requests, r =>
            r.Uri.Host.Contains("attacker", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("http://api.mailgun.test/v3/mg/bounces?skip=2")]   // downgrade to HTTP
    [InlineData("https://attacker.example/v3/mg/bounces?skip=2")]  // off-origin host
    [InlineData("ftp://api.mailgun.test/v3/mg/bounces")]           // non-HTTP scheme
    public async Task Rejects_unsafe_pagination_links(string nextUrl)
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, $$"""
            {
              "items": [{"address":"a@example.com"}],
              "paging": {"next":"{{nextUrl}}"},
              "total_count": 2
            }
            """);

        await Assert.ThrowsAsync<MailgunSerializationException>(async () =>
        {
            await foreach (var _ in client.Suppressions.Bounces.ListAllAsync("mg"))
            {
            }
        });

        Assert.Single(handler.Requests);
    }
}
