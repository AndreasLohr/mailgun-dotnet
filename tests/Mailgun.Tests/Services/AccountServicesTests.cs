using System.Net;
using Mailgun.Services;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class AccountServicesTests
{
    // ──────────── Subaccounts ────────────

    [Fact]
    public async Task Subaccounts_List_paginates_with_filter()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"subaccounts\":[{\"id\":\"a\"}],\"total\":1}");

        var resp = await client.Subaccounts.ListAsync(limit: 25, skip: 0, filter: "active");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v5/accounts/subaccounts", req.Uri.AbsolutePath);
        Assert.Contains("limit=25", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("filter=active", req.Uri.Query, StringComparison.Ordinal);
        Assert.NotNull(resp.Subaccounts);
        Assert.Single(resp.Subaccounts!);
    }

    [Fact]
    public async Task Subaccounts_Create_posts_json_with_name()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"acct_1\",\"name\":\"Mine\"}");

        var s = await client.Subaccounts.CreateAsync("Mine");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("application/json", req.ContentType);
        Assert.Contains("\"name\":\"Mine\"", req.Body!, StringComparison.Ordinal);
        Assert.Equal("acct_1", s.Id);
    }

    [Fact]
    public async Task Subaccounts_typed_Create_locks_wire_format_via_JsonPropertyName()
    {
        // Regression for the original "new { name }" anonymous-type fragility: with the typed
        // CreateSubaccountRequest, the JSON field is tied to [JsonPropertyName("name")] rather
        // than the C# property name, so future renames can't silently break the wire format.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"acct_2\",\"name\":\"Typed\"}");

        await client.Subaccounts.CreateAsync(new CreateSubaccountRequest { Name = "Typed" });

        var req = Assert.Single(handler.Requests);
        Assert.Contains("\"name\":\"Typed\"", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Subaccounts_typed_Create_rejects_blank_name()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Subaccounts.CreateAsync(new CreateSubaccountRequest { Name = "" }));
    }

    [Fact]
    public async Task Subaccounts_typed_Update_calls_PUT_with_typed_body()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"acct_1\",\"name\":\"Renamed\"}");

        await client.Subaccounts.UpdateAsync("acct_1", new UpdateSubaccountRequest { Name = "Renamed" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v5/accounts/subaccounts/acct_1", req.Uri.AbsolutePath);
        Assert.Contains("\"name\":\"Renamed\"", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Subaccounts_Update_puts_json_with_name()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"acct_1\",\"name\":\"New\"}");

        await client.Subaccounts.UpdateAsync("acct_1", "New");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v5/accounts/subaccounts/acct_1", req.Uri.AbsolutePath);
        Assert.Contains("\"name\":\"New\"", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Subaccounts_Enable_and_Disable_post_to_correct_subpaths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        await client.Subaccounts.EnableAsync("acct_1");
        await client.Subaccounts.DisableAsync("acct_1");

        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/enable", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/disable", handler.Requests[1].Uri.AbsolutePath);
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Post, r.Method));
    }

    [Fact]
    public async Task Subaccounts_GetMonthlyCustomLimit_returns_CustomLimit_not_Subaccount()
    {
        // Regression for the original return-type bug — must deserialize a CustomLimit envelope.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"limit\":10000,\"enabled\":true,\"usage\":42}");

        var cl = await client.Subaccounts.GetMonthlyCustomLimitAsync("acct_1");

        Assert.Equal(10000, cl.Limit);
        Assert.True(cl.Enabled);
        Assert.Equal(42, cl.Usage);
    }

    [Fact]
    public async Task Subaccounts_SetMonthlyCustomLimit_puts_json_with_limit()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        await client.Subaccounts.SetMonthlyCustomLimitAsync("acct_1", 5000);

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v5/accounts/subaccounts/acct_1/limit/custom/monthly", req.Uri.AbsolutePath);
        Assert.Contains("\"limit\":5000", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Subaccounts_GetFeatures_and_UpdateFeatures_round_trip_dictionary()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"features\":{\"foo\":true,\"bar\":false}}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"features\":{\"foo\":false}}");

        var f = await client.Subaccounts.GetFeaturesAsync("acct_1");
        await client.Subaccounts.UpdateFeaturesAsync("acct_1",
            new SubaccountFeatures { Features = new Dictionary<string, bool> { ["foo"] = false } });

        Assert.NotNull(f.Features);
        Assert.True(f.Features!["foo"]);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
        Assert.Contains("\"features\"", handler.Requests[1].Body!, StringComparison.Ordinal);
    }

    // ──────────── CustomMessageLimit ────────────

    [Fact]
    public async Task CustomMessageLimit_Get_Set_Enable_Disable_use_correct_paths_and_methods()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"limit\":1000,\"enabled\":true}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        var cl = await client.CustomMessageLimit.GetAsync();
        await client.CustomMessageLimit.SetAsync(2000);
        await client.CustomMessageLimit.EnableAsync();
        await client.CustomMessageLimit.DisableAsync();

        Assert.Equal(1000, cl.Limit);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[3].Method);
        Assert.EndsWith("/limit/custom/monthly", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/enable", handler.Requests[2].Uri.AbsolutePath);
        Assert.EndsWith("/disable", handler.Requests[3].Uri.AbsolutePath);
    }

    // ──────────── Account ────────────

    [Fact]
    public async Task Account_Update_puts_account_object_to_v5_accounts()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"a_1\",\"name\":\"Updated\"}");

        var a = await client.Account.UpdateAsync(new Account { Name = "Updated" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v5/accounts", req.Uri.AbsolutePath);
        Assert.Equal("Updated", a.Name);
    }

    [Fact]
    public async Task Account_GetHttpSigningKey_and_Rotate_use_different_methods()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"http_signing_key\":\"k1\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"http_signing_key\":\"k2\"}");

        var got = await client.Account.GetHttpSigningKeyAsync();
        var rotated = await client.Account.RotateHttpSigningKeyAsync();

        Assert.Equal("k1", got.Key);
        Assert.Equal("k2", rotated.Key);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.All(handler.Requests, r => Assert.EndsWith("/http_signing_key", r.Uri.AbsolutePath));
    }

    [Fact]
    public async Task Account_AddRemoveSandboxAuthRecipient_use_email_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        await client.Account.AddSandboxAuthRecipientAsync("a@b.com");
        await client.Account.RemoveSandboxAuthRecipientAsync("a@b.com");

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.EndsWith("/v5/sandbox/auth_recipients", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v5/sandbox/auth_recipients/a%40b.com", handler.Requests[1].Uri.AbsolutePath);
    }

    [Fact]
    public async Task Account_ResendActivationEmail_posts_empty_body()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        await client.Account.ResendActivationEmailAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v5/accounts/resend_activation_email", req.Uri.AbsolutePath);
    }

    // ──────────── Users ────────────

    [Fact]
    public async Task Users_CRUD_uses_v5_users_paths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"u\",\"email\":\"a@b\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"u\",\"email\":\"a@b\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"u\",\"email\":\"a@b\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        await client.Users.ListAsync(limit: 10);
        await client.Users.GetAsync("u");
        await client.Users.CreateAsync(new CreateUserRequest { Email = "a@b", Role = "admin" });
        await client.Users.UpdateAsync("u", new UpdateUserRequest { Role = "support" });
        await client.Users.DeleteAsync("u");

        Assert.Equal(5, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Contains("/v5/users", r.Uri.AbsolutePath));
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
        Assert.Equal(HttpMethod.Put, handler.Requests[3].Method);
        Assert.Equal(HttpMethod.Delete, handler.Requests[4].Method);
        Assert.Contains("\"email\":\"a@b\"", handler.Requests[2].Body!, StringComparison.Ordinal);
        Assert.Contains("\"role\":\"support\"", handler.Requests[3].Body!, StringComparison.Ordinal);
    }
}
