using Mailgun.Models.Analytics;

namespace Mailgun.Services;

/// <summary>
/// Operations on <c>/v1/analytics/metrics</c>, <c>/v1/analytics/usage/metrics</c>, and <c>/v1/analytics/logs</c>.
/// These supersede the deprecated <c>/v3/{domain}/events</c>, <c>/v3/stats/*</c>, and <c>/v3/{domain}/tags*</c> endpoints.
/// </summary>
public interface IAnalyticsService
{
    /// <summary><c>POST /v1/analytics/metrics</c> — aggregated metrics grouped by dimensions.</summary>
    Task<MetricsResponse> QueryMetricsAsync(MetricsRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v1/analytics/usage/metrics</c> — account usage rollups.</summary>
    Task<MetricsResponse> QueryUsageMetricsAsync(UsageMetricsRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v1/analytics/logs</c> — per-event delivery logs (replaces deprecated <c>events</c>).</summary>
    Task<LogsResponse> QueryLogsAsync(LogsRequest request, CancellationToken cancellationToken = default);
}
