using System.Net;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Http;

public class SubaccountTests
{
    [Fact]
    public async Task ForSubaccount_adds_on_behalf_of_header_to_requests()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        var sub = client.ForSubaccount("acct_abc123");
        _ = await sub.Routes.ListAsync();

        var req = Assert.Single(handler.Requests);
        Assert.True(req.Headers.ContainsKey("X-Mailgun-On-Behalf-Of"));
        Assert.Equal("acct_abc123", req.Headers["X-Mailgun-On-Behalf-Of"]);
    }

    [Fact]
    public async Task Parent_client_does_not_send_on_behalf_of_header()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        _ = await client.Routes.ListAsync();

        var req = Assert.Single(handler.Requests);
        Assert.False(req.Headers.ContainsKey("X-Mailgun-On-Behalf-Of"));
    }
}
