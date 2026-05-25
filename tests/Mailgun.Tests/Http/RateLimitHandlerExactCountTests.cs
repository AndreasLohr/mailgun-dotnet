using System.Net;
using Mailgun.Exceptions;

namespace Mailgun.Tests.Http;

/// <summary>
/// Pins down <c>RateLimitHandler</c>'s exact retry-count semantics so Stryker can't quietly
/// flip <c>&gt;=</c> to <c>&gt;</c>, swap <c>++</c> for <c>--</c>, or short-circuit the success
/// path by mutating <c>ShouldRetry</c>'s default return.
/// </summary>
public class RateLimitHandlerExactCountTests
{
    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses;
        public int CallCount { get; private set; }

        public CountingHandler(params HttpStatusCode[] statuses) =>
            _statuses = new Queue<HttpStatusCode>(statuses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var status = _statuses.Count > 0 ? _statuses.Dequeue() : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("{\"items\":[],\"total_count\":0}", System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private static (MailgunClient client, CountingHandler counter) Build(int maxRetries, params HttpStatusCode[] statuses)
    {
        var counter = new CountingHandler(statuses);
        var rateLimit = (DelegatingHandler)Activator.CreateInstance(
            typeof(MailgunClient).Assembly.GetType("Mailgun.Http.RateLimitHandler")!,
            args: new object[] { maxRetries })!;
        rateLimit.InnerHandler = counter;
        var http = new HttpClient(rateLimit) { BaseAddress = new Uri("https://api.mailgun.test/") };
        var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
        });
        return (client, counter);
    }

    [Fact]
    public async Task Three_429s_with_max_retries_three_yields_exactly_four_calls()
    {
        var (client, counter) = Build(3, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests,
            HttpStatusCode.TooManyRequests, HttpStatusCode.OK);

        _ = await client.Routes.ListAsync();

        // Initial attempt + exactly 3 retries = 4 total. Kills the >= → > mutation and the
        // attempt++ → attempt-- mutation (both would change this count).
        Assert.Equal(4, counter.CallCount);
    }

    [Fact]
    public async Task Four_429s_with_max_retries_three_throws_after_exactly_four_calls()
    {
        var (client, counter) = Build(3, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests,
            HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests);

        await Assert.ThrowsAsync<MailgunRateLimitException>(() => client.Routes.ListAsync());

        Assert.Equal(4, counter.CallCount);
    }

    [Fact]
    public async Task Successful_first_response_does_not_retry_even_with_high_MaxRetries()
    {
        // Kills the `return false` → `return true` mutation in ShouldRetry: if mutated, every
        // call would loop until MaxRetries+1.
        var (client, counter) = Build(5, HttpStatusCode.OK);

        _ = await client.Routes.ListAsync();

        Assert.Equal(1, counter.CallCount);
    }

    [Fact]
    public async Task POST_returning_500_is_NOT_retried_even_when_MaxRetries_is_high()
    {
        // Kills mutations that broaden retry to non-idempotent methods on 5xx.
        var (client, counter) = Build(5, HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<MailgunApiException>(() => client.Messages.SendAsync("d", new()
        {
            From = "a@d",
            To = { "b@x" },
            Text = "t",
        }));

        Assert.Equal(1, counter.CallCount);
    }

    [Fact]
    public async Task GET_returning_500_is_retried_until_MaxRetries()
    {
        // Kills mutations that narrow retry to 429-only on idempotent methods.
        var (client, counter) = Build(2, HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError, HttpStatusCode.OK);

        _ = await client.Routes.ListAsync();

        Assert.Equal(3, counter.CallCount);
    }
}
