using System.Net;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class DomainsServiceFullTests
{
    [Fact]
    public async Task List_paginates_with_options()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"name\":\"mg.example.com\"}],\"paging\":{},\"total_count\":1}");

        var page = await client.Domains.ListAsync(new() { Limit = 25, Skip = 0, Filter = "mg", State = "active" });

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v4/domains", req.Uri.AbsolutePath);
        var q = req.Uri.Query.TrimStart('?');
        Assert.Contains("limit=25", q, StringComparison.Ordinal);
        Assert.Contains("skip=0", q, StringComparison.Ordinal);
        Assert.Contains("filter=mg", q, StringComparison.Ordinal);
        Assert.Contains("state=active", q, StringComparison.Ordinal);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task ListAll_iterates_pages()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"name\":\"a\"}],\"paging\":{\"next\":\"https://api.mailgun.test/v4/domains?skip=1\"}}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[{\"name\":\"b\"}],\"paging\":{}}");

        var names = new List<string>();
        await foreach (var d in client.Domains.ListAllAsync())
        {
            names.Add(d.Name);
        }
        Assert.Equal(new[] { "a", "b" }, names);
    }

    [Fact]
    public async Task Update_puts_domain_form()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"domain\":{\"name\":\"mg.example.com\",\"web_scheme\":\"https\"}}");

        await client.Domains.UpdateAsync("mg.example.com", new()
        {
            SpamAction = "tag",
            Wildcard = false,
            WebScheme = "https",
            UseAutomaticSenderSecurity = true,
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v4/domains/mg.example.com", req.Uri.AbsolutePath);
        Assert.Contains("spam_action=tag", req.Body!, StringComparison.Ordinal);
        Assert.Contains("wildcard=no", req.Body!, StringComparison.Ordinal);
        Assert.Contains("web_scheme=https", req.Body!, StringComparison.Ordinal);
        Assert.Contains("use_automatic_sender_security=yes", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTracking_returns_typed_settings()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"open\":{\"active\":true},\"click\":{\"active\":\"htmlonly\"},\"unsubscribe\":{\"active\":false,\"html_footer\":\"<a>x</a>\"}}");

        var t = await client.Domains.GetTrackingAsync("mg.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/domains/mg.example.com/tracking", req.Uri.AbsolutePath);
        Assert.True(t.Open!.Active);
        Assert.Equal("htmlonly", t.Click!.Active);
        Assert.Equal("<a>x</a>", t.Unsubscribe!.HtmlFooter);
    }

    [Fact]
    public async Task UpdateClickTracking_puts_active_string()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Domains.UpdateClickTrackingAsync("mg.example.com", "htmlonly");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v3/domains/mg.example.com/tracking/click", req.Uri.AbsolutePath);
        Assert.Equal("active=htmlonly", req.Body);
    }

    [Fact]
    public async Task UpdateUnsubscribeTracking_puts_active_and_optional_footers()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Domains.UpdateUnsubscribeTrackingAsync("mg.example.com", true,
            htmlFooter: "<a>x</a>", textFooter: "x");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/domains/mg.example.com/tracking/unsubscribe", req.Uri.AbsolutePath);
        Assert.Contains("active=yes", req.Body!, StringComparison.Ordinal);
        Assert.Contains("html_footer=", req.Body!, StringComparison.Ordinal);
        Assert.Contains("text_footer=x", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListSmtpCredentials_and_CRUD()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"login\":\"a@d\",\"state\":\"active\"}],\"paging\":{},\"total_count\":1}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Domains.ListSmtpCredentialsAsync("mg.example.com", limit: 5, skip: 0);
        await client.Domains.CreateSmtpCredentialAsync("mg.example.com", "user@mg.example.com", "secret");
        await client.Domains.UpdateSmtpCredentialAsync("mg.example.com", "user@mg.example.com", "newpw");
        await client.Domains.DeleteSmtpCredentialAsync("mg.example.com", "user@mg.example.com");

        Assert.EndsWith("/v3/domains/mg.example.com/credentials", handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Contains("login=user%40mg.example.com", handler.Requests[1].Body!, StringComparison.Ordinal);
        Assert.Contains("password=secret", handler.Requests[1].Body!, StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Put, handler.Requests[2].Method);
        Assert.Equal("password=newpw", handler.Requests[2].Body);
        Assert.Equal(HttpMethod.Delete, handler.Requests[3].Method);
        Assert.EndsWith("/v3/domains/mg.example.com/credentials/user%40mg.example.com", handler.Requests[3].Uri.AbsolutePath);
    }

    [Fact]
    public async Task UpdateConnectionSettings_puts_form_with_tls_flags()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Domains.UpdateConnectionSettingsAsync("mg.example.com", requireTls: true, skipVerification: false);

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v3/domains/mg.example.com/connection", req.Uri.AbsolutePath);
        Assert.Contains("require_tls=yes", req.Body!, StringComparison.Ordinal);
        Assert.Contains("skip_verification=no", req.Body!, StringComparison.Ordinal);
    }
}
