using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;

namespace Mailgun.Services;

/// <summary>Operations on <c>/v1/analytics/tags</c>.</summary>
public interface IAnalyticsTagsService
{
    /// <summary>
    /// <c>POST /v1/analytics/tags</c> — list tags (or filter by tag prefix). Mailgun uses POST for
    /// this endpoint to accommodate the rich filter body; the SDK exposes the common knobs
    /// (limit/skip/tag prefix/include_subaccounts/include_metrics) and surfaces the result through
    /// <see cref="AnalyticsTagsListResponse"/>.
    /// </summary>
    Task<AnalyticsTagsListResponse> ListAsync(AnalyticsTagsFilter? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v1/analytics/tags</c> — delete a tag. The tag identity is supplied in the JSON
    /// request body, not the URL.
    /// </summary>
    Task DeleteAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v1/analytics/tags/limits</c> — current tag-count + per-tag limits.</summary>
    Task<TagLimits> GetLimitsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v1/analytics/tags</c> — update a tag's description. The tag identity is supplied
    /// in the request body, not the URL.
    /// </summary>
    Task UpdateAsync(string tag, string description, CancellationToken cancellationToken = default);
}

/// <summary>Filter / pagination for <see cref="IAnalyticsTagsService.ListAsync"/>.</summary>
public sealed class AnalyticsTagsFilter
{
    /// <summary>Optional tag-prefix filter — returns only tags whose name starts with this value.</summary>
    [JsonPropertyName("tag")] public string? Tag { get; set; }

    /// <summary>Include data from all subaccounts. Defaults to false.</summary>
    [JsonPropertyName("include_subaccounts")] public bool? IncludeSubaccounts { get; set; }

    /// <summary>Include metrics for each tag. When true, Mailgun caps the page limit at 20.</summary>
    [JsonPropertyName("include_metrics")] public bool? IncludeMetrics { get; set; }

    /// <summary>Pagination block — limit/skip/sort/include_total.</summary>
    [JsonPropertyName("pagination")] public AnalyticsTagsPagination? Pagination { get; set; }
}

/// <summary>Pagination block for the analytics-tags list endpoint.</summary>
public sealed class AnalyticsTagsPagination
{
    /// <summary>Maximum items returned (Mailgun caps at 100, or at 20 when <c>include_metrics</c> is true on the filter).</summary>
    [JsonPropertyName("limit")] public int? Limit { get; set; }

    /// <summary>Number of items to skip.</summary>
    [JsonPropertyName("skip")] public int? Skip { get; set; }

    /// <summary>Colon-separated column + direction, e.g. <c>"timestamp:desc"</c>.</summary>
    [JsonPropertyName("sort")] public string? Sort { get; set; }

    /// <summary>Whether to include the total number of matching items in the response.</summary>
    [JsonPropertyName("include_total")] public bool? IncludeTotal { get; set; }
}

/// <summary>A Mailgun analytics tag.</summary>
public sealed class AnalyticsTag
{
    [JsonPropertyName("tag")] public string Tag { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("first_seen")] public string? FirstSeen { get; init; }
    [JsonPropertyName("last_seen")] public string? LastSeen { get; init; }
}

/// <summary>Mailgun's analytics-tags list response.</summary>
public sealed class AnalyticsTagsListResponse
{
    [JsonPropertyName("items")] public List<AnalyticsTag>? Items { get; init; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; init; }
}

/// <summary>Account tag limits.</summary>
public sealed class TagLimits
{
    [JsonPropertyName("limit")] public long? Limit { get; init; }
    [JsonPropertyName("count")] public long? Count { get; init; }
}

internal sealed class AnalyticsTagsService : IAnalyticsTagsService
{
    private readonly MailgunHttpClient _http;
    public AnalyticsTagsService(MailgunHttpClient http) => _http = http;

    public Task<AnalyticsTagsListResponse> ListAsync(AnalyticsTagsFilter? filter = null, CancellationToken cancellationToken = default) =>
        // Mailgun's list-tags endpoint is POST, with a JSON body carrying optional filter + pagination.
        _http.PostJsonBodyAsync<AnalyticsTagsListResponse>("v1/analytics/tags", filter ?? new AnalyticsTagsFilter(), cancellationToken);

    public Task DeleteAsync(string tag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        // Mailgun's DELETE /v1/analytics/tags takes a JSON body with {tag}; tag is NOT in the URL.
        return _http.DeleteJsonBodyNoResponseAsync("v1/analytics/tags", new DeleteAnalyticsTagBody(tag), cancellationToken);
    }

    public Task<TagLimits> GetLimitsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<TagLimits>("v1/analytics/tags/limits", null, cancellationToken);

    public Task UpdateAsync(string tag, string description, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(description);
        // Mailgun's PUT /v1/analytics/tags takes a JSON body with {tag, description}; tag is NOT in the URL.
        var body = new UpdateAnalyticsTagRequest { Tag = tag, Description = description };
        return _http.PutJsonBodyNoResponseAsync("v1/analytics/tags", body, cancellationToken);
    }
}

internal sealed class UpdateAnalyticsTagRequest
{
    [JsonPropertyName("tag")] public string Tag { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
}

internal sealed record DeleteAnalyticsTagBody([property: JsonPropertyName("tag")] string Tag);
