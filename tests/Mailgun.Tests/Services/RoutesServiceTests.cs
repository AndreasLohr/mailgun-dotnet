using System.Net;
using Mailgun.Models.Routes;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class RoutesServiceTests
{
    [Fact]
    public async Task List_paginates_with_limit_and_skip()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"id\":\"r1\",\"priority\":10,\"expression\":\"match_recipient('.*@x.com')\",\"actions\":[\"forward('http://x')\"]}],\"paging\":{},\"total_count\":1}");

        var page = await client.Routes.ListAsync(limit: 25, skip: 0);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/routes", req.Uri.AbsolutePath);
        Assert.Equal("limit=25&skip=0", req.Uri.Query.TrimStart('?'));
        Assert.Single(page.Items);
        Assert.Equal("r1", page.Items[0].Id);
        Assert.Equal(10, page.Items[0].Priority);
    }

    [Fact]
    public async Task ListAll_iterates_pages_via_AsyncPageable()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"id\":\"r1\"}],\"paging\":{\"next\":\"https://api.mailgun.test/v3/routes?skip=1\"},\"total_count\":2}");
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"id\":\"r2\"}],\"paging\":{},\"total_count\":2}");

        var ids = new List<string>();
        await foreach (var r in client.Routes.ListAllAsync())
        {
            ids.Add(r.Id);
        }

        Assert.Equal(new[] { "r1", "r2" }, ids);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Get_extracts_route_from_envelope()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"route\":{\"id\":\"r1\",\"priority\":10,\"description\":\"d\",\"expression\":\"e\",\"actions\":[\"stop()\"]}}");

        var r = await client.Routes.GetAsync("r1");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/routes/r1", req.Uri.AbsolutePath);
        Assert.Equal("d", r.Description);
        Assert.Equal("e", r.Expression);
        Assert.Single(r.Actions!);
    }

    [Fact]
    public async Task Create_posts_priority_description_expression_and_each_action()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"route\":{\"id\":\"r1\",\"expression\":\"e\"}}");

        await client.Routes.CreateAsync(new CreateRouteRequest
        {
            Priority = 1,
            Description = "catchall",
            Expression = "match_recipient(\".*\")",
            Actions = { "forward(\"http://x\")", "stop()" },
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/routes", req.Uri.AbsolutePath);
        Assert.Contains("priority=1", req.Body!, StringComparison.Ordinal);
        Assert.Contains("description=catchall", req.Body!, StringComparison.Ordinal);
        Assert.Contains("expression=", req.Body!, StringComparison.Ordinal);
        Assert.Contains("action=", req.Body!, StringComparison.Ordinal);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(req.Body!, "action=").Count);
    }

    [Fact]
    public async Task Create_rejects_blank_expression()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Routes.CreateAsync(new CreateRouteRequest { Expression = "" }));
    }

    [Fact]
    public async Task Update_puts_form_to_route_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"route\":{\"id\":\"r1\",\"priority\":5}}");

        await client.Routes.UpdateAsync("r1", new UpdateRouteRequest
        {
            Priority = 5,
            Description = "d",
            Expression = "e2",
            Actions = { "stop()" },
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v3/routes/r1", req.Uri.AbsolutePath);
        Assert.Contains("priority=5", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_calls_DELETE_on_route_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Routes.DeleteAsync("r1");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v3/routes/r1", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task Match_posts_recipient_to_routes_match()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\",\"matched\":[{\"id\":\"r1\"}]}");

        var result = await client.Routes.MatchAsync("alice@example.com");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/routes/match", req.Uri.AbsolutePath);
        Assert.Equal("recipient=alice%40example.com", req.Body);
        Assert.Single(result.Matched!);
    }
}
