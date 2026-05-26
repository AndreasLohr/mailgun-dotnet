using System.Net;
using Mailgun.Exceptions;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Pagination;

public class AsyncPageableTests
{
    [Fact]
    public async Task Iterates_until_next_url_is_absent()
    {
        var (client, handler) = TestMailgunClient.Create();
        // First page with a server-supplied next URL.
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "items": [{"address":"a@example.com"},{"address":"b@example.com"}],
              "paging": {"next":"https://api.mailgun.test/v3/mg/bounces?skip=2"},
              "total_count": 3
            }
            """);
        // Second page with no next URL → iteration ends.
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "items": [{"address":"c@example.com"}],
              "paging": {},
              "total_count": 3
            }
            """);

        var seen = new List<string>();
        await foreach (var b in client.Suppressions.Bounces.ListAllAsync("mg"))
        {
            seen.Add(b.Address);
        }

        Assert.Equal(new[] { "a@example.com", "b@example.com", "c@example.com" }, seen);
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("?skip=2", handler.Requests[1].Uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_to_follow_off_origin_pagination_link()
    {
        // Regression: SendCoreAsync unconditionally attaches Basic auth, so the SDK must not
        // follow server-supplied paging.next links to arbitrary hosts. A compromised upstream
        // or replayed fixture could otherwise turn auto-pagination into credential exfiltration.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "items": [{"address":"a@example.com"}],
              "paging": {"next":"https://attacker.example/steal"},
              "total_count": 2
            }
            """);

        await Assert.ThrowsAsync<MailgunSerializationException>(async () =>
        {
            await foreach (var _ in client.Suppressions.Bounces.ListAllAsync("mg"))
            {
                // iterate until pagination follow-up throws
            }
        });

        // The first page WAS fetched (it's how we learned about the bad next URL); the second
        // page must NOT have been fetched at all.
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Refuses_to_follow_non_https_pagination_link()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "items": [{"address":"a@example.com"}],
              "paging": {"next":"http://api.mailgun.test/v3/mg/bounces?skip=2"},
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

    [Fact]
    public async Task Follows_pagination_link_to_known_mailgun_region_hosts()
    {
        // Even when the client is configured against a different (test) base URL, well-known
        // Mailgun region hosts must be honored — accounts that span EU+US, or that flip base
        // URL post-construction, should still iterate cleanly.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "items": [{"address":"a@example.com"}],
              "paging": {"next":"https://api.eu.mailgun.net/v3/mg/bounces?skip=1"},
              "total_count": 2
            }
            """);
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "items": [{"address":"b@example.com"}],
              "paging": {},
              "total_count": 2
            }
            """);

        var seen = new List<string>();
        await foreach (var b in client.Suppressions.Bounces.ListAllAsync("mg"))
        {
            seen.Add(b.Address);
        }

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("api.eu.mailgun.net", handler.Requests[1].Uri.Host);
    }
}
