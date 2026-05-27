using System.Net;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

/// <summary>
/// Tests for the v0.9.3 Bounce Classification config + stats surface — 7 endpoints under
/// <c>/v1/bounce-classification</c> covering entity/rule catalogs, account-wide and per-domain
/// per-entity per-rule stats, and the bounce-event log.
/// </summary>
public class V093EndpointsTests
{
    [Fact]
    public async Task ListConfigEntities_gets_config_entities_as_id_map()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"hard-bounce\":{\"name\":\"Hard Bounce\"},\"soft-bounce\":{\"name\":\"Soft Bounce\"}}");

        var resp = await client.BounceClassification.ListConfigEntitiesAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v1/bounce-classification/config/entities", req.Uri.AbsolutePath);
        Assert.Equal(2, resp.Count);
        Assert.Equal("Hard Bounce", resp["hard-bounce"].Name);
    }

    [Fact]
    public async Task ListConfigRules_gets_rules_with_entity_id_and_class()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"r1\":{\"entity_id\":\"hard-bounce\",\"class\":\"550\",\"short-explanation\":\"mailbox missing\"}}");

        var resp = await client.BounceClassification.ListConfigRulesAsync();

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/bounce-classification/config/rules", req.Uri.AbsolutePath);
        Assert.Equal("hard-bounce", resp["r1"].EntityId);
        Assert.Equal("550", resp["r1"].Class);
        Assert.Equal("mailbox missing", resp["r1"].ShortExplanation);
    }

    [Fact]
    public async Task ListDomainStats_passes_filter_params_and_parses_items()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"domain\":{\"name\":\"mg.x.com\"},\"bounced\":{\"total\":42}}],\"total\":1," +
            "\"req\":{\"limit\":50}}");

        var resp = await client.BounceClassification.ListDomainStatsAsync(
            limit: 50, skip: 10, query: "mg.", includeSubaccounts: true);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/bounce-classification/domains", req.Uri.AbsolutePath);
        Assert.Contains("limit=50", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("skip=10", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("query=mg.", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("include_subaccounts=yes", req.Uri.Query, StringComparison.Ordinal);

        Assert.Equal(1, resp.Total);
        var item = Assert.Single(resp.Items!);
        Assert.Equal("mg.x.com", item.Domain!.Name);
        Assert.Equal(42, item.Bounced!.Total);
    }

    [Fact]
    public async Task ListDomainEntityStats_gets_per_entity_breakdown_for_domain()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"entity-id\":\"hard-bounce\",\"entity-name\":\"Hard Bounce\",\"bounced\":{\"total\":17}}]}");

        var resp = await client.BounceClassification.ListDomainEntityStatsAsync(
            "mg.example.com", includeSubaccounts: false);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/bounce-classification/domains/mg.example.com/entities", req.Uri.AbsolutePath);
        Assert.Contains("include_subaccounts=no", req.Uri.Query, StringComparison.Ordinal);
        var item = Assert.Single(resp.Items!);
        Assert.Equal("hard-bounce", item.EntityId);
        Assert.Equal(17, item.Bounced!.Total);
    }

    [Fact]
    public async Task ListEntityRuleStats_gets_per_rule_breakdown_for_entity()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"rule-id\":\"r1\",\"category\":\"hard\",\"severity\":\"permanent\"," +
            "\"sample-text\":\"550 No such user\",\"explanation\":\"mailbox missing\"," +
            "\"bounced\":{\"total\":5}}]}");

        var resp = await client.BounceClassification.ListEntityRuleStatsAsync(
            "mg.example.com", "hard-bounce", includeSubaccounts: true);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith(
            "/v1/bounce-classification/domains/mg.example.com/entities/hard-bounce/rules",
            req.Uri.AbsolutePath);
        var item = Assert.Single(resp.Items!);
        Assert.Equal("r1", item.RuleId);
        Assert.Equal("permanent", item.Severity);
        Assert.Equal(5, item.Bounced!.Total);
    }

    [Fact]
    public async Task ListDomainEvents_passes_hyphenated_query_keys_per_spec()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"paging\":{}}");

        await client.BounceClassification.ListDomainEventsAsync(
            "mg.example.com",
            ruleId: "r1",
            entityId: "hard-bounce",
            sort: "timestamp:desc",
            pageCursor: "abc",
            limit: 100);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/bounce-classification/domains/mg.example.com/events", req.Uri.AbsolutePath);
        // Spec uses hyphenated query param names — verify the wire preserves them exactly.
        Assert.Contains("rule-id=r1", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("entity-id=hard-bounce", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("sort=timestamp%3Adesc", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("page=abc", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("limit=100", req.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAccountStats_gets_stats_endpoint_with_group_filter()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"domain\":{\"name\":\"mg.x.com\"},\"rule-id\":\"r1\"," +
            "\"entity-id\":\"hard-bounce\",\"short-explanation\":\"mailbox missing\"," +
            "\"bounced\":{\"total\":99}}],\"_duration\":\"42ms\"}");

        var resp = await client.BounceClassification.ListAccountStatsAsync(
            group: "domain", limit: 25, includeSubaccounts: false);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/bounce-classification/stats", req.Uri.AbsolutePath);
        Assert.Contains("group=domain", req.Uri.Query, StringComparison.Ordinal);
        Assert.Contains("limit=25", req.Uri.Query, StringComparison.Ordinal);
        Assert.Equal("42ms", resp.Duration);
        var item = Assert.Single(resp.Items!);
        Assert.Equal(99, item.Bounced!.Total);
        Assert.Equal("hard-bounce", item.EntityId);
    }

    [Fact]
    public async Task BounceClassification_new_methods_validate_blank_path_segments()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.BounceClassification.ListDomainEntityStatsAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.BounceClassification.ListEntityRuleStatsAsync(" ", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.BounceClassification.ListEntityRuleStatsAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.BounceClassification.ListDomainEventsAsync(""));
    }
}
