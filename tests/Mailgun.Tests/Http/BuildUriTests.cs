using System.Net;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Http;

public class BuildUriTests
{
    [Fact]
    public async Task Path_with_existing_query_string_is_emitted_verbatim()
    {
        // Unsubscribe deletion with a tag uses a path that already contains "?tag=...".
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Suppressions.Unsubscribes.DeleteAsync("mg.example.com", "x@example.com", tag: "marketing");

        var req = Assert.Single(handler.Requests);
        Assert.Equal("/v3/mg.example.com/unsubscribes/x%40example.com", req.Uri.AbsolutePath);
        Assert.Equal("?tag=marketing", req.Uri.Query);
    }

    [Fact]
    public async Task Query_parameters_are_url_encoded()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        _ = await client.Suppressions.Bounces.ListAsync("mg.example.com", limit: 5, skip: 10);

        var req = Assert.Single(handler.Requests);
        Assert.Equal("limit=5&skip=10", req.Uri.Query.TrimStart('?'));
    }
}
