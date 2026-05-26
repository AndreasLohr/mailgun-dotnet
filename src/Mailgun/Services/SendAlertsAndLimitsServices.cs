using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;

namespace Mailgun.Services;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Send Alerts  (/v1/thresholds/alerts/send)
// ─────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Operations on <c>/v1/thresholds/alerts/send</c> — CRUD over named send-alert threshold rules.
/// Each rule fires when a metric crosses a comparator+limit on a dimension, optionally restricted
/// by filters and notified via one or more alert channels.
/// </summary>
public interface ISendAlertsService
{
    /// <summary><c>GET /v1/thresholds/alerts/send</c> — list every send-alert rule on the account.</summary>
    Task<SendAlertRuleList> ListAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /v1/thresholds/alerts/send/{name}</c> — retrieve a single rule by name.</summary>
    Task<SendAlertRule> GetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v1/thresholds/alerts/send</c> — create a new send-alert rule.</summary>
    Task<SendAlertRule> CreateAsync(SendAlertRule rule, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v1/thresholds/alerts/send/{name}</c> — replace a rule.</summary>
    Task<SendAlertRule> UpdateAsync(string name, SendAlertRule rule, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v1/thresholds/alerts/send/{name}</c> — delete a rule.</summary>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>
/// A send-alert threshold rule per Mailgun's <c>/v1/thresholds/alerts/send</c> schema.
/// </summary>
public sealed class SendAlertRule
{
    /// <summary>User-friendly identifier (required on create). Also the URL segment for item-level operations.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    /// <summary>The metric being monitored (required).</summary>
    [JsonPropertyName("metric")] public string Metric { get; set; } = string.Empty;

    /// <summary>Comparator (required), e.g. <c>gt</c>, <c>gte</c>, <c>lt</c>, <c>lte</c>, <c>eq</c>.</summary>
    [JsonPropertyName("comparator")] public string Comparator { get; set; } = string.Empty;

    /// <summary>Threshold value (required). String so callers can use either integers or decimals.</summary>
    [JsonPropertyName("limit")] public string Limit { get; set; } = string.Empty;

    /// <summary>Dimension the metric is grouped/scoped by (required).</summary>
    [JsonPropertyName("dimension")] public string Dimension { get; set; } = string.Empty;

    /// <summary>Optional dimension filters narrowing where the rule applies.</summary>
    [JsonPropertyName("filters")] public List<ThresholdFilter>? Filters { get; set; }

    /// <summary>Optional alert channels (e.g. webhook URLs or email addresses Mailgun should notify).</summary>
    [JsonPropertyName("alert_channels")] public List<string>? AlertChannels { get; set; }

    /// <summary>Optional time-aggregation window, e.g. <c>1h</c>, <c>24h</c>.</summary>
    [JsonPropertyName("period")] public string? Period { get; set; }

    /// <summary>Free-form description (optional).</summary>
    [JsonPropertyName("description")] public string? Description { get; set; }

    /// <summary>Server-assigned id (response-only).</summary>
    [JsonPropertyName("id"), JsonInclude] public string? Id { get; private set; }
}

/// <summary>List envelope for send-alert rules.</summary>
public sealed class SendAlertRuleList
{
    [JsonPropertyName("items")] public List<SendAlertRule>? Items { get; init; }
    [JsonPropertyName("total")] public long? Total { get; init; }
}

internal sealed class SendAlertsService : ISendAlertsService
{
    private readonly MailgunHttpClient _http;
    public SendAlertsService(MailgunHttpClient http) => _http = http;

    private const string BasePath = "v1/thresholds/alerts/send";

    public Task<SendAlertRuleList> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<SendAlertRuleList>(BasePath, null, cancellationToken);

    public Task<SendAlertRule> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _http.GetJsonAsync<SendAlertRule>($"{BasePath}/{PathEscape.Segment(name)}", null, cancellationToken);
    }

    public Task<SendAlertRule> CreateAsync(SendAlertRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ValidateRequired(rule.Name, rule.Metric, rule.Comparator, rule.Limit, rule.Dimension);
        return _http.PostJsonBodyAsync<SendAlertRule>(BasePath, rule, cancellationToken);
    }

    public Task<SendAlertRule> UpdateAsync(string name, SendAlertRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rule);
        ValidateRequired(rule.Name, rule.Metric, rule.Comparator, rule.Limit, rule.Dimension);
        return _http.PutJsonBodyAsync<SendAlertRule>($"{BasePath}/{PathEscape.Segment(name)}", rule, cancellationToken);
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _http.DeleteNoResponseAsync($"{BasePath}/{PathEscape.Segment(name)}", cancellationToken);
    }

    private static void ValidateRequired(string name, string metric, string comparator, string limit, string dimension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(metric, nameof(metric));
        ArgumentException.ThrowIfNullOrWhiteSpace(comparator, nameof(comparator));
        ArgumentException.ThrowIfNullOrWhiteSpace(limit, nameof(limit));
        ArgumentException.ThrowIfNullOrWhiteSpace(dimension, nameof(dimension));
    }
}

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Limits  (/v1/thresholds/limits)
// ─────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Operations on <c>/v1/thresholds/limits</c> — CRUD over named limit threshold rules.
/// Same shape as send-alert rules but without <see cref="SendAlertRule.AlertChannels"/>.
/// </summary>
public interface ILimitsService
{
    /// <summary><c>GET /v1/thresholds/limits</c> — list every limit rule on the account.</summary>
    Task<LimitRuleList> ListAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /v1/thresholds/limits/{name}</c> — retrieve a single rule by name.</summary>
    Task<LimitRule> GetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v1/thresholds/limits</c> — create a new limit rule.</summary>
    Task<LimitRule> CreateAsync(LimitRule rule, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v1/thresholds/limits/{name}</c> — replace a limit rule.</summary>
    Task<LimitRule> UpdateAsync(string name, LimitRule rule, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v1/thresholds/limits/{name}</c> — delete a limit rule.</summary>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>
/// A limit threshold rule per Mailgun's <c>/v1/thresholds/limits</c> schema.
/// </summary>
public sealed class LimitRule
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("metric")] public string Metric { get; set; } = string.Empty;
    [JsonPropertyName("comparator")] public string Comparator { get; set; } = string.Empty;
    [JsonPropertyName("limit")] public string Limit { get; set; } = string.Empty;
    [JsonPropertyName("dimension")] public string Dimension { get; set; } = string.Empty;
    [JsonPropertyName("filters")] public List<ThresholdFilter>? Filters { get; set; }
    [JsonPropertyName("period")] public string? Period { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("id"), JsonInclude] public string? Id { get; private set; }
}

/// <summary>List envelope for limit rules.</summary>
public sealed class LimitRuleList
{
    [JsonPropertyName("items")] public List<LimitRule>? Items { get; init; }
    [JsonPropertyName("total")] public long? Total { get; init; }
}

/// <summary>Optional dimension filter that narrows a threshold rule's scope.</summary>
public sealed class ThresholdFilter
{
    /// <summary>The dimension the filter applies to (required when the parent <c>filters</c> array is present).</summary>
    [JsonPropertyName("dimension")] public string Dimension { get; set; } = string.Empty;

