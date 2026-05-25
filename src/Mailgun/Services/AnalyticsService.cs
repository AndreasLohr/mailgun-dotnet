using Mailgun.Http;
using Mailgun.Models.Analytics;

namespace Mailgun.Services;

internal sealed class AnalyticsService : IAnalyticsService
{
    private readonly MailgunHttpClient _http;
    public AnalyticsService(MailgunHttpClient http) => _http = http;

    public Task<MetricsResponse> QueryMetricsAsync(MetricsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _http.PostJsonBodyAsync<MetricsResponse>("v1/analytics/metrics", request, cancellationToken);
    }

    public Task<MetricsResponse> QueryUsageMetricsAsync(UsageMetricsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _http.PostJsonBodyAsync<MetricsResponse>("v1/analytics/usage/metrics", request, cancellationToken);
    }

    public Task<LogsResponse> QueryLogsAsync(LogsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _http.PostJsonBodyAsync<LogsResponse>("v1/analytics/logs", request, cancellationToken);
    }
}
