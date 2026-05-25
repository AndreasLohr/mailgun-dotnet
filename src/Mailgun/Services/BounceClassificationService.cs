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
        _http.GetJsonAsync<BounceClassificationListResponse>("v1/bounce-classification", null, cancellationToken);

    public Task<BounceClassification> GetAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return _http.GetJsonAsync<BounceClassification>($"v1/bounce-classification/{PathEscape.Segment(code)}", null, cancellationToken);
    }

    public Task<BounceClassificationMetricsResponse> QueryMetricsAsync(BounceClassificationMetricsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _http.PostJsonBodyAsync<BounceClassificationMetricsResponse>("v2/bounce-classification/metrics", request, cancellationToken);
    }

    public Task<BounceClassificationCodesResponse> ListCodesAsync(string classificationCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classificationCode);
        return _http.GetJsonAsync<BounceClassificationCodesResponse>(
            $"v1/bounce-classification/{PathEscape.Segment(classificationCode)}/codes", null, cancellationToken);
    }

    public Task<BounceClassification> ClassifyAsync(ClassifyBounceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _http.PostJsonBodyAsync<BounceClassification>("v1/bounce-classification/classify", request, cancellationToken);
    }

    public Task<BounceClassificationListResponse> ListCategoriesAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<BounceClassificationListResponse>("v1/bounce-classification/categories", null, cancellationToken);

    public Task<BounceClassificationDimensionsResponse> ListDimensionsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<BounceClassificationDimensionsResponse>("v2/bounce-classification/metrics/dimensions", null, cancellationToken);

    public Task<BounceClassificationCodesResponse> ListMetricsCodesAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<BounceClassificationCodesResponse>("v2/bounce-classification/metrics/codes", null, cancellationToken);
}
