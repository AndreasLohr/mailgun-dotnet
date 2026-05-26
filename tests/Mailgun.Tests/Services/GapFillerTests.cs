using System.Net;
using Mailgun.Models.MailingLists;
using Mailgun.Models.Templates;
using Mailgun.Services;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

/// <summary>
/// Fills in the remaining method coverage for services where only a subset of endpoints
/// have a test today: Templates (versions list/get/update), MailingLists (member get/update,
/// ListAllAsync), Webhooks (account-side missing methods), IpPools (Get/Update/RemoveIp),
/// IpsService (ListByDomain, ListDomains), AnalyticsService (usage metrics / logs).
/// </summary>
public class GapFillerTests
{
    // ── Templates ──

    [Fact]
    public async Task Templates_ListVersionsAsync_hits_versions_subpath()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"template\":{\"name\":\"t\",\"versions\":[{\"tag\":\"v1\"},{\"tag\":\"v2\"}]},\"paging\":{}}");

        var p = await client.Templates.ListVersionsAsync("t", limit: 10, skip: 0);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v4/templates/t/versions", req.Uri.AbsolutePath);
        Assert.Equal(2, p.Items.Count);
    }

    [Fact]
    public async Task Templates_GetVersionAsync_extracts_version_from_envelope()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"template\":{\"name\":\"t\",\"version\":{\"tag\":\"v1\",\"template\":\"<p>1</p>\"}}}");

        var v = await client.Templates.GetVersionAsync("t", "v1");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v4/templates/t/versions/v1", req.Uri.AbsolutePath);
        Assert.Equal("v1", v.Tag);
    }

    [Fact]
    public async Task Templates_UpdateVersion_puts_template_form_with_active_flag()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"template\":{\"name\":\"t\",\"version\":{\"tag\":\"v1\",\"active\":true}}}");

        await client.Templates.UpdateVersionAsync("t", "v1", new UpdateTemplateVersionRequest
        {
            Template = "<p>new</p>",
            Comment = "rev",
            Active = true,
            Headers = new() { ["X-Foo"] = "bar" },
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v4/templates/t/versions/v1", req.Uri.AbsolutePath);
        Assert.Contains("active=yes", req.Body!, StringComparison.Ordinal);
        Assert.Contains("comment=rev", req.Body!, StringComparison.Ordinal);
        Assert.Contains("headers=", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Templates_ListAllAsync_iterates_pages()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"name\":\"a\"}],\"paging\":{\"next\":\"https://api.mailgun.test/v4/templates?skip=1\"},\"total_count\":2}");
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"name\":\"b\"}],\"paging\":{},\"total_count\":2}");

        var names = new List<string>();
        await foreach (var t in client.Templates.ListAllAsync())
            names.Add(t.Name);

        Assert.Equal(new[] { "a", "b" }, names);
    }

    // ── MailingLists ──

    [Fact]
    public async Task MailingLists_ListAllAsync_iterates_pages()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"address\":\"a@y\"}],\"paging\":{\"next\":\"https://api.mailgun.test/v3/lists/pages?skip=1\"},\"total_count\":2}");
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"address\":\"b@y\"}],\"paging\":{},\"total_count\":2}");

        var addresses = new List<string>();
        await foreach (var l in client.MailingLists.ListAllAsync())
            addresses.Add(l.Address);

        Assert.Equal(new[] { "a@y", "b@y" }, addresses);
    }

    [Fact]
    public async Task MailingLists_ListAllMembersAsync_passes_subscribed_filter()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[{\"address\":\"a\"}],\"paging\":{},\"total_count\":1}");

        var seen = new List<string>();
        await foreach (var m in client.MailingLists.ListAllMembersAsync("l@y", limit: 5, subscribed: true))
            seen.Add(m.Address);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/lists/l%40y/members/pages", req.Uri.AbsolutePath);
        Assert.Contains("subscribed=yes", req.Uri.Query, StringComparison.Ordinal);
        Assert.Single(seen);
    }

    [Fact]
    public async Task MailingLists_GetMember_and_UpdateMember_use_member_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"member\":{\"address\":\"a@b\",\"name\":\"A\"}}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"member\":{\"address\":\"a@b\",\"name\":\"AA\"}}");

        var m1 = await client.MailingLists.GetMemberAsync("l@y", "a@b");
        var m2 = await client.MailingLists.UpdateMemberAsync("l@y", "a@b", new AddMemberRequest { Address = "a@b", Name = "AA" });

        Assert.Equal("A", m1.Name);
        Assert.Equal("AA", m2.Name);
        Assert.EndsWith("/v3/lists/l%40y/members/a%40b", handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
    }

    [Fact]
    public async Task Webhooks_GetDomain_round_trips_single_webhook()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"webhook\":{\"urls\":[\"https://x\"]}}");

        var w = await client.Webhooks.GetDomainAsync("mg.example.com", "delivered");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/domains/mg.example.com/webhooks/delivered", req.Uri.AbsolutePath);
        Assert.Single(w.Webhook.Urls);
    }

    [Fact]
    public async Task Webhooks_DeleteDomain_calls_DELETE()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Webhooks.DeleteDomainAsync("d", "opened");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v3/domains/d/webhooks/opened", req.Uri.AbsolutePath);
    }

    // ── IpPools gaps ──

    [Fact]
    public async Task IpPools_Update_PatchesMultipart_and_RemoveIp_calls_delete()
    {
        // Mailgun documents PATCH /v3/ip_pools/{id} with multipart and repeatable add_ip / remove_ip,
        // not PUT with a joined "ips=" form field.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.IpPools.UpdateAsync("p1", new()
        {
            Name = "renamed",
            Description = "d",
            AddIps = { "1.1.1.1" },
            RemoveIps = { "3.3.3.3" },
        });
        await client.IpPools.RemoveIpAsync("p1", "2.2.2.2");

        Assert.Equal(HttpMethod.Patch, handler.Requests[0].Method);
        Assert.EndsWith("/v3/ip_pools/p1", handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal("multipart/form-data", handler.Requests[0].ContentType);
        Assert.Contains("renamed", handler.Requests[0].Body!, StringComparison.Ordinal);
        Assert.Contains("add_ip", handler.Requests[0].Body!, StringComparison.Ordinal);
        Assert.Contains("1.1.1.1", handler.Requests[0].Body!, StringComparison.Ordinal);
        Assert.Contains("remove_ip", handler.Requests[0].Body!, StringComparison.Ordinal);
        Assert.Contains("3.3.3.3", handler.Requests[0].Body!, StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.EndsWith("/v3/ip_pools/p1/ips/2.2.2.2", handler.Requests[1].Uri.AbsolutePath);
    }

    // ── Ips gaps ──

    [Fact]
    public async Task Ips_ListByDomain_and_ListDomains_use_correct_paths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[\"1.1.1.1\"],\"details\":[{\"ip\":\"1.1.1.1\"}],\"total_count\":1}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[\"mg.example.com\"]}");

        await client.Ips.ListByDomainAsync("mg.example.com");
        await client.Ips.ListDomainsAsync("1.1.1.1");

        Assert.EndsWith("/v3/domains/mg.example.com/ips", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v3/ips/1.1.1.1/domains", handler.Requests[1].Uri.AbsolutePath);
    }

    // ── DynamicIpPools gaps ──

    [Fact]
    public async Task DynamicIpPools_List_Get_Update_use_json_paths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"dynamic_pools\":[{\"pool_id\":\"dp1\"}]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"pool_id\":\"dp1\",\"name\":\"x\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"pool_id\":\"dp1\",\"name\":\"y\"}");

        await client.DynamicIpPools.ListAsync();
        await client.DynamicIpPools.GetAsync("dp1");
        await client.DynamicIpPools.UpdateAsync("dp1", new() { Name = "y", SendStrategy = "round_robin" });

        Assert.EndsWith("/v1/dynamic_pools", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v1/dynamic_pools/dp1", handler.Requests[1].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Put, handler.Requests[2].Method);
        Assert.Equal("application/json", handler.Requests[2].ContentType);
        Assert.Contains("\"name\":\"y\"", handler.Requests[2].Body!, StringComparison.Ordinal);
    }

    // ── Analytics gaps ──

    [Fact]
    public async Task Analytics_QueryUsageMetrics_and_QueryLogs_use_correct_paths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");

        await client.Analytics.QueryUsageMetricsAsync(new()
        {
            Start = "Thu, 01 Jan 2026 00:00:00 +0000",
            End = "Fri, 02 Jan 2026 00:00:00 +0000",
            Resolution = "1d",
        });
        await client.Analytics.QueryLogsAsync(new()
        {
            Start = "Thu, 01 Jan 2026 00:00:00 +0000",
            End = "Fri, 02 Jan 2026 00:00:00 +0000",
            Events = new() { "delivered", "failed" },
        });

        Assert.EndsWith("/v1/analytics/usage/metrics", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v1/analytics/logs", handler.Requests[1].Uri.AbsolutePath);
        Assert.All(handler.Requests, r => Assert.Equal("application/json", r.ContentType));
    }

    // ── KeysService gaps (paging) ──

    [Fact]
    public async Task Keys_ListAllAsync_iterates_pages()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"id\":\"k1\"}],\"paging\":{\"next\":\"https://api.mailgun.test/v1/keys?skip=1\"},\"total_count\":2}");
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"id\":\"k2\"}],\"paging\":{},\"total_count\":2}");

        var ids = new List<string>();
        await foreach (var k in client.Keys.ListAllAsync())
            ids.Add(k.Id);

        Assert.Equal(new[] { "k1", "k2" }, ids);
    }

    // ── InboxPlacement gaps ──

    [Fact]
    public async Task InboxPlacement_ListResults_GetResult_ListProviders()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[{\"id\":\"r_1\"}]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"r_1\",\"subject\":\"s\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");

        await client.InboxPlacement.ListResultsAsync();
        await client.InboxPlacement.GetResultAsync("r_1");
        await client.InboxPlacement.ListProvidersAsync();

        Assert.EndsWith("/v4/inbox/results", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v4/inbox/results/r_1", handler.Requests[1].Uri.AbsolutePath);
        Assert.EndsWith("/v4/inbox/providers", handler.Requests[2].Uri.AbsolutePath);
    }

    // ── Validate bulk preview gaps ──

    [Fact]
    public async Task Validate_BulkPreview_Create_Get_List_Delete()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"preview\":{}}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"preview\":{}}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"previews\":[]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("address\na@b\n"));
        await client.Validate.CreateBulkPreviewAsync("my-list", ms);
        await client.Validate.GetBulkPreviewAsync("my-list");
        await client.Validate.ListBulkPreviewsAsync();
        await client.Validate.DeleteBulkPreviewAsync("my-list");

        Assert.EndsWith("/v4/address/validate/bulk/preview/my-list", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v4/address/validate/bulk/preview/my-list", handler.Requests[1].Uri.AbsolutePath);
        Assert.EndsWith("/v4/address/validate/bulk/preview", handler.Requests[2].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, handler.Requests[3].Method);
    }

    // ── Account features ──

    [Fact]
    public async Task Account_GetFeatures_and_ListSandboxAuthRecipients()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"foo\":true,\"bar\":false}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");

        var f = await client.Account.GetFeaturesAsync();
        await client.Account.ListSandboxAuthRecipientsAsync();

        Assert.True(f["foo"]);
        Assert.False(f["bar"]);
        Assert.EndsWith("/v5/accounts/features", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v5/sandbox/auth_recipients", handler.Requests[1].Uri.AbsolutePath);
    }
}