    /// <summary>Comparator for the filter (required when the parent <c>filters</c> array is present).</summary>
    [JsonPropertyName("comparator")] public string Comparator { get; set; } = string.Empty;

    /// <summary>Values to match (required when the parent <c>filters</c> array is present).</summary>
    [JsonPropertyName("values")] public List<string> Values { get; set; } = new();
}

internal sealed class LimitsService : ILimitsService
{
    private readonly MailgunHttpClient _http;
    public LimitsService(MailgunHttpClient http) => _http = http;

    private const string BasePath = "v1/thresholds/limits";

    public Task<LimitRuleList> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<LimitRuleList>(BasePath, null, cancellationToken);

    public Task<LimitRule> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _http.GetJsonAsync<LimitRule>($"{BasePath}/{PathEscape.Segment(name)}", null, cancellationToken);
    }

    public Task<LimitRule> CreateAsync(LimitRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ValidateRequired(rule.Name, rule.Metric, rule.Comparator, rule.Limit, rule.Dimension);
        return _http.PostJsonBodyAsync<LimitRule>(BasePath, rule, cancellationToken);
    }

    public Task<LimitRule> UpdateAsync(string name, LimitRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rule);
        ValidateRequired(rule.Name, rule.Metric, rule.Comparator, rule.Limit, rule.Dimension);
        return _http.PutJsonBodyAsync<LimitRule>($"{BasePath}/{PathEscape.Segment(name)}", rule, cancellationToken);
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _http.DeleteNoResponseAsync($"{BasePath}/{PathEscape.Segment(name)}", cancellationToken);
    }

    private static void ValidateRequired(string name, string metric, string comparator, string limit, string dimension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(metric, nameof(metric));
        ArgumentException.ThrowIfNullOrWhiteSpace(comparator, nameof(comparator));
        ArgumentException.ThrowIfNullOrWhiteSpace(limit, nameof(limit));
        ArgumentException.ThrowIfNullOrWhiteSpace(dimension, nameof(dimension));
    }
}
