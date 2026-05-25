using System.Net;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Http;

public class ResponseMetadataTests
{
    [Fact]
    public async Task Parses_request_id_and_all_rate_limit_headers()
    {
        var (client, handler) = TestMailgunClient.Create();
        // X-RateLimit-Reset is Unix milliseconds per Mailgun's docs.
        var resetMs = new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds().ToString();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}",
            headers: new Dictionary<string, string>
            {
                { "X-Mailgun-Request-Id", "req-xyz" },
                { "X-RateLimit-Limit", "300" },
                { "X-RateLimit-Remaining", "297" },
                { "X-RateLimit-Reset", resetMs },
            });

        _ = await client.Routes.ListAsync();
        var md = client.LastResponseMetadata!;
        Assert.Equal(HttpStatusCode.OK, md.StatusCode);
        Assert.Equal("req-xyz", md.RequestId);
        Assert.Equal(300, md.RateLimitLimit);
        Assert.Equal(297, md.RateLimitRemaining);
        Assert.Equal(new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero), md.RateLimitReset);
    }

    [Fact]
    public async Task Falls_back_to_X_Request_Id_when_mailgun_header_is_absent()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}",
            headers: new Dictionary<string, string> { { "X-Request-Id", "fallback-id" } });

        _ = await client.Routes.ListAsync();
        Assert.Equal("fallback-id", client.LastResponseMetadata!.RequestId);
    }

    [Fact]
    public async Task Treats_small_reset_value_as_unix_seconds_for_compatibility()
    {
        // Some edge handlers report seconds, not ms. Anything before year-2001 (1e12 ms) is treated as seconds.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}",
            headers: new Dictionary<string, string>
            {
                { "X-RateLimit-Reset", "1700000000" },
            });

        _ = await client.Routes.ListAsync();
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), client.LastResponseMetadata!.RateLimitReset);
    }

    [Fact]
    public async Task Missing_rate_limit_headers_yield_null_RateLimit()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        _ = await client.Routes.ListAsync();
        Assert.Null(client.LastResponseMetadata!.RateLimit);
    }
}
