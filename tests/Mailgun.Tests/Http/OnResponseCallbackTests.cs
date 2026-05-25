using System.Net;
using Mailgun.Http;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Http;

public class OnResponseCallbackTests
{
    [Fact]
    public async Task OnResponse_invoked_once_per_call_with_parsed_metadata()
    {
        var captured = new List<MailgunResponseMetadata>();
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}",
            headers: new Dictionary<string, string>
            {
                { "X-Mailgun-Request-Id", "req-1" },
                { "X-RateLimit-Remaining", "297" },
            });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
            OnResponse = md => captured.Add(md),
        });

        _ = await client.Routes.ListAsync();

        var only = Assert.Single(captured);
        Assert.Equal(HttpStatusCode.OK, only.StatusCode);
        Assert.Equal("req-1", only.RequestId);
        Assert.Equal(297, only.RateLimitRemaining);
    }

    [Fact]
    public async Task OnResponse_fires_on_error_responses_too()
    {
        var captured = new List<MailgunResponseMetadata>();
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.NotFound, "{\"message\":\"nope\"}",
            headers: new Dictionary<string, string> { { "X-Mailgun-Request-Id", "req-err" } });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
            OnResponse = md => captured.Add(md),
        });

        await Assert.ThrowsAsync<Mailgun.Exceptions.MailgunApiException>(() => client.Domains.GetAsync("missing"));

        var only = Assert.Single(captured);
        Assert.Equal(HttpStatusCode.NotFound, only.StatusCode);
        Assert.Equal("req-err", only.RequestId);
    }

    [Fact]
    public async Task Callback_thrown_exception_does_not_break_the_call()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
            OnResponse = _ => throw new InvalidOperationException("bad consumer"),
        });

        // A buggy callback must not propagate into the caller's request flow.
        var page = await client.Routes.ListAsync();
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Callback_collects_every_response_under_concurrent_load()
    {
        // The documented concurrent-safe pattern: route the callback into a thread-safe
        // collection. Concurrent callers append; the consumer correlates by X-Mailgun-Request-Id
        // (or by Activity.Current.Id for OTel-instrumented apps). Crucially this does NOT rely
        // on AsyncLocal flowing the value back from the SDK's callback to the caller's
        // continuation — that pattern doesn't work because AsyncLocal writes only propagate
        // forward into child async ops, not back up to the awaiting parent.
        var captured = new System.Collections.Concurrent.ConcurrentBag<MailgunResponseMetadata>();
        var handler = new MockHttpMessageHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
            OnResponse = md => captured.Add(md),
        });

        for (var i = 0; i < 5; i++)
        {
            handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}",
                headers: new Dictionary<string, string> { { "X-Mailgun-Request-Id", $"call-{i}" } });
        }

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.Routes.ListAsync()));

        Assert.Equal(5, captured.Count);
        var ids = captured.Select(m => m.RequestId).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "call-0", "call-1", "call-2", "call-3", "call-4" }, ids);
    }
}
