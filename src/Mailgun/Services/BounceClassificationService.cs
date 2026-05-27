using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;

namespace Mailgun.Services;

/// <summary>
/// Operations on <c>/v1/bounce-classification</c> (catalog) and <c>/v2/bounce-classification/metrics</c>
/// (metrics roll-up grouped by classification code).
/// </summary>
public interface IBounceClassificationService
{
    /// <summary><c>GET /v1/bounce-classification</c> — list classification codes.</summary>
    Task<BounceClassificationListResponse> ListAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /v1/bounce-classification/{code}</c> — get a single classification entry.</summary>
    Task<BounceClassification> GetAsync(string code, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v2/bounce-classification/metrics</c> — aggregated bounce metrics by classification.</summary>
    Task<BounceClassificationMetricsResponse> QueryMetricsAsync(BounceClassificationMetricsRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v1/bounce-classification/{code}/codes</c> — list the SMTP/MTA codes that map to a Mailgun classification.</summary>
    Task<BounceClassificationCodesResponse> ListCodesAsync(string classificationCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v1/bounce-classification/classify</c> — classify a single delivery failure
    /// using its SMTP status + diagnostic message.
    /// </summary>
    Task<BounceClassification> ClassifyAsync(ClassifyBounceRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v1/bounce-classification/categories</c> — list classification categories with severities.</summary>
    Task<BounceClassificationListResponse> ListCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /v2/bounce-classification/metrics/dimensions</c> — list dimensions available to the metrics query.</summary>
    Task<BounceClassificationDimensionsResponse> ListDimensionsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /v2/bounce-classification/metrics/codes</c> — list Mailgun classification codes available in metrics queries.</summary>
    Task<BounceClassificationCodesResponse> ListMetricsCodesAsync(CancellationToken cancellationToken = default);

    // ---------- Config + per-domain stats surface ----------
    //
    // The /v1/bounce-classification/{config,domains,stats} endpoints are the modern data surface:
    // entity/rule definitions, plus rolled-up bounce counts at the account / per-domain / per-entity
    // level. They share no shape with the catalog endpoints above and have their own DTOs below.

    /// <summary>
    /// <c>GET /v1/bounce-classification/config/entities</c> — the catalog of classification entities
    /// (the high-level groupings rules roll up into). Returned as an id → entity map.
    /// </summary>
    Task<Dictionary<string, BounceClassificationEntity>> ListConfigEntitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v1/bounce-classification/config/rules</c> — the catalog of classification rules.
    /// Returned as an id → rule map.
    /// </summary>
    Task<Dictionary<string, BounceClassificationRule>> ListConfigRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v1/bounce-classification/domains</c> — list per-domain bounce statistics for the
    /// account. Supports pagination + an optional substring <paramref name="query"/> against the
    /// domain name and <paramref name="includeSubaccounts"/> for parent-scoped accounts.
    /// </summary>
    Task<BounceClassificationAccountStatsResponse> ListDomainStatsAsync(
        int? limit = null,
        int? skip = null,
        string? query = null,
        bool? includeSubaccounts = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v1/bounce-classification/domains/{domain}/entities</c> — per-entity bounce counts
    /// for one domain.
    /// </summary>
    Task<BounceClassificationDomainStatsResponse> ListDomainEntityStatsAsync(
        string domain,
        bool? includeSubaccounts = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v1/bounce-classification/domains/{domain}/entities/{entityId}/rules</c> — per-rule
    /// bounce counts inside one entity, for one domain.
    /// </summary>
    Task<BounceClassificationEntityStatsResponse> ListEntityRuleStatsAsync(
        string domain,
        string entityId,
        bool? includeSubaccounts = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v1/bounce-classification/domains/{domain}/events</c> — paginated bounce-log events
    /// for one domain. Filters are optional; the response shape is undefined in the spec and is
    /// returned as a generic dictionary.
    /// </summary>
    Task<Dictionary<string, object>> ListDomainEventsAsync(
        string domain,
        string? ruleId = null,
        string? entityId = null,
        string? sort = null,
        string? pageCursor = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v1/bounce-classification/stats</c> — account-wide rolled-up bounce stats, ordered
    /// by total bounces descending. <paramref name="group"/> selects a grouping dimension.
    /// </summary>
    Task<BounceClassificationStatsResponse> ListAccountStatsAsync(
        string? group = null,
        int? limit = null,
        bool? includeSubaccounts = null,
        CancellationToken cancellationToken = default);
}

/// <summary>A classification entity as returned by the config catalog.</summary>
public sealed class BounceClassificationEntity
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
}

/// <summary>A classification rule as returned by the config catalog.</summary>
public sealed class BounceClassificationRule
{
    [JsonPropertyName("entity_id")] public string? EntityId { get; init; }
    [JsonPropertyName("class")] public string? Class { get; init; }
    [JsonPropertyName("sample-text")] public string? SampleText { get; init; }
    [JsonPropertyName("explanation")] public string? Explanation { get; init; }
    [JsonPropertyName("short-explanation")] public string? ShortExplanation { get; init; }
}

/// <summary>The bounce count wrapper Mailgun returns for every stat shape.</summary>
public sealed class BounceCount
{
    [JsonPropertyName("total")] public int Total { get; init; }
}

/// <summary>A subaccount reference attached to a stat row.</summary>
public sealed class BounceStatSubaccount
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
}

/// <summary>A domain reference attached to a stat row.</summary>
public sealed class BounceStatDomain
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
}

/// <summary>
/// One row of the account-wide stats endpoint — per-domain bounce totals.
/// </summary>
public sealed class BounceClassificationAccountStat
{
    [JsonPropertyName("domain")] public BounceStatDomain? Domain { get; init; }
    [JsonPropertyName("bounced")] public BounceCount? Bounced { get; init; }
}

/// <summary>Paginated response from <c>GET /v1/bounce-classification/domains</c>.</summary>
public sealed class BounceClassificationAccountStatsResponse
{
    [JsonPropertyName("items")] public List<BounceClassificationAccountStat>? Items { get; init; }
    [JsonPropertyName("total")] public int? Total { get; init; }
    /// <summary>Echo of the query parameters Mailgun applied (server-supplied, untyped).</summary>
    [JsonPropertyName("req")] public Dictionary<string, object>? Req { get; init; }
}

/// <summary>One row of per-entity stats for a domain.</summary>
public sealed class BounceClassificationDomainStat
{
    [JsonPropertyName("entity-id")] public string EntityId { get; init; } = string.Empty;
    [JsonPropertyName("entity-name")] public string EntityName { get; init; } = string.Empty;
    [JsonPropertyName("bounced")] public BounceCount? Bounced { get; init; }
}

/// <summary>Response from <c>GET /v1/bounce-classification/domains/{domain}/entities</c>.</summary>
public sealed class BounceClassificationDomainStatsResponse
{
    [JsonPropertyName("items")] public List<BounceClassificationDomainStat>? Items { get; init; }
}

/// <summary>One row of per-rule stats inside an entity, for a domain.</summary>
public sealed class BounceClassificationEntityStat
{
    [JsonPropertyName("rule-id")] public string RuleId { get; init; } = string.Empty;
    [JsonPropertyName("category")] public string? Category { get; init; }
    [JsonPropertyName("severity")] public string? Severity { get; init; }
    [JsonPropertyName("sample-text")] public string? SampleText { get; init; }
    [JsonPropertyName("explanation")] public string? Explanation { get; init; }
    [JsonPropertyName("bounced")] public BounceCount? Bounced { get; init; }
}

/// <summary>Response from <c>GET /v1/bounce-classification/domains/{domain}/entities/{entity}/rules</c>.</summary>
public sealed class BounceClassificationEntityStatsResponse
{
    [JsonPropertyName("items")] public List<BounceClassificationEntityStat>? Items { get; init; }
}

/// <summary>Per-row stat returned by <c>GET /v1/bounce-classification/stats</c>.</summary>
public sealed class BounceClassificationStat
{
    [JsonPropertyName("subaccount")] public BounceStatSubaccount? Subaccount { get; init; }
    [JsonPropertyName("domain")] public BounceStatDomain? Domain { get; init; }
    [JsonPropertyName("rule-id")] public string? RuleId { get; init; }
    [JsonPropertyName("entity-id")] public string? EntityId { get; init; }
    [JsonPropertyName("short-explanation")] public string? ShortExplanation { get; init; }
    [JsonPropertyName("bounced")] public BounceCount? Bounced { get; init; }
}

/// <summary>Response from <c>GET /v1/bounce-classification/stats</c>.</summary>
public sealed class BounceClassificationStatsResponse
{
    [JsonPropertyName("items")] public List<BounceClassificationStat>? Items { get; init; }
    /// <summary>Server-measured query duration, e.g. <c>"123ms"</c>.</summary>
    [JsonPropertyName("_duration")] public string? Duration { get; init; }
}

/// <summary>The SMTP/MTA-status codes that map to a single Mailgun classification.</summary>
public sealed class BounceClassificationCodesResponse
{
    [JsonPropertyName("items")] public List<string>? Items { get; init; }
}

/// <summary>Available dimensions for a bounce-classification metrics query.</summary>
public sealed class BounceClassificationDimensionsResponse
{
    [JsonPropertyName("items")] public List<string>? Items { get; init; }
}

/// <summary>Request body for <c>POST /v1/bounce-classification/classify</c>.</summary>
public sealed class ClassifyBounceRequest
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

/// <summary>A Mailgun bounce-classification entry.</summary>
public sealed class BounceClassification
{
    [JsonPropertyName("code")] public string Code { get; init; } = string.Empty;
    [JsonPropertyName("category")] public string? Category { get; init; }
    [JsonPropertyName("severity")] public string? Severity { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
}

/// <summary>List response.</summary>
public sealed class BounceClassificationListResponse
{
    [JsonPropertyName("items")] public List<BounceClassification>? Items { get; init; }
}

/// <summary>Request body for <c>POST /v2/bounce-classification/metrics</c>.</summary>
public sealed class BounceClassificationMetricsRequest
{
    [JsonPropertyName("start")] public string? Start { get; set; }
    [JsonPropertyName("end")] public string? End { get; set; }
    [JsonPropertyName("resolution")] public string? Resolution { get; set; }
    [JsonPropertyName("filter")] public object? Filter { get; set; }
    [JsonPropertyName("include_subaccounts")] public bool? IncludeSubaccounts { get; set; }
}

/// <summary>Response body for bounce-classification metrics.</summary>
public sealed class BounceClassificationMetricsResponse
{
    [JsonPropertyName("items")] public List<Dictionary<string, object>>? Items { get; init; }
    [JsonPropertyName("aggregates")] public Dictionary<string, object>? Aggregates { get; init; }
}

internal sealed class BounceClassificationService : IBounceClassificationService
{
    private readonly MailgunHttpClient _http;
    public BounceClassificationService(MailgunHttpClient http) => _http = http;

    public Task<BounceClassificationListResponse> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<BounceClassificationListResponse>("v1/bounce-classification", null, cancellationToken,
            routeTemplate: "v1/bounce-classification");

    public Task<BounceClassification> GetAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return _http.GetJsonAsync<BounceClassification>($"v1/bounce-classification/{PathEscape.Segment(code)}", null, cancellationToken,
            routeTemplate: "v1/bounce-classification/{code}");
    }

    public Task<BounceClassificationMetricsResponse> QueryMetricsAsync(BounceClassificationMetricsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _http.PostJsonBodyAsync<BounceClassificationMetricsResponse>("v2/bounce-classification/metrics", request, cancellationToken,
            routeTemplate: "v2/bounce-classification/metrics");
    }

    public Task<BounceClassificationCodesResponse> ListCodesAsync(string classificationCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classificationCode);
        return _http.GetJsonAsync<BounceClassificationCodesResponse>(
            $"v1/bounce-classification/{PathEscape.Segment(classificationCode)}/codes", null, cancellationToken,
            routeTemplate: "v1/bounce-classification/{classification_code}/codes");
    }

    public Task<BounceClassification> ClassifyAsync(ClassifyBounceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _http.PostJsonBodyAsync<BounceClassification>("v1/bounce-classification/classify", request, cancellationToken,
            routeTemplate: "v1/bounce-classification/classify");
    }

    public Task<BounceClassificationListResponse> ListCategoriesAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<BounceClassificationListResponse>("v1/bounce-classification/categories", null, cancellationToken,
            routeTemplate: "v1/bounce-classification/categories");

    public Task<BounceClassificationDimensionsResponse> ListDimensionsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<BounceClassificationDimensionsResponse>("v2/bounce-classification/metrics/dimensions", null, cancellationToken,
            routeTemplate: "v2/bounce-classification/metrics/dimensions");

    public Task<BounceClassificationCodesResponse> ListMetricsCodesAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<BounceClassificationCodesResponse>("v2/bounce-classification/metrics/codes", null, cancellationToken,
            routeTemplate: "v2/bounce-classification/metrics/codes");

    // ---------- Config + per-domain stats surface ----------

    public Task<Dictionary<string, BounceClassificationEntity>> ListConfigEntitiesAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<Dictionary<string, BounceClassificationEntity>>(
            "v1/bounce-classification/config/entities", null, cancellationToken,
            routeTemplate: "v1/bounce-classification/config/entities");

    public Task<Dictionary<string, BounceClassificationRule>> ListConfigRulesAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<Dictionary<string, BounceClassificationRule>>(
            "v1/bounce-classification/config/rules", null, cancellationToken,
            routeTemplate: "v1/bounce-classification/config/rules");

    public Task<BounceClassificationAccountStatsResponse> ListDomainStatsAsync(
        int? limit = null,
        int? skip = null,
        string? query = null,
        bool? includeSubaccounts = null,
        CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder()
            .Add("limit", limit)
            .Add("skip", skip)
            .Add("query", query)
            .Add("include_subaccounts", includeSubaccounts)
            .Build();
        return _http.GetJsonAsync<BounceClassificationAccountStatsResponse>(
            "v1/bounce-classification/domains", q, cancellationToken,
            routeTemplate: "v1/bounce-classification/domains");
    }

    public Task<BounceClassificationDomainStatsResponse> ListDomainEntityStatsAsync(
        string domain,
        bool? includeSubaccounts = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var q = new QueryBuilder().Add("include_subaccounts", includeSubaccounts).Build();
        return _http.GetJsonAsync<BounceClassificationDomainStatsResponse>(
            $"v1/bounce-classification/domains/{PathEscape.Segment(domain)}/entities", q, cancellationToken,
            routeTemplate: "v1/bounce-classification/domains/{domain}/entities");
    }

    public Task<BounceClassificationEntityStatsResponse> ListEntityRuleStatsAsync(
        string domain,
        string entityId,
        bool? includeSubaccounts = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        var q = new QueryBuilder().Add("include_subaccounts", includeSubaccounts).Build();
        return _http.GetJsonAsync<BounceClassificationEntityStatsResponse>(
            $"v1/bounce-classification/domains/{PathEscape.Segment(domain)}/entities/{PathEscape.Segment(entityId)}/rules",
            q, cancellationToken,
            routeTemplate: "v1/bounce-classification/domains/{domain}/entities/{entity-id}/rules");
    }

    public Task<Dictionary<string, object>> ListDomainEventsAsync(
        string domain,
        string? ruleId = null,
        string? entityId = null,
        string? sort = null,
        string? pageCursor = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        // Mailgun's spec uses hyphenated query keys here (rule-id, entity-id) — preserve them.
        var q = new QueryBuilder()
            .Add("rule-id", ruleId)
            .Add("entity-id", entityId)
            .Add("sort", sort)
            .Add("page", pageCursor)
            .Add("limit", limit)
            .Build();
        return _http.GetJsonAsync<Dictionary<string, object>>(
            $"v1/bounce-classification/domains/{PathEscape.Segment(domain)}/events", q, cancellationToken,
            routeTemplate: "v1/bounce-classification/domains/{domain}/events");
    }

    public Task<BounceClassificationStatsResponse> ListAccountStatsAsync(
        string? group = null,
        int? limit = null,
        bool? includeSubaccounts = null,
        CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder()
            .Add("group", group)
            .Add("limit", limit)
            .Add("include_subaccounts", includeSubaccounts)
            .Build();
        return _http.GetJsonAsync<BounceClassificationStatsResponse>(
            "v1/bounce-classification/stats", q, cancellationToken,
            routeTemplate: "v1/bounce-classification/stats");
    }
}
