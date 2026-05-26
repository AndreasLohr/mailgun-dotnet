using System.Net;
using Mailgun.Services;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class ExtendedServicesTests
{
    // ── IPs extensions ──

    [Fact]
    public async Task Ips_GetReputationBand_hits_ip_band_subpath()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"ip\":\"1.2.3.4\",\"band\":\"healthy\",\"score\":0.92}");

        var b = await client.Ips.GetReputationBandAsync("1.2.3.4");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/ips/1.2.3.4/ip_band", req.Uri.AbsolutePath);
        Assert.Equal("healthy", b.Band);
        Assert.Equal(0.92, b.Score);
    }

    [Fact]
    public async Task Ips_ListDetailed_hits_v3_ips_details()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        await client.Ips.ListDetailedAsync();

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/ips/details", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task Ips_ListAllAccountIps_hits_v3_ips_all()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        await client.Ips.ListAllAccountIpsAsync();

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/ips/all", req.Uri.AbsolutePath);
    }

    // ── IpPools extensions ──

    [Fact]
    public async Task IpPools_AddIps_posts_json_with_ips_array_to_ips_json_subpath()
    {
        // Mailgun documents POST /v3/ip_pools/{id}/ips.json as "Add multiple IPs" — the SDK no
        // longer pretends this is a replace (the misleading ReplaceIpsAsync was renamed/removed).
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.IpPools.AddIpsAsync("p1", new[] { "1.1.1.1", "2.2.2.2" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("application/json", req.ContentType);
        Assert.EndsWith("/v3/ip_pools/p1/ips.json", req.Uri.AbsolutePath);
        Assert.Contains("\"ips\":[\"1.1.1.1\",\"2.2.2.2\"]", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IpPools_AddIp_puts_to_ips_segment_subpath()
    {
        // PUT /v3/ip_pools/{id}/ips/{ip} — Mailgun's documented "add single IP" endpoint.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.IpPools.AddIpAsync("p1", "1.1.1.1");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v3/ip_pools/p1/ips/1.1.1.1", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task IpPools_Delegate_puts_multipart_with_singular_subaccount_id()
    {
        // Mailgun documents PUT /v3/ip_pools/{id}/delegate with multipart `subaccount_id` (singular).
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.IpPools.DelegateAsync("p1", "acct_a");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v3/ip_pools/p1/delegate", req.Uri.AbsolutePath);
        Assert.Equal("multipart/form-data", req.ContentType);
        Assert.Contains("subaccount_id", req.Body!, StringComparison.Ordinal);
        Assert.Contains("acct_a", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IpPools_Delegate_rejects_empty_subaccount_id()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.IpPools.DelegateAsync("p1", ""));
    }

    [Fact]
    public async Task IpPools_ListDelegations_and_RevokeDelegation_use_correct_paths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"subaccounts\":[\"acct_a\"]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.IpPools.ListDelegationsAsync("p1");
        await client.IpPools.RevokeDelegationAsync("p1", "acct_a");

        Assert.EndsWith("/v3/ip_pools/p1/delegations", handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        // Revoke uses DELETE /v3/ip_pools/{poolId}/delegate with multipart subaccount_id in the body,
        // NOT a path-segment subaccount id (the old shape Mailgun never supported here).
        Assert.EndsWith("/v3/ip_pools/p1/delegate", handler.Requests[1].Uri.AbsolutePath);
        Assert.Equal("multipart/form-data", handler.Requests[1].ContentType);
        Assert.Contains("subaccount_id", handler.Requests[1].Body!, StringComparison.Ordinal);
        Assert.Contains("acct_a", handler.Requests[1].Body!, StringComparison.Ordinal);
    }

    // ── InboxPlacement extensions ──

    [Fact]
    public async Task InboxPlacement_DeleteResult_calls_DELETE()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.InboxPlacement.DeleteResultAsync("r_1");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v4/inbox/results/r_1", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task InboxPlacement_GetResultDetails_and_GetResultCounters_use_subpaths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"r_1\",\"providers\":[{\"provider\":\"gmail\",\"inbox\":5}]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"inbox\":5,\"spam\":1,\"missing\":0,\"total\":6}");

        var details = await client.InboxPlacement.GetResultDetailsAsync("r_1");
        var counters = await client.InboxPlacement.GetResultCountersAsync("r_1");

        Assert.EndsWith("/v4/inbox/results/r_1/details", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v4/inbox/results/r_1/counters", handler.Requests[1].Uri.AbsolutePath);
        Assert.Single(details.Providers!);
        Assert.Equal(6, counters.Total);
    }

    [Fact]
    public async Task InboxPlacement_AddSeed_and_RemoveSeed_use_email_subpath()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.InboxPlacement.AddSeedAsync("main", "seed@example.com");
        await client.InboxPlacement.RemoveSeedAsync("main", "seed@example.com");

        Assert.EndsWith("/v4/inbox/seedlists/main/seeds", handler.Requests[0].Uri.AbsolutePath);
        Assert.Contains("\"email\":\"seed@example.com\"", handler.Requests[0].Body!, StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.EndsWith("/v4/inbox/seedlists/main/seeds/seed%40example.com", handler.Requests[1].Uri.AbsolutePath);
    }

    [Fact]
    public async Task InboxPlacement_ListResultsForSeedlist_uses_seedlist_subpath()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");

        await client.InboxPlacement.ListResultsForSeedlistAsync("main");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v4/inbox/seedlists/main/results", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task InboxPlacement_FilterResults_serializes_filter_as_query_string()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");

        await client.InboxPlacement.FilterResultsAsync(new InboxPlacementResultsFilter
        {
            Subject = "Promo",
            FromDomain = "mg.example.com",
            Seedlist = "main",
            Limit = 50,
        });

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v4/inbox/results/filter", req.Uri.AbsolutePath);
        var q = req.Uri.Query.TrimStart('?');
        Assert.Contains("subject=Promo", q, StringComparison.Ordinal);
        Assert.Contains("from_domain=mg.example.com", q, StringComparison.Ordinal);
        Assert.Contains("seedlist=main", q, StringComparison.Ordinal);
        Assert.Contains("limit=50", q, StringComparison.Ordinal);
    }

    // ── BounceClassification extensions ──

    [Fact]
    public async Task BounceClassification_ListCodes_hits_classification_codes_subpath()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[\"550\",\"552\"]}");

        var r = await client.BounceClassification.ListCodesAsync("HARD");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/bounce-classification/HARD/codes", req.Uri.AbsolutePath);
        Assert.Equal(2, r.Items!.Count);
    }

    [Fact]
    public async Task BounceClassification_Classify_posts_json_to_classify_endpoint()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"code\":\"HARD\",\"category\":\"permanent\"}");

        var c = await client.BounceClassification.ClassifyAsync(new ClassifyBounceRequest
        {
            Status = "5.1.1",
            Code = "550",
            Message = "Mailbox does not exist",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v1/bounce-classification/classify", req.Uri.AbsolutePath);
        Assert.Contains("\"status\":\"5.1.1\"", req.Body!, StringComparison.Ordinal);
        Assert.Equal("HARD", c.Code);
        Assert.Equal("permanent", c.Category);
    }

    [Fact]
    public async Task BounceClassification_ListCategories_ListDimensions_ListMetricsCodes()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");

        await client.BounceClassification.ListCategoriesAsync();
        await client.BounceClassification.ListDimensionsAsync();
        await client.BounceClassification.ListMetricsCodesAsync();

        Assert.EndsWith("/v1/bounce-classification/categories", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v2/bounce-classification/metrics/dimensions", handler.Requests[1].Uri.AbsolutePath);
        Assert.EndsWith("/v2/bounce-classification/metrics/codes", handler.Requests[2].Uri.AbsolutePath);
    }

    // ── Limits extensions ──

    [Fact]
    public async Task Limits_CRUD_hits_documented_paths_and_json_body()
    {
        // Mailgun documents /v1/thresholds/limits as a CRUD-over-named-rules resource (List/Get/
        // Create/Update/Delete by name) — NOT the obsolete /enable & /disable subpaths the SDK
        // used to call.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[{\"name\":\"daily-cap\",\"metric\":\"accepted_count\",\"comparator\":\"gt\",\"limit\":\"1000\",\"dimension\":\"domain\"}],\"total\":1}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"name\":\"daily-cap\",\"metric\":\"accepted_count\",\"comparator\":\"gt\",\"limit\":\"1000\",\"dimension\":\"domain\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"name\":\"new-rule\",\"id\":\"abc\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"name\":\"new-rule\"}");
        handler.EnqueueResponse(HttpStatusCode.NoContent, "");

        var list = await client.Limits.ListAsync();
        await client.Limits.GetAsync("daily-cap");
        await client.Limits.CreateAsync(new LimitRule
        {
            Name = "new-rule", Metric = "accepted_count", Comparator = "gt",
            Limit = "5000", Dimension = "domain",
        });
        await client.Limits.UpdateAsync("new-rule", new LimitRule
        {
            Name = "new-rule", Metric = "accepted_count", Comparator = "gt",
            Limit = "10000", Dimension = "domain",
        });
        await client.Limits.DeleteAsync("new-rule");

        Assert.Single(list.Items!);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.EndsWith("/v1/thresholds/limits", handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.EndsWith("/v1/thresholds/limits/daily-cap", handler.Requests[1].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
        Assert.Equal("application/json", handler.Requests[2].ContentType);
        Assert.Contains("\"name\":\"new-rule\"", handler.Requests[2].Body!, StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Put, handler.Requests[3].Method);
        Assert.EndsWith("/v1/thresholds/limits/new-rule", handler.Requests[3].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.Requests[4].Method);
    }

    [Fact]
    public async Task Limits_Create_rejects_missing_required_fields()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Limits.CreateAsync(new LimitRule { Name = "x" /* missing metric/comparator/limit/dimension */ }));
    }
}
