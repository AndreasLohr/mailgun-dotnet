using System.Net;
using Mailgun.Models.Domains;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class DomainsServiceTests
{
    [Fact]
    public async Task Get_hits_v4_domains_with_escaped_name()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"domain\":{\"id\":\"d_1\",\"name\":\"mg.example.com\",\"state\":\"active\"}}");

        var resp = await client.Domains.GetAsync("mg.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v4/domains/mg.example.com", req.Uri.AbsolutePath);
        Assert.Equal("mg.example.com", resp.Domain.Name);
        Assert.Equal("active", resp.Domain.State);
    }

    [Fact]
    public async Task Create_posts_form_with_name_and_optional_fields()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"domain\":{\"id\":\"d_1\",\"name\":\"mg.example.com\"}}");

        await client.Domains.CreateAsync(new CreateDomainRequest
        {
            Name = "mg.example.com",
            SmtpPassword = "secret",
            SpamAction = "tag",
            Wildcard = true,
            ForceDkimAuthority = false,
            DkimKeySize = 2048,
            Ips = { "1.2.3.4", "5.6.7.8" },
            PoolId = "pool_x",
            WebScheme = "https",
            UseAutomaticSenderSecurity = true,
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v4/domains", req.Uri.AbsolutePath);
        Assert.Equal("application/x-www-form-urlencoded", req.ContentType);
        Assert.Contains("name=mg.example.com", req.Body, StringComparison.Ordinal);
        Assert.Contains("smtp_password=secret", req.Body, StringComparison.Ordinal);
        Assert.Contains("spam_action=tag", req.Body, StringComparison.Ordinal);
        Assert.Contains("wildcard=yes", req.Body, StringComparison.Ordinal);
        Assert.Contains("force_dkim_authority=no", req.Body, StringComparison.Ordinal);
        Assert.Contains("dkim_key_size=2048", req.Body, StringComparison.Ordinal);
        Assert.Contains("ips=1.2.3.4%2C5.6.7.8", req.Body, StringComparison.Ordinal);
        Assert.Contains("web_scheme=https", req.Body, StringComparison.Ordinal);
        Assert.Contains("use_automatic_sender_security=yes", req.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_uses_PUT_to_verify_subpath()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"domain\":{\"name\":\"x\"}}");

        await client.Domains.VerifyAsync("mg.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v4/domains/mg.example.com/verify", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task Delete_uses_v3_path_for_backward_compatibility()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"deleted\"}");

        await client.Domains.DeleteAsync("mg.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v3/domains/mg.example.com", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task UpdateOpenTracking_puts_active_yes_or_no()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Domains.UpdateOpenTrackingAsync("d", active: true);
        await client.Domains.UpdateOpenTrackingAsync("d", active: false);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("active=yes", handler.Requests[0].Body);
        Assert.Equal("active=no", handler.Requests[1].Body);
        Assert.All(handler.Requests, r => Assert.EndsWith("/tracking/open", r.Uri.AbsolutePath));
    }
}
