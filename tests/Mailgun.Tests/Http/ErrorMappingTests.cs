using System.Net;
using Mailgun.Exceptions;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Http;

public class ErrorMappingTests
{
    [Fact]
    public async Task Maps_404_with_error_envelope_to_MailgunApiException()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.NotFound, "{\"message\":\"Domain not found\"}",
            headers: new Dictionary<string, string> { { "X-Mailgun-Request-Id", "req-1" } });

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("missing"));
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("Domain not found", ex.ErrorMessage);
        Assert.Equal("req-1", ex.RequestId);
    }

    [Fact]
    public async Task Maps_429_to_MailgunRateLimitException()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.TooManyRequests, "{\"message\":\"slow down\"}",
            headers: new Dictionary<string, string>
            {
                { "X-RateLimit-Limit", "300" },
                { "X-RateLimit-Remaining", "0" },
                { "X-RateLimit-Reset", "1716000000000" },
            });

        var ex = await Assert.ThrowsAsync<MailgunRateLimitException>(() => client.Domains.GetAsync("x"));
        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.NotNull(ex.RateLimit);
        Assert.Equal(300, ex.RateLimit!.Limit);
        Assert.Equal(0, ex.RateLimit.Remaining);
    }
}
