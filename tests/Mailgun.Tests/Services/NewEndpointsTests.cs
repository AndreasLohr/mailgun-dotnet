using System.Net;
using Mailgun.Models.Domains;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

/// <summary>
/// Tests for the 9 endpoints added in v0.9.1 to close documented Mailgun-API coverage gaps:
/// stored-message resend, v4 domain webhooks, public-key regenerate, bulk-delete SMTP creds,
/// threshold-hit listing, delete-all templates, IP allowlist CRUD, subaccount delete, pool-domains list.
/// Each test pins the method + URL + a representative piece of the request/response shape.
/// </summary>
public class NewEndpointsTests
{
    // ---- #1 stored-message resend ------------------------------------------------------------

    [Fact]
    public async Task ResendStored_posts_multipart_with_to_recipients_and_returns_id()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"<resent@mg>\",\"message\":\"Queued\"}");

        var resp = await client.Messages.ResendStoredAsync(
            "mg.example.com",
            "ABCDEF",
            new[] { "alice@example.com", "bob@example.com" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/domains/mg.example.com/messages/ABCDEF", req.Uri.AbsolutePath);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
        Assert.Contains("name=to", req.Body, StringComparison.Ordinal);
        Assert.Contains("alice@example.com", req.Body, StringComparison.Ordinal);
        Assert.Contains("bob@example.com", req.Body, StringComparison.Ordinal);
        Assert.Equal("<resent@mg>", resp.Id);
    }

    [Fact]
    public async Task ResendStored_requires_recipient()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Messages.ResendStoredAsync("d", "k", Array.Empty<string>()));
    }

    // ---- #2 v4 domain webhooks ---------------------------------------------------------------

    [Fact]
    public async Task CreateDomainWebhookV4_posts_form_with_url_and_event_types()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"webhooks\":{\"delivered\":{\"urls\":[\"https://hook.test\"]}}}");

        await client.Webhooks.CreateDomainWebhookV4Async(
            "mg.example.com",
            "https://hook.test/in",
            new[] { "delivered", "opened" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v4/domains/mg.example.com/webhooks", req.Uri.AbsolutePath);
        Assert.Equal("application/x-www-form-urlencoded", req.ContentType);
        Assert.Contains("url=https%3A%2F%2Fhook.test%2Fin", req.Body, StringComparison.Ordinal);
        Assert.Contains("event_types=delivered", req.Body, StringComparison.Ordinal);
        Assert.Contains("event_types=opened", req.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateDomainWebhookV4_uses_put_method()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"webhooks\":{}}");

        await client.Webhooks.UpdateDomainWebhookV4Async(
            "mg.example.com", "https://hook.test/in", new[] { "clicked" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v4/domains/mg.example.com/webhooks", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task DeleteDomainWebhooksV4_passes_urls_as_query_params()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "");

        await client.Webhooks.DeleteDomainWebhooksV4Async(
            "mg.example.com",
            new[] { "https://hook.test/a", "https://hook.test/b" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v4/domains/mg.example.com/webhooks", req.Uri.AbsolutePath);
        Assert.Contains("url=https%3A%2F%2Fhook.test%2Fa", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("url=https%3A%2F%2Fhook.test%2Fb", req.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteDomainWebhooksV4_requires_at_least_one_url()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Webhooks.DeleteDomainWebhooksV4Async("d", Array.Empty<string>()));
    }

    [Fact]
    public async Task CreateDomainWebhookV4_requires_at_least_one_event_type()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Webhooks.CreateDomainWebhookV4Async("d", "https://x.test", Array.Empty<string>()));
    }

    // ---- #3 regenerate public key ------------------------------------------------------------

    [Fact]
    public async Task RegeneratePublicKey_posts_v1_keys_public_and_returns_new_key()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"key\":\"pubkey-new\",\"message\":\"OK\"}");

        var resp = await client.Keys.RegeneratePublicKeyAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v1/keys/public", req.Uri.AbsolutePath);
        Assert.Equal("pubkey-new", resp.Key);
        Assert.Equal("OK", resp.Message);
    }

    // ---- #4 bulk SMTP credentials delete -----------------------------------------------------

    [Fact]
    public async Task DeleteAllSmtpCredentials_deletes_collection_and_returns_count()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"deleted\",\"count\":7}");

        var resp = await client.Domains.DeleteAllSmtpCredentialsAsync("mg.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v3/domains/mg.example.com/credentials", req.Uri.AbsolutePath);
        Assert.Equal(7, resp.Count);
        Assert.Equal("deleted", resp.Message);
    }

    // ---- #5 threshold-hits listing -----------------------------------------------------------

    [Fact]
    public async Task ListThresholdHits_gets_v1_thresholds_hits_and_parses_items()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"id\":\"h1\",\"name\":\"bounces-high\",\"triggered\":true," +
            "\"latest_value\":\"42\",\"metric\":\"bounces\",\"comparator\":\"gt\"," +
            "\"limit\":\"30\",\"dimension\":\"domain\",\"dimension_value\":\"x.com\"}]," +
            "\"total\":1}");

        var resp = await client.SendAlerts.ListHitsAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v1/thresholds/hits", req.Uri.AbsolutePath);
        Assert.Equal(1L, resp.Total);
        var hit = Assert.Single(resp.Items!);
        Assert.Equal("h1", hit.Id);
        Assert.True(hit.Triggered);
        Assert.Equal("42", hit.LatestValue);
        Assert.Equal("x.com", hit.DimensionValue);
    }

    // ---- #6 delete-all templates --------------------------------------------------------------

    [Fact]
    public async Task DeleteAllTemplates_deletes_v4_templates_collection()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"templates deleted\"}");

        await client.Templates.DeleteAllAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v4/templates", req.Uri.AbsolutePath);
    }

    // ---- #7 IP allowlist ----------------------------------------------------------------------

    [Fact]
    public async Task IpAllowlist_List_gets_v2_ip_whitelist_and_parses_addresses()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"addresses\":[{\"ip_address\":\"10.0.0.1\",\"description\":\"office\"}]}");

        var resp = await client.IpAllowlist.ListAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v2/ip_whitelist", req.Uri.AbsolutePath);
        var entry = Assert.Single(resp.Addresses!);
        Assert.Equal("10.0.0.1", entry.IpAddress);
        Assert.Equal("office", entry.Description);
    }

    [Fact]
    public async Task IpAllowlist_Add_posts_multipart_with_address_and_description()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"addresses\":[]}");

        await client.IpAllowlist.AddAsync("10.0.0.5", "ci-runner");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v2/ip_whitelist", req.Uri.AbsolutePath);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
        Assert.Contains("10.0.0.5", req.Body, StringComparison.Ordinal);
        Assert.Contains("ci-runner", req.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IpAllowlist_UpdateDescription_uses_put_method()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"addresses\":[]}");

        await client.IpAllowlist.UpdateDescriptionAsync("10.0.0.5", "new-desc");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v2/ip_whitelist", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task IpAllowlist_Delete_passes_address_as_query_param()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"addresses\":[]}");

        await client.IpAllowlist.DeleteAsync("10.0.0.5");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v2/ip_whitelist", req.Uri.AbsolutePath);
        Assert.Contains("address=10.0.0.5", req.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IpAllowlist_methods_validate_blank_address()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() => client.IpAllowlist.AddAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => client.IpAllowlist.UpdateDescriptionAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.IpAllowlist.DeleteAsync(""));
    }

    // ---- #8 subaccount delete ----------------------------------------------------------------

    [Fact]
    public async Task DeleteSubaccount_sends_on_behalf_of_header_and_no_id_in_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"deleted\"}");

        await client.Subaccounts.DeleteAsync("acct_xyz");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v5/accounts/subaccounts", req.Uri.AbsolutePath);
        // Subaccount id MUST go in the header, not the URL.
        Assert.DoesNotContain("acct_xyz", req.Uri.AbsolutePath);
        Assert.True(req.Headers.TryGetValue("X-Mailgun-On-Behalf-Of", out var v) && v == "acct_xyz",
            $"X-Mailgun-On-Behalf-Of header missing or wrong: {(req.Headers.TryGetValue("X-Mailgun-On-Behalf-Of", out var h) ? h : "<missing>")}");
    }

    [Fact]
    public async Task DeleteSubaccount_requires_id()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() => client.Subaccounts.DeleteAsync(" "));
    }

    // ---- #9 pool domains listing -------------------------------------------------------------

    [Fact]
    public async Task ListPoolDomains_gets_pool_domains_with_paging_cursor()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"domains\":[{\"id\":\"d1\",\"name\":\"a.com\"},{\"id\":\"d2\",\"name\":\"b.com\"}]," +
            "\"paging\":{\"first\":\"https://api.mailgun.test/v3/ip_pools/pool1/domains?page=abc\"," +
            "\"next\":\"https://api.mailgun.test/v3/ip_pools/pool1/domains?page=def\"}}");

        var resp = await client.IpPools.ListDomainsAsync("pool1", limit: 100, pageCursor: "abc");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v3/ip_pools/pool1/domains", req.Uri.AbsolutePath);
        Assert.Contains("limit=100", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("page=abc", req.Uri.Query, StringComparison.Ordinal);
        Assert.Equal(2, resp.Domains!.Count);
        Assert.Equal("a.com", resp.Domains[0].Name);
        Assert.NotNull(resp.Paging?.Next);
    }

    [Fact]
    public async Task ListPoolDomains_requires_pool_id()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() => client.IpPools.ListDomainsAsync(""));
    }
}
