using System.Net;
using Mailgun.Services;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class DkimServicesTests
{
    // ── DKIM Keys ──

    [Fact]
    public async Task DkimKeys_ListAll_hits_v1_dkim_keys_with_filter()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"selector\":\"mta\",\"signing_domain\":\"mg.example.com\",\"activated\":true}],\"total_count\":1}");

        var resp = await client.DkimKeys.ListAllAsync(limit: 25, signingDomain: "mg.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/dkim/keys", req.Uri.AbsolutePath);
        var q = req.Uri.Query.TrimStart('?');
        Assert.Contains("limit=25", q, StringComparison.Ordinal);
        Assert.Contains("signing_domain=mg.example.com", q, StringComparison.Ordinal);
        Assert.Single(resp.Items!);
        Assert.True(resp.Items![0].Activated);
    }

    [Fact]
    public async Task DkimKeys_Create_legacy_posts_json_to_v1_dkim_keys()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        await client.DkimKeys.CreateAsync(new CreateDkimKeyForSigningDomainRequest
        {
            SigningDomain = "mg.example.com",
            Selector = "mta",
            Bits = 2048,
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("application/json", req.ContentType);
        Assert.EndsWith("/v1/dkim/keys", req.Uri.AbsolutePath);
        Assert.Contains("\"signing_domain\":\"mg.example.com\"", req.Body!, StringComparison.Ordinal);
        Assert.Contains("\"selector\":\"mta\"", req.Body!, StringComparison.Ordinal);
        Assert.Contains("\"bits\":2048", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DkimKeys_Delete_passes_signing_domain_and_selector_as_query()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.DkimKeys.DeleteAsync("mg.example.com", "mta");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.Equal("/v1/dkim/keys", req.Uri.AbsolutePath);
        Assert.Contains("signing_domain=mg.example.com", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("selector=mta", req.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DkimKeys_ListForAuthority_and_CreateForAuthority_use_v4_paths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"selector\":\"mta\",\"signing_domain\":\"a.com\"}");

        await client.DkimKeys.ListForAuthorityAsync("a.com");
        await client.DkimKeys.CreateForAuthorityAsync("a.com", new CreateDkimKeyRequest
        {
            SigningDomain = "a.com",
            Selector = "mta",
            Bits = 1024,
        });

        Assert.EndsWith("/v4/domains/a.com/keys", handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.EndsWith("/v4/domains/a.com/keys", handler.Requests[1].Uri.AbsolutePath);
        Assert.Contains("\"bits\":1024", handler.Requests[1].Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DkimKeys_Activate_puts_activate_yes_or_no()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"message\":\"domain key activated\",\"authority\":\"a.com\",\"selector\":\"mta\",\"active\":true}");
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"message\":\"domain key deactivated\",\"authority\":\"a.com\",\"selector\":\"mta\",\"active\":false}");

        var activated = await client.DkimKeys.ActivateForAuthorityAsync("a.com", "mta");
        var deactivated = await client.DkimKeys.DeactivateForAuthorityAsync("a.com", "mta");

        // Mailgun docs: PUT /v4/domains/{authority}/keys/{selector}/activate (and /deactivate), empty body.
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Put, r.Method));
        Assert.EndsWith("/v4/domains/a.com/keys/mta/activate", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v4/domains/a.com/keys/mta/deactivate", handler.Requests[1].Uri.AbsolutePath);
        Assert.Equal(string.Empty, handler.Requests[0].Body);
        Assert.Equal(string.Empty, handler.Requests[1].Body);
        Assert.True(activated.Active);
        Assert.False(deactivated.Active);
    }

    [Fact]
    public async Task DkimKeys_DeleteForAuthority_deletes_selector_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.DkimKeys.DeleteForAuthorityAsync("a.com", "mta");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v4/domains/a.com/keys/mta", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task DkimKeys_ListAll_passes_selector_and_page_query_params()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        await client.DkimKeys.ListAllAsync(limit: 10, signingDomain: "mg.example.com", selector: "mta", page: "abc123");

        var req = Assert.Single(handler.Requests);
        var q = req.Uri.Query.TrimStart('?');
        Assert.Contains("limit=10", q, StringComparison.Ordinal);
        Assert.Contains("signing_domain=mg.example.com", q, StringComparison.Ordinal);
        Assert.Contains("selector=mta", q, StringComparison.Ordinal);
        Assert.Contains("page=abc123", q, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DkimKeys_Create_legacy_includes_pem_when_supplied()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        await client.DkimKeys.CreateAsync(new CreateDkimKeyForSigningDomainRequest
        {
            SigningDomain = "mg.example.com",
            Selector = "mta",
            Pem = "-----BEGIN PRIVATE KEY-----\nABCD\n-----END PRIVATE KEY-----",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Contains("\"pem\":\"-----BEGIN PRIVATE KEY-----", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DkimKeys_Create_legacy_omits_pem_when_null()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        await client.DkimKeys.CreateAsync(new CreateDkimKeyForSigningDomainRequest
        {
            SigningDomain = "mg.example.com",
            Selector = "mta",
            Bits = 2048,
        });

        var req = Assert.Single(handler.Requests);
        Assert.DoesNotContain("pem", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DkimKeys_UpdateDkimAuthority_puts_multipart_self_to_v3_endpoint()
    {
        // Mailgun's PUT /v3/domains/{name}/dkim_authority is documented multipart/form-data only.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.DkimKeys.UpdateDkimAuthorityAsync("mg.example.com", self: true);
        await client.DkimKeys.UpdateDkimAuthorityAsync("mg.example.com", self: false);

        Assert.All(handler.Requests, r =>
        {
            Assert.Equal(HttpMethod.Put, r.Method);
            Assert.EndsWith("/v3/domains/mg.example.com/dkim_authority", r.Uri.AbsolutePath);
            Assert.Equal("multipart/form-data", r.ContentType);
            Assert.Contains("self", r.Body!, StringComparison.Ordinal);
        });
        Assert.Contains("true", handler.Requests[0].Body!, StringComparison.Ordinal);
        Assert.Contains("false", handler.Requests[1].Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DkimKeys_UpdateDkimSelector_puts_multipart_dkim_selector_to_v3_endpoint()
    {
        // Mailgun's PUT /v3/domains/{name}/dkim_selector is documented multipart/form-data only.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.DkimKeys.UpdateDkimSelectorAsync("mg.example.com", "newsel");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v3/domains/mg.example.com/dkim_selector", req.Uri.AbsolutePath);
        Assert.Equal("multipart/form-data", req.ContentType);
        Assert.Contains("dkim_selector", req.Body!, StringComparison.Ordinal);
        Assert.Contains("newsel", req.Body!, StringComparison.Ordinal);
    }

    // ── DKIM Security ──

    [Fact]
    public async Task DkimSecurity_Rotate_posts_to_domains_rotate_subpath()
    {
        // Mailgun's documented endpoint is POST /v1/dkim_management/domains/{name}/rotate,
        // NOT PUT /v1/dkim_management/{domain}/rotate-dkim-key (which was a fabricated URL).
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.DkimSecurity.RotateAsync("mg.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v1/dkim_management/domains/mg.example.com/rotate", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task DkimSecurity_SetAutoRotation_emits_literal_true_false_and_optional_interval()
    {
        // PUT /v1/dkim_management/domains/{name}/rotation — multipart/form-data.
        // Mailgun rejects "yes"/"no" on this endpoint, so the SDK must emit literal "true"/"false".
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.NoContent, string.Empty);
        handler.EnqueueResponse(HttpStatusCode.NoContent, string.Empty);

        await client.DkimSecurity.SetAutoRotationAsync("mg.example.com", rotationEnabled: false, rotationInterval: "30d");
        await client.DkimSecurity.SetAutoRotationAsync("mg.example.com", rotationEnabled: true);

        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.EndsWith("/v1/dkim_management/domains/mg.example.com/rotation", handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal("multipart/form-data", handler.Requests[0].ContentType);
        Assert.Contains("rotation_enabled", handler.Requests[0].Body!, StringComparison.Ordinal);
        Assert.Contains("false", handler.Requests[0].Body!, StringComparison.Ordinal);
        Assert.Contains("rotation_interval", handler.Requests[0].Body!, StringComparison.Ordinal);
        Assert.Contains("30d", handler.Requests[0].Body!, StringComparison.Ordinal);

        Assert.Equal("multipart/form-data", handler.Requests[1].ContentType);
        Assert.Contains("rotation_enabled", handler.Requests[1].Body!, StringComparison.Ordinal);
        Assert.Contains("true", handler.Requests[1].Body!, StringComparison.Ordinal);
        Assert.DoesNotContain("rotation_interval", handler.Requests[1].Body!, StringComparison.Ordinal);
    }
}
