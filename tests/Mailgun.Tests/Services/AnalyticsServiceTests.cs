using System.Net;
using Mailgun.Models.Analytics;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class AnalyticsServiceTests
{
    [Fact]
    public async Task QueryMetrics_posts_json_body_to_v1_analytics_metrics()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "items": [{"dimensions":[{"dimension":"time","value":"2026-05-01"}], "metrics":{"accepted_count":12.0}}],
              "pagination": {"skip":0,"limit":10,"total":1}
            }
            """);

        var resp = await client.Analytics.QueryMetricsAsync(new MetricsRequest
        {
            Start = "Wed, 01 May 2026 00:00:00 +0000",
            End = "Fri, 31 May 2026 23:59:59 +0000",
            Resolution = "1d",
            Dimensions = new() { "time" },
            Metrics = new() { "accepted_count" },
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v1/analytics/metrics", req.Uri.AbsolutePath);
        Assert.Equal("application/json", req.ContentType);
        Assert.Contains("\"resolution\":\"1d\"", req.Body, StringComparison.Ordinal);
        Assert.Single(resp.Items!);
        Assert.Equal(1, resp.Pagination!.Total);
    }
}
