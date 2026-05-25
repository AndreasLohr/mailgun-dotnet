using System.Diagnostics;
using System.Net;
using Mailgun.Http;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests;

/// <summary>
/// Targets the high-ROI Stryker NoCoverage mutants identified during the survivor triage:
/// the <c>ListAllAsync</c> variants on Complaints / Unsubscribes / Allowlists (each currently
/// has no test), <c>WebhooksService.ListDomainAsync</c> (no test), and the activity-tag
/// emission inside <c>MailgunHttpClient.SendCoreAsync</c> (requires an <c>ActivityListener</c>
/// subscribed to the SDK's <c>ActivitySource</c>).
/// </summary>
public class NoCoverageKillerTests
{
    [Fact]
    public async Task Complaints_ListAllAsync_iterates_through_pages()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {"items":[{"address":"a@x"}],"paging":{"next":"https://api.mailgun.test/v3/d/complaints?skip=1"},"total_count":2}
            """);
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {"items":[{"address":"b@x"}],"paging":{},"total_count":2}
            """);

        var addresses = new List<string>();
        await foreach (var c in client.Suppressions.Complaints.ListAllAsync("d"))
            addresses.Add(c.Address);

        Assert.Equal(new[] { "a@x", "b@x" }, addresses);
        Assert.EndsWith("/v3/d/complaints", handler.Requests[0].Uri.AbsolutePath);
    }

    [Fact]
    public async Task Unsubscribes_ListAllAsync_iterates_through_pages()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {"items":[{"address":"a@x"}],"paging":{"next":"https://api.mailgun.test/v3/d/unsubscribes?skip=1"},"total_count":2}
            """);
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {"items":[{"address":"b@x"}],"paging":{},"total_count":2}
            """);

        var addresses = new List<string>();
        await foreach (var u in client.Suppressions.Unsubscribes.ListAllAsync("d"))
            addresses.Add(u.Address);

        Assert.Equal(new[] { "a@x", "b@x" }, addresses);
        Assert.EndsWith("/v3/d/unsubscribes", handler.Requests[0].Uri.AbsolutePath);
    }

    [Fact]
    public async Task Allowlists_ListAllAsync_iterates_through_pages()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {"items":[{"value":"a@x"}],"paging":{"next":"https://api.mailgun.test/v3/d/whitelists?skip=1"},"total_count":2}
            """);
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {"items":[{"value":"b@x"}],"paging":{},"total_count":2}
            """);

        var values = new List<string>();
        await foreach (var a in client.Suppressions.Allowlists.ListAllAsync("d"))
            values.Add(a.Value!);

        Assert.Equal(new[] { "a@x", "b@x" }, values);
        Assert.EndsWith("/v3/d/whitelists", handler.Requests[0].Uri.AbsolutePath);
    }

    [Fact]
    public async Task Webhooks_ListDomain_returns_typed_map()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"webhooks\":{\"delivered\":{\"urls\":[\"https://a\"]},\"opened\":{\"urls\":[\"https://b\"]}}}");

        var map = await client.Webhooks.ListDomainAsync("mg.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v4/domains/mg.example.com/webhooks", req.Uri.AbsolutePath);
        Assert.Equal(2, map.Webhooks.Count);
        Assert.Single(map.Webhooks["delivered"].Urls);
    }

    /// <summary>
    /// Captures stopped activities that match a per-test tag, so concurrent test runs that
    /// share the global <see cref="ActivitySource"/> don't clobber each other's collections.
    /// </summary>
    private static (ActivityListener listener, System.Collections.Concurrent.ConcurrentBag<Activity> bag, string tag) RegisterListener()
    {
        var tag = Guid.NewGuid().ToString();
        var bag = new System.Collections.Concurrent.ConcurrentBag<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == MailgunActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            // Baggage propagates from a parent Activity to its children automatically (unlike
            // Tags, which stay on the activity that set them). We tag the test-scope parent
            // with a unique baggage entry and filter child SDK activities by that value, so
            // parallel xUnit test methods don't see each other's spans.
            ActivityStopped = a =>
            {
                if (a.GetBaggageItem("test.id") == tag)
                    bag.Add(a);
            },
        };
        ActivitySource.AddActivityListener(listener);
        return (listener, bag, tag);
    }

    [Fact]
    public async Task ActivitySource_emits_spans_with_documented_tags_per_request()
    {
        var (listener, bag, tag) = RegisterListener();
        using var l = listener;

        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}",
            headers: new Dictionary<string, string>
            {
                { "X-Mailgun-Request-Id", "req-xyz" },
                { "X-RateLimit-Remaining", "297" },
            });

        // Tag the test's own activity so the listener filter picks only spans from this test.
        using (var scope = new Activity("test-scope").AddBaggage("test.id", tag).Start())
        {
            _ = await client.Routes.ListAsync();
        }

        var activity = Assert.Single(bag);
        Assert.Equal("mailgun GET", activity.OperationName);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        var tags = activity.TagObjects.ToDictionary(t => t.Key, t => t.Value);
        Assert.Equal("GET", tags["http.request.method"]);
        Assert.Equal(200, tags["http.response.status_code"]);
        Assert.Equal("api.mailgun.test", tags["server.address"]);
        Assert.Contains("/v3/routes", tags["url.full"]!.ToString());
        Assert.Equal("req-xyz", tags["mailgun.request_id"]);
        Assert.Equal(297, tags["mailgun.rate_limit.remaining"]);
    }

    [Fact]
    public async Task ActivitySource_marks_failed_HTTP_responses_with_error_status()
    {
        var (listener, bag, tag) = RegisterListener();
        using var l = listener;

        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, "{\"message\":\"nope\"}");

        using (var scope = new Activity("test-scope").AddBaggage("test.id", tag).Start())
        {
            await Assert.ThrowsAsync<Mailgun.Exceptions.MailgunApiException>(() => client.Domains.GetAsync("x"));
        }

        var activity = Assert.Single(bag);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }
}
