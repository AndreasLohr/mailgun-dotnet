using System.Diagnostics.Metrics;
using System.Reflection;

namespace Mailgun.Http;

/// <summary>
/// Single <see cref="System.Diagnostics.Metrics.Meter"/> the SDK emits HTTP-client metrics on.
/// OpenTelemetry consumers subscribe by name:
/// <code>
/// builder.Services.AddOpenTelemetry().WithMetrics(m =&gt; m.AddMeter(MailgunMeter.Name));
/// </code>
/// Instruments:
/// <list type="bullet">
///   <item><c>mailgun.client.request.duration</c> — Histogram&lt;double&gt;, seconds. Tags:
///   <c>http.request.method</c>, <c>http.route</c>, <c>http.response.status_code</c>, <c>server.address</c>.</item>
///   <item><c>mailgun.client.request.retries</c> — Counter&lt;long&gt;. Tags:
///   <c>http.request.method</c>, <c>http.route</c>, <c>retry.reason</c> (<c>"429"</c> or <c>"5xx"</c>).</item>
///   <item><c>mailgun.client.request.errors</c> — Counter&lt;long&gt;. Tags:
///   <c>http.request.method</c>, <c>http.route</c>, <c>error.type</c>.</item>
///   <item><c>mailgun.client.active_requests</c> — UpDownCounter&lt;long&gt;. Tags:
///   <c>http.request.method</c>, <c>server.address</c>.</item>
/// </list>
/// <para>
/// Per-request unique fields (<c>mailgun.request_id</c>, <c>mailgun.rate_limit.remaining</c>) appear on
/// <see cref="MailgunActivitySource"/> spans but are deliberately NOT mirrored to metric tags —
/// they'd blow up cardinality.
/// </para>
/// </summary>
public static class MailgunMeter
{
    /// <summary>The well-known meter name. Consumers pass this to <c>AddMeter(…)</c>.</summary>
    public const string Name = "Mailgun";

    private static readonly string Version =
        typeof(MailgunMeter).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(MailgunMeter).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>The shared <see cref="Meter"/> instance.</summary>
    public static readonly Meter Instance = new(Name, Version);

    /// <summary>Per-request latency. Always recorded once per <c>SendCoreAsync</c> call, success or failure.</summary>
    public static readonly Histogram<double> RequestDuration =
        Instance.CreateHistogram<double>(
            "mailgun.client.request.duration",
            unit: "s",
            description: "Duration of Mailgun HTTP requests in seconds.");

    /// <summary>One increment per retry attempt the <c>RateLimitHandler</c> decides to make.</summary>
    public static readonly Counter<long> RequestRetries =
        Instance.CreateCounter<long>(
            "mailgun.client.request.retries",
            unit: "{retry}",
            description: "Count of Mailgun HTTP request retries (429 + idempotent 5xx).");

    /// <summary>Incremented when a request throws — transport failure or 4xx/5xx mapped to exception.</summary>
    public static readonly Counter<long> RequestErrors =
        Instance.CreateCounter<long>(
            "mailgun.client.request.errors",
            unit: "{error}",
            description: "Count of Mailgun HTTP requests that completed with an exception.");

    /// <summary>Concurrent in-flight Mailgun requests. Increments on entry, decrements in <c>finally</c>.</summary>
    public static readonly UpDownCounter<long> ActiveRequests =
        Instance.CreateUpDownCounter<long>(
            "mailgun.client.active_requests",
            unit: "{request}",
            description: "Concurrent in-flight Mailgun HTTP requests.");

    /// <summary>
    /// Bridge for the route template across the <see cref="System.Net.Http.DelegatingHandler"/> boundary.
    /// <see cref="MailgunHttpClient.SendCoreAsync"/> stamps the template on <see cref="HttpRequestMessage.Options"/>
    /// before sending, so <see cref="RateLimitHandler"/> can read it when emitting retry counters
    /// without having to change the handler's signature.
    /// </summary>
    internal static readonly HttpRequestOptionsKey<string> RouteTemplateKey =
        new("mailgun.client.route_template");
}
