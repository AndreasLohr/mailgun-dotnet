using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using Mailgun.Exceptions;
using Mailgun.Http;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Telemetry;

/// <summary>
/// Verifies the <see cref="MailgunMeter"/> instruments fire on the right code paths with the right
/// tags. <see cref="MeterListener"/> has no per-test isolation channel (unlike Activity baggage),
/// AND xUnit's <c>[Collection]</c> only serializes tests within a class — other test classes run
/// in parallel and can emit measurements on the same global <see cref="MailgunMeter.Instance"/>.
/// So every test in this class uses a unique base URL (<c>api.mailgun-test-{guid}.test</c>) and
/// filters the captured measurements by <c>server.address</c> — that's the only reliable way to
/// isolate "my measurements" from cross-class noise.
/// </summary>
public class MailgunMeterTests
{
    private readonly record struct Measurement(string InstrumentName, double Value, KeyValuePair<string, object?>[] Tags);

    private static (MeterListener Listener, ConcurrentBag<Measurement> Bag, string UniqueHost) RegisterListener()
    {
        var uniqueHost = $"api.mailgun-test-{Guid.NewGuid():N}.test";
        var bag = new ConcurrentBag<Measurement>();
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == MailgunMeter.Name)
                    l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<double>((inst, value, tags, _) =>
            bag.Add(new Measurement(inst.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<long>((inst, value, tags, _) =>
            bag.Add(new Measurement(inst.Name, value, tags.ToArray())));
        listener.Start();
        return (listener, bag, uniqueHost);
    }

    private static object? GetTag(Measurement m, string key) =>
        m.Tags.FirstOrDefault(t => t.Key == key).Value;

    [Fact]
    public async Task RequestDuration_records_on_success_with_method_route_status_and_server_tags()
    {
        var registration = RegisterListener();
        using var _listener = registration.Listener;
        var bag = registration.Bag;
        var (client, handler) = TestMailgunClient.Create(baseUrl: $"https://{registration.UniqueHost}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"name\":\"d\",\"type\":\"sandbox\"}");

        await client.Domains.GetAsync("d");

        var dur = bag
            .Where(m => m.InstrumentName == "mailgun.client.request.duration"
                     && (GetTag(m, "server.address") as string) == registration.UniqueHost)
            .Single();
        Assert.True(dur.Value >= 0); // wall-clock seconds — always non-negative
        Assert.Equal("GET", GetTag(dur, "http.request.method"));
        Assert.Equal("v4/domains/{name}", GetTag(dur, "http.route"));
        Assert.Equal(registration.UniqueHost, GetTag(dur, "server.address"));
        Assert.Equal(200, GetTag(dur, "http.response.status_code"));
    }

    [Fact]
    public async Task RequestDuration_records_status_code_tag_when_response_is_non_2xx()
    {
        // Even when the SDK throws MailgunException on a 4xx, the histogram must still record the
        // measurement with `http.response.status_code` populated — that's what enables error-rate
        // dashboards via filtered histogram count.
        var registration = RegisterListener();
        using var _listener = registration.Listener;
        var bag = registration.Bag;
        var (client, handler) = TestMailgunClient.Create(baseUrl: $"https://{registration.UniqueHost}");
        handler.EnqueueResponse(HttpStatusCode.NotFound, "{\"message\":\"not found\"}");

        await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));

        var dur = bag
            .Where(m => m.InstrumentName == "mailgun.client.request.duration"
                     && (GetTag(m, "server.address") as string) == registration.UniqueHost)
            .Single();
        Assert.Equal(404, GetTag(dur, "http.response.status_code"));
        Assert.Equal("v4/domains/{name}", GetTag(dur, "http.route"));
    }

