using System.Net;
using Mailgun.Services;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class IpServicesTests
{
    // ──────────── Ips ────────────

    [Fact]
    public async Task Ips_List_with_dedicated_filter_hits_v3_ips()
    {
        var (client, handler) = TestMailgunClient.Create();
        // Mailgun's actual /v3/ips response: items is string[], details is the rich object list.
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[\"1.1.1.1\"],\"details\":[{\"ip\":\"1.1.1.1\"}],\"total_count\":1}");

        await client.Ips.ListAsync(dedicated: true);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/ips", req.Uri.AbsolutePath);
        Assert.Equal("dedicated=yes", req.Uri.Query.TrimStart('?'));
    }

    [Fact]
    public async Task Ips_Get_targets_specific_ip()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"ip\":\"1.2.3.4\",\"dedicated\":false}");

        var ip = await client.Ips.GetAsync("1.2.3.4");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/ips/1.2.3.4", req.Uri.AbsolutePath);
        Assert.False(ip.Dedicated);
    }

    [Fact]
    public async Task Ips_AttachToDomain_posts_ip_form_field()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Ips.AttachToDomainAsync("mg.example.com", "1.2.3.4");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/domains/mg.example.com/ips", req.Uri.AbsolutePath);
        Assert.Equal("ip=1.2.3.4", req.Body);
    }

    [Fact]
    public async Task Ips_DetachFromDomain_deletes()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Ips.DetachFromDomainAsync("mg.example.com", "1.2.3.4");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v3/domains/mg.example.com/ips/1.2.3.4", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task Ips_RequestNew_posts_empty_form()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Ips.RequestNewAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/ips/request/new", req.Uri.AbsolutePath);
    }

    // ──────────── IpPools ────────────

    [Fact]
    public async Task IpPools_List_Get_hit_v3_ip_pools()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"ip_pools\":[]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"pool_id\":\"p1\",\"name\":\"warm\"}");

        await client.IpPools.ListAsync();
        var p = await client.IpPools.GetAsync("p1");

        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/v3/ip_pools", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v3/ip_pools/p1", handler.Requests[1].Uri.AbsolutePath);
        Assert.Equal("warm", p.Name);
    }

    [Fact]
    public async Task IpPools_Create_posts_form_with_ips_comma_joined()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"pool_id\":\"p1\"}");

        await client.IpPools.CreateAsync(new CreateIpPoolRequest
        {
            Name = "warm",
            Description = "warmup pool",
            Ips = { "1.1.1.1", "2.2.2.2" },
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("name=warm", req.Body!, StringComparison.Ordinal);
        Assert.Contains("description=warmup+pool", req.Body!, StringComparison.Ordinal);
        Assert.Contains("ips=1.1.1.1%2C2.2.2.2", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IpPools_Delete_with_replacement_appends_pool_id_query()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.IpPools.DeleteAsync("p1", replacementPool: "p2");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.Equal("?pool_id=p2", req.Uri.Query);
    }

    [Fact]
    public async Task IpPools_AddIps_fans_out_each_ip_as_separate_form_field()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.IpPools.AddIpsAsync("p1", new[] { "1.1.1.1", "2.2.2.2" });

        var req = Assert.Single(handler.Requests);
        Assert.Contains("ip=1.1.1.1", req.Body!, StringComparison.Ordinal);
        Assert.Contains("ip=2.2.2.2", req.Body!, StringComparison.Ordinal);
    }

    // ──────────── DynamicIpPools ────────────

    [Fact]
    public async Task DynamicIpPools_Create_posts_json_body_to_v1_dynamic_pools()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"pool_id\":\"dp1\",\"name\":\"smart\"}");

        await client.DynamicIpPools.CreateAsync(new CreateDynamicIpPoolRequest
        {
            Name = "smart",
            Description = "auto-warm",
            SendStrategy = "round_robin",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("application/json", req.ContentType);
        Assert.EndsWith("/v1/dynamic_pools", req.Uri.AbsolutePath);
        Assert.Contains("\"name\":\"smart\"", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DynamicIpPools_Delete_uses_DELETE_on_pool_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.DynamicIpPools.DeleteAsync("dp1");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v1/dynamic_pools/dp1", req.Uri.AbsolutePath);
    }

    // ──────────── IpWarmups ────────────

    [Fact]
    public async Task IpWarmups_List_Get_Start_Stop_use_correct_paths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"ip\":\"1.2.3.4\",\"state\":\"running\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"ip\":\"1.2.3.4\",\"state\":\"running\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.IpWarmups.ListAsync();
        await client.IpWarmups.GetAsync("1.2.3.4");
        await client.IpWarmups.StartAsync("1.2.3.4");
        await client.IpWarmups.StopAsync("1.2.3.4");

        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
        Assert.Equal(HttpMethod.Delete, handler.Requests[3].Method);
        Assert.All(handler.Requests, r => Assert.Contains("/v3/ip_warmups", r.Uri.AbsolutePath));
    }
}
