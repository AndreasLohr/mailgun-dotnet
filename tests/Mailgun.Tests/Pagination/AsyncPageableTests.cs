using System.Net;
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
}