    [Fact]
    public async Task RequestErrors_increments_on_mapped_4xx_exception()
    {
        var registration = RegisterListener();
        using var _listener = registration.Listener;
        var bag = registration.Bag;
        var (client, handler) = TestMailgunClient.Create(baseUrl: $"https://{registration.UniqueHost}");
        handler.EnqueueResponse(HttpStatusCode.BadRequest, "{\"message\":\"bad\"}");

        await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));

        var err = bag
            .Where(m => m.InstrumentName == "mailgun.client.request.errors"
                     && (GetTag(m, "server.address") as string) == registration.UniqueHost)
            .Single();
        Assert.Equal(1, err.Value);
        Assert.Equal("GET", GetTag(err, "http.request.method"));
        var errorType = GetTag(err, "error.type") as string;
        Assert.NotNull(errorType);
        Assert.Contains("MailgunApiException", errorType, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestRetries_increments_with_reason_5xx_per_idempotent_retry()
    {
        // The RateLimitHandler retries idempotent 5xx. To exercise it deterministically we wire
        // the handler manually around the mock (mirrors the established pattern in
        // RateLimitHandlerTests.SDK_owned_pipeline_retries_429_then_succeeds).
        var registration = RegisterListener();
        using var _listener = registration.Listener;
        var bag = registration.Bag;
        var uniqueHost = registration.UniqueHost;

        var primary = new MockHttpMessageHandler();
        primary.EnqueueResponse(HttpStatusCode.InternalServerError, "boom");
        primary.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        var rateLimit = (DelegatingHandler)Activator.CreateInstance(
            typeof(MailgunClient).Assembly.GetType("Mailgun.Http.RateLimitHandler")!,
            args: new object[] { 1 })!;
        rateLimit.InnerHandler = primary;
        using var http = new HttpClient(rateLimit) { BaseAddress = new Uri($"https://{uniqueHost}/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = $"https://{uniqueHost}",
            HttpClient = http,
        });

        await client.Routes.ListAsync();

        var retries = bag
            .Where(m => m.InstrumentName == "mailgun.client.request.retries"
                     && (GetTag(m, "server.address") as string) == uniqueHost)
            .ToList();
        var retry = Assert.Single(retries);
        Assert.Equal(1, retry.Value);
        Assert.Equal("GET", GetTag(retry, "http.request.method"));
        Assert.Equal("5xx", GetTag(retry, "retry.reason"));
    }

    [Fact]
    public async Task RequestRetries_increments_with_reason_429_on_TooManyRequests()
    {
        var registration = RegisterListener();
        using var _listener = registration.Listener;
        var bag = registration.Bag;
        var uniqueHost = registration.UniqueHost;

        var primary = new MockHttpMessageHandler();
        var pastReset = DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeMilliseconds().ToString();
        primary.EnqueueResponse(HttpStatusCode.TooManyRequests, "{\"message\":\"slow\"}",
            headers: new Dictionary<string, string> { { "X-RateLimit-Reset", pastReset } });
        primary.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        var rateLimit = (DelegatingHandler)Activator.CreateInstance(
            typeof(MailgunClient).Assembly.GetType("Mailgun.Http.RateLimitHandler")!,
            args: new object[] { 1 })!;
        rateLimit.InnerHandler = primary;
        using var http = new HttpClient(rateLimit) { BaseAddress = new Uri($"https://{uniqueHost}/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = $"https://{uniqueHost}",
            HttpClient = http,
        });

        await client.Routes.ListAsync();

        var retries = bag
            .Where(m => m.InstrumentName == "mailgun.client.request.retries"
                     && (GetTag(m, "server.address") as string) == uniqueHost)
            .ToList();
        var retry = Assert.Single(retries);
        Assert.Equal(1, retry.Value);
        Assert.Equal("429", GetTag(retry, "retry.reason"));
    }

    [Fact]
    public async Task ActiveRequests_balances_to_zero_on_success_path()
    {
        var registration = RegisterListener();
        using var _listener = registration.Listener;
        var bag = registration.Bag;
        var (client, handler) = TestMailgunClient.Create(baseUrl: $"https://{registration.UniqueHost}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"name\":\"d\",\"type\":\"sandbox\"}");

        await client.Domains.GetAsync("d");

        // Filter to our unique host so cross-class active_requests emissions don't contaminate.
        // UpDownCounter emits +1 on entry and -1 in finally; net should be exactly zero.
        var sum = bag
            .Where(m => m.InstrumentName == "mailgun.client.active_requests"
                     && (GetTag(m, "server.address") as string) == registration.UniqueHost)
            .Sum(m => m.Value);
        Assert.Equal(0, sum);
    }

    [Fact]
    public async Task ActiveRequests_balances_to_zero_on_exception_path()
    {
        var registration = RegisterListener();
        using var _listener = registration.Listener;
        var bag = registration.Bag;
        var (client, handler) = TestMailgunClient.Create(baseUrl: $"https://{registration.UniqueHost}");
        handler.EnqueueResponse(HttpStatusCode.NotFound, "{\"message\":\"missing\"}");

        await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));

        var sum = bag
            .Where(m => m.InstrumentName == "mailgun.client.active_requests"
                     && (GetTag(m, "server.address") as string) == registration.UniqueHost)
            .Sum(m => m.Value);
        Assert.Equal(0, sum);
    }
}
