using System.Net;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class SuppressionsServiceTests
{
    [Fact]
    public async Task Bounces_List_hits_v3_domain_bounces()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "items": [{"address":"x@example.com","code":"550","error":"mailbox unavailable"}],
              "paging": {"next":"https://api.mailgun.test/v3/mg/bounces?skip=1"},
              "total_count": 1
            }
            """);

        var page = await client.Suppressions.Bounces.ListAsync("mg.example.com", limit: 5);
        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v3/mg.example.com/bounces?limit=5", req.Uri.AbsoluteUri.Replace("https://api.mailgun.test", string.Empty), StringComparison.Ordinal);
        Assert.Single(page.Items);
        Assert.Equal("x@example.com", page.Items[0].Address);
        Assert.True(page.HasMore);
    }

    [Fact]
    public async Task Unsubscribe_delete_with_tag_appends_query_param()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Suppressions.Unsubscribes.DeleteAsync("mg.example.com", "x@example.com", tag: "marketing");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.Contains("?tag=marketing", req.Uri.AbsoluteUri, StringComparison.Ordinal);
    }
}
