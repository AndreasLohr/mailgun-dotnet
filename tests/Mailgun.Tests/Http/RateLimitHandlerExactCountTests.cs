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
    public async Task Rotate_endpoint_uses_POST_which_is_already_non_retried_on_5xx()
    {
        // DkimSecurity.RotateAsync hits POST /v1/dkim_management/domains/{name}/rotate per Mailgun's
        // docs — POST isn't classified as idempotent, so it's never retried regardless of the
        // action-endpoint check. This test pins that "rotate is POST, fires once" contract.
        var (client, counter) = Build(3,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK);

        await Assert.ThrowsAsync<MailgunApiException>(() => client.DkimSecurity.RotateAsync("mg.example.com"));

        Assert.Equal(1, counter.CallCount);
    }

    [Fact]
    public async Task IsActionEndpoint_skips_retry_for_PUT_to_last_segment_rotate_paths()
    {
        // Defensive: even if a future Mailgun endpoint uses PUT/DELETE for a non-idempotent
        // action, the handler's IsActionEndpoint heuristic must kick in. Drive the handler
        // directly with a synthetic PUT to a /rotate-something last segment.
        var counter = new CountingHandler(HttpStatusCode.InternalServerError, HttpStatusCode.OK);
        var rateLimit = (DelegatingHandler)Activator.CreateInstance(
            typeof(MailgunClient).Assembly.GetType("Mailgun.Http.RateLimitHandler")!,
            args: new object[] { 3 })!;
        rateLimit.InnerHandler = counter;
        using var http = new HttpClient(rateLimit);
        using var req = new HttpRequestMessage(HttpMethod.Put,
            new Uri("https://api.mailgun.test/v1/dkim_management/domains/refresh-club.com/rotate-something"));

        using var resp = await http.SendAsync(req);

        // One call only — even though PUT is "idempotent" by spec, the /rotate-something last
        // segment classifies this as an action endpoint and disables retry. Also exercises the
        // false-positive fix for #4: the middle segment "refresh-club.com" containing the verb
        // "refresh" does NOT trigger the heuristic (because middle segments are ignored AND
        // the domain has a dot).
        Assert.Equal(1, counter.CallCount);
    }

    [Fact]
    public async Task IsActionEndpoint_does_not_disable_retry_for_domain_named_refresh()
    {
        // Regression for #4: substring-matching on /refresh, /rotate, /regenerate previously caught
        // any domain literally containing those tokens (e.g. refresh-club.com), silently disabling
        // 5xx retry on every PUT/DELETE to that domain. The dot-aware last-segment heuristic must
        // restore retry for these.
        var counter = new CountingHandler(
            HttpStatusCode.InternalServerError,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK);
        var rateLimit = (DelegatingHandler)Activator.CreateInstance(
            typeof(MailgunClient).Assembly.GetType("Mailgun.Http.RateLimitHandler")!,
            args: new object[] { 3 })!;
        rateLimit.InnerHandler = counter;
        using var http = new HttpClient(rateLimit);
        // PUT /v4/domains/{refresh-club.com}/tracking/open — the verb-shaped token is in a middle
        // segment AND the segment has a dot, so it must NOT trip the action-endpoint check.
        using var req = new HttpRequestMessage(HttpMethod.Put,
            new Uri("https://api.mailgun.test/v4/domains/refresh-club.com/tracking/open"));

        using var resp = await http.SendAsync(req);

        // Initial attempt + 2 retries (the two 5xx) = 3 total. Retry must NOT be suppressed.
        Assert.Equal(3, counter.CallCount);
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
