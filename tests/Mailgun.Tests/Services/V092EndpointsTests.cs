using System.Net;
using Mailgun.Services;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

/// <summary>
/// Tests for the v0.9.2 endpoint additions:
///   - 9 one-offs (account features, tag-limits, IP bulk ops, template alternate verbs, custom-limit cleanups)
///   - 4 subaccount DIPP delegation ops
///   - 16 dynamic IP pools v3 ops (including the v1 → v3 path migration for ListAsync)
///   - 11 modern alerts settings ops
/// Each test pins method + URL + at least one piece of the request/response wire shape.
/// </summary>
public class V092EndpointsTests
{
    // ============================================================================================
    // One-offs
    // ============================================================================================

    [Fact]
    public async Task UpdateAccountFeature_puts_form_with_named_flag()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"success\":true}");

        await client.Account.UpdateFeatureAsync("ai_insights", true);

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v5/accounts/features", req.Uri.AbsolutePath);
        Assert.Equal("application/x-www-form-urlencoded", req.ContentType);
        // SDK encodes bools as Mailgun's yes/no convention rather than JSON true/false.
        Assert.Contains("ai_insights=yes", req.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTagLimits_gets_v3_domains_limits_tag_and_parses_count()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"l1\",\"limit\":4000,\"count\":12}");

        var resp = await client.Domains.GetTagLimitsAsync("mg.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v3/domains/mg.example.com/limits/tag", req.Uri.AbsolutePath);
        Assert.Equal(4000, resp.Limit);
        Assert.Equal(12, resp.Count);
    }

    [Fact]
    public async Task DetachIpFromAllDomains_deletes_with_alternative_in_query()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"queued\",\"reference_id\":\"ref-1\"}");

        var resp = await client.Ips.DetachIpFromAllDomainsAsync("10.0.0.1", "10.0.0.2");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v3/ips/10.0.0.1/domains", req.Uri.AbsolutePath);
        Assert.Contains("alternative=10.0.0.2", req.Uri.Query, StringComparison.Ordinal);
        Assert.Equal("ref-1", resp.ReferenceId);
    }

    [Fact]
    public async Task ListAllDetailedIps_supports_all_filter_params()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[{\"ip\":\"10.0.0.1\"}],\"total_count\":1}");

        var resp = await client.Ips.ListAllDetailedAsync(
            limit: 50, skip: 10, poolId: "p1", domainId: "d1", subaccountId: "s1",
            ip: "10.0.0.1", sortBy: "ip", sortOrder: "descending");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/ips/details/all", req.Uri.AbsolutePath);
        Assert.Contains("limit=50", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("pool_id=p1", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("sort_order=descending", req.Uri.Query, StringComparison.Ordinal);
        Assert.Equal(1L, resp.TotalCount);
    }

    [Fact]
    public async Task Templates_BatchCopy_puts_json_with_requests_array()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"copied\",\"failed_copies\":[]}");

        var resp = await client.Templates.BatchCopyAsync("src-template",
            new[] { new TemplateCopyRequest { TargetTemplateName = "dst-1", Description = "d1" } });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v4/templates/src-template/copy", req.Uri.AbsolutePath);
        Assert.Contains("\"target_template_name\":\"dst-1\"", req.Body!, StringComparison.Ordinal);
        Assert.NotNull(resp.FailedCopies);
        Assert.Empty(resp.FailedCopies);
    }

    [Fact]
    public async Task Templates_BatchCopy_requires_at_least_one_target()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Templates.BatchCopyAsync("src", Array.Empty<TemplateCopyRequest>()));
    }

    [Fact]
    public async Task Templates_RenameByPath_puts_with_new_name_in_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"renamed\",\"template\":{\"name\":\"new-name\"}}");

        var t = await client.Templates.RenameByPathAsync("old-name", "new-name");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v4/templates/old-name/rename/new-name", req.Uri.AbsolutePath);
        Assert.Equal("new-name", t.Name);
    }

    [Fact]
    public async Task Templates_CopyVersion_puts_with_comment_in_query()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"message\":\"copied\",\"template\":{\"name\":\"t\",\"version\":{\"tag\":\"v2\"}}}");

        var v = await client.Templates.CopyVersionAsync("t", "v1", "v2", comment: "minor tweak");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v4/templates/t/versions/v1/copy/v2", req.Uri.AbsolutePath);
        Assert.Contains("comment=minor%20tweak", req.Uri.Query, StringComparison.Ordinal);
        Assert.Equal("v2", v.Tag);
    }

    [Fact]
    public async Task CustomMessageLimit_Delete_deletes_monthly_endpoint()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"success\":true}");

        await client.CustomMessageLimit.DeleteAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v5/accounts/limit/custom/monthly", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task CustomMessageLimit_ReEnableSending_puts_modern_enable_endpoint()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"success\":true}");

        await client.CustomMessageLimit.ReEnableSendingAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v5/accounts/limit/custom/enable", req.Uri.AbsolutePath);
    }

    // ============================================================================================
    // Subaccount DIPP delegation
    // ============================================================================================

    [Fact]
    public async Task DelegateIpPool_puts_form_with_pool_id_in_body()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"queued\",\"reference_id\":\"r1\"}");

        var resp = await client.Subaccounts.DelegateIpPoolAsync("acct_x", "pool_y");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v5/accounts/subaccounts/acct_x/ip_pool", req.Uri.AbsolutePath);
        Assert.Equal("application/x-www-form-urlencoded", req.ContentType);
        Assert.Contains("pool_id=pool_y", req.Body, StringComparison.Ordinal);
        Assert.Equal("r1", resp.ReferenceId);
    }

    [Fact]
    public async Task RevokeIpPoolDelegation_deletes_with_pool_id_in_query()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"queued\",\"reference_id\":\"r2\"}");

        var resp = await client.Subaccounts.RevokeIpPoolDelegationAsync("acct_x", "pool_y");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v5/accounts/subaccounts/acct_x/ip_pool", req.Uri.AbsolutePath);
        Assert.Contains("pool_id=pool_y", req.Uri.Query, StringComparison.Ordinal);
        Assert.Equal("r2", resp.ReferenceId);
    }

    [Fact]
    public async Task ListAllIpPoolDelegations_gets_ip_pools_all_endpoint()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[{\"pool_id\":\"p1\"}],\"total_count\":1}");

        var resp = await client.Subaccounts.ListAllIpPoolDelegationsAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v5/accounts/subaccounts/ip_pools/all", req.Uri.AbsolutePath);
        Assert.Equal(1L, resp.TotalCount);
    }

    [Fact]
    public async Task DeleteMonthlyCustomLimit_deletes_subaccount_monthly_endpoint()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"success\":true}");

        await client.Subaccounts.DeleteMonthlyCustomLimitAsync("acct_x");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v5/accounts/subaccounts/acct_x/limit/custom/monthly", req.Uri.AbsolutePath);
    }

    // ============================================================================================
    // Dynamic IP Pools v3
    // ============================================================================================

    [Fact]
    public async Task DynamicIpPools_List_now_uses_v3_path_not_v1()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"dynamic_pools\":[],\"total_count\":0}");

        await client.DynamicIpPools.ListAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        // The critical assertion: the v1 path the SDK used to call is gone.
        Assert.EndsWith("/v3/dynamic_pools", req.Uri.AbsolutePath);
        Assert.DoesNotContain("/v1/dynamic_pools", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task UpdatePoolIps_patches_with_add_ip_and_remove_ip_in_multipart()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "\"ok\"");

        await client.DynamicIpPools.UpdatePoolIpsAsync("warm-pool", "10.0.0.5", "10.0.0.6");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, req.Method);
        Assert.EndsWith("/v3/dynamic_pools/warm-pool", req.Uri.AbsolutePath);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
        Assert.Contains("10.0.0.5", req.Body, StringComparison.Ordinal);
        Assert.Contains("10.0.0.6", req.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddIpToPool_posts_to_pool_name_ip_segment_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "\"ok\"");

        await client.DynamicIpPools.AddIpToPoolAsync("warm-pool", "10.0.0.5");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/dynamic_pools/warm-pool/10.0.0.5", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task InitializeAllPools_posts_multipart_with_three_reputation_fields()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"initialized\"}");

        await client.DynamicIpPools.InitializeAllPoolsAsync("good-pool", "poor-pool", "new-pool");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/dynamic_pools/all", req.Uri.AbsolutePath);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
        Assert.Contains("good-pool", req.Body, StringComparison.Ordinal);
        Assert.Contains("poor-pool", req.Body, StringComparison.Ordinal);
        Assert.Contains("new-pool", req.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAllPools_deletes_v3_dynamic_pools_all()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"deleted\"}");

        await client.DynamicIpPools.DeleteAllPoolsAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v3/dynamic_pools/all", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task ListAssignableDomains_supports_optional_filters()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.DynamicIpPools.ListAssignableDomainsAsync(subaccountId: "sa-1", domain: "mg.x.com");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/domains/dynamic_pools/assignable", req.Uri.AbsolutePath);
        Assert.Contains("subaccount_id=sa-1", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("domain=mg.x.com", req.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnrollAllDomains_posts_with_include_subaccounts_in_query()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"enrolling\"}");

        await client.DynamicIpPools.EnrollAllDomainsAsync(includeSubaccounts: true);

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/domains/all/dynamic_pools/enroll", req.Uri.AbsolutePath);
        Assert.Contains("include_subaccounts=yes", req.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnrollDomain_posts_with_replacement_ip_in_query()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"enrolled\"}");

        await client.DynamicIpPools.EnrollDomainAsync("mg.example.com", "10.0.0.9");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/domains/mg.example.com/dynamic_pools", req.Uri.AbsolutePath);
        Assert.Contains("replacement_ip=10.0.0.9", req.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnenrollDomain_deletes_with_replacement_ip_and_pool_id_in_query()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"unenrolled\"}");

        await client.DynamicIpPools.UnenrollDomainAsync("mg.example.com", "10.0.0.9", "pool-x");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v3/domains/mg.example.com/dynamic_pools", req.Uri.AbsolutePath);
        Assert.Contains("replacement_ip=10.0.0.9", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("replacement_pool_id=pool-x", req.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveIpFromDomainPool_deletes_path_with_optional_replacements_in_query()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"removed\"}");

        await client.DynamicIpPools.RemoveIpFromDomainPoolAsync(
            "mg.example.com", "10.0.0.5", replacementIp: "10.0.0.7", replacementPoolId: "pool-y");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v3/domains/mg.example.com/pool/10.0.0.5", req.Uri.AbsolutePath);
        Assert.Contains("ip=10.0.0.7", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("pool_id=pool-y", req.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAssignedDomains_supports_sort_filters()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"domain_name\":\"mg.x.com\",\"pool_id\":\"p1\"}],\"total_items\":1," +
            "\"paging\":{\"first\":\"https://api.mailgun.test/x\"}}");

        var resp = await client.DynamicIpPools.ListAssignedDomainsAsync(
            limit: 50, sortBy: "bounce_rate", sortOrder: "descending");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/dynamic_pools/domains", req.Uri.AbsolutePath);
        Assert.Contains("sort_by=bounce_rate", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("sort_order=descending", req.Uri.Query, StringComparison.Ordinal);
        Assert.Single(resp.Items!);
    }

    [Fact]
    public async Task GetDomainHistory_gets_v1_dynamic_pools_history_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"id\":\"h1\",\"new_band\":\"good\",\"prev_band\":\"new\",\"reason\":\"low_bounces\"}");

        var resp = await client.DynamicIpPools.GetDomainHistoryAsync("mg.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/dynamic_pools/domains/mg.example.com/history", req.Uri.AbsolutePath);
        Assert.Equal("good", resp.NewBand);
        Assert.Equal("low_bounces", resp.Reason);
    }

    [Fact]
    public async Task GetDomainPreview_gets_preview_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"pool\":\"good-pool\"}");

        var resp = await client.DynamicIpPools.GetDomainPreviewAsync("mg.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/dynamic_pools/domains/mg.example.com/preview", req.Uri.AbsolutePath);
        Assert.NotNull(resp);
    }

    [Fact]
    public async Task GetAccountHistory_passes_capitalised_Limit_param_per_spec()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[],\"total_items\":0,\"paging\":{}}");

        await client.DynamicIpPools.GetAccountHistoryAsync(limit: 25, includeSubaccounts: true);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/dynamic_pools/history", req.Uri.AbsolutePath);
        // The spec uses Limit (PascalCase) — preserve it on the wire even though the rest of the
        // SDK normally uses snake_case query params.
        Assert.Contains("Limit=25", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("include_subaccounts=yes", req.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OverrideDomainAssignment_puts_multipart_with_pool_field()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        await client.DynamicIpPools.OverrideDomainAssignmentAsync("mg.example.com", "warm-pool");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v1/dynamic_pools/domains/mg.example.com/override", req.Uri.AbsolutePath);
        Assert.Contains("warm-pool", req.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveDomainOverride_deletes_override_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        await client.DynamicIpPools.RemoveDomainOverrideAsync("mg.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v1/dynamic_pools/domains/mg.example.com/override", req.Uri.AbsolutePath);
    }

    // ============================================================================================
    // Alerts settings (modern surface)
    // ============================================================================================

    [Fact]
    public async Task AddSettingsAlert_posts_json_with_event_type_channel_settings()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"id\":\"a1\",\"event_type\":\"send_failure\",\"channel\":\"email\"," +
            "\"settings\":{\"recipients\":[\"alice@example.com\"]}}");

        var resp = await client.Alerts.AddSettingsAlertAsync(new AlertSettingsEventRequest
        {
            EventType = "send_failure",
            Channel = "email",
            Settings = new Dictionary<string, object>
            {
                ["recipients"] = new[] { "alice@example.com" },
            },
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v1/alerts/settings/events", req.Uri.AbsolutePath);
        Assert.Equal("application/json", req.ContentType);
        Assert.Contains("\"event_type\":\"send_failure\"", req.Body!, StringComparison.Ordinal);
        Assert.Equal("a1", resp.Id);
    }

    [Fact]
    public async Task UpdateSettingsAlert_puts_to_id_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"updated\"}");

        await client.Alerts.UpdateSettingsAlertAsync("a1", new AlertSettingsEventRequest
        {
            EventType = "send_failure",
            Channel = "slack",
            Settings = new Dictionary<string, object> { ["channel_id"] = "C123" },
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v1/alerts/settings/events/a1", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task RemoveSettingsAlert_deletes_to_id_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"removed\"}");

        await client.Alerts.RemoveSettingsAlertAsync("a1");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v1/alerts/settings/events/a1", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task UpdateSlackSettings_puts_all_required_credentials()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.NoContent, null);

        await client.Alerts.UpdateSlackSettingsAsync(new SlackSettingsRequest
        {
            Token = "xoxb-tok",
            TeamId = "T1",
            TeamName = "Team",
            Scope = "channels:read",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v1/alerts/settings/slack", req.Uri.AbsolutePath);
        Assert.Contains("xoxb-tok", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteSlackSettings_deletes_settings_slack_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.NoContent, null);

        await client.Alerts.DeleteSlackSettingsAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v1/alerts/settings/slack", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task ResetWebhookSigningKey_puts_and_returns_new_key()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"signing_key\":\"new-sk-abc\"}");

        var resp = await client.Alerts.ResetWebhookSigningKeyAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v1/alerts/settings/webhooks/signing_key", req.Uri.AbsolutePath);
        Assert.Equal("new-sk-abc", resp.SigningKey);
    }

    [Fact]
    public async Task GetSlackChannel_gets_typed_channel_metadata()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"id\":\"C123\",\"name\":\"alerts\",\"is_archived\":false}");

        var resp = await client.Alerts.GetSlackChannelAsync("C123");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v1/alerts/slack/channels/C123", req.Uri.AbsolutePath);
        Assert.Equal("alerts", resp.Name);
        Assert.False(resp.IsArchived);
    }

    [Fact]
    public async Task RevokeSlackOAuth_deletes_oauth_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.NoContent, null);

        await client.Alerts.RevokeSlackOAuthAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v1/alerts/slack/oauth", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task SendEmailTest_posts_with_event_type_and_emails_array()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"sent\"}");

        await client.Alerts.SendEmailTestAsync("send_failure", new[] { "ops@example.com" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v1/alerts/email/test", req.Uri.AbsolutePath);
        Assert.Contains("\"event_type\":\"send_failure\"", req.Body!, StringComparison.Ordinal);
        Assert.Contains("ops@example.com", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendSlackTest_posts_with_optional_channel_ids()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"sent\"}");

        await client.Alerts.SendSlackTestAsync("send_failure", new[] { "C1", "C2" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v1/alerts/slack/test", req.Uri.AbsolutePath);
        Assert.Contains("\"channel_ids\":[\"C1\",\"C2\"]", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendWebhookTest_posts_with_event_type_and_url()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"sent\"}");

        await client.Alerts.SendWebhookTestAsync("send_failure", "https://hook.test/in");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v1/alerts/webhooks/test", req.Uri.AbsolutePath);
        Assert.Contains("https://hook.test/in", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Alerts_settings_methods_validate_required_args()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Alerts.SendEmailTestAsync("send_failure", Array.Empty<string>()));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.Alerts.AddSettingsAlertAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Alerts.RemoveSettingsAlertAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Alerts.GetSlackChannelAsync(""));
    }
}
