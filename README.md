# mailgun-dotnet

A .NET SDK for the [Mailgun](https://documentation.mailgun.com/) HTTP API. Covers every non-deprecated endpoint across Mailgun's `v1`–`v5` surface (Messages, Domains, Suppressions, Routes, Mailing Lists, Templates, Webhooks, Analytics, Validate, Inbox Placement, IPs, Subaccounts, Account, Users, Keys, …) with idiomatic hand-written DTOs.

## Features

- **Broad API coverage** — 26 resource services covering Mailgun's non-deprecated v1–v5 surface: Messages, Domains (including SMTP-credential CRUD), IPs, IP Pools, Dynamic IP Pools, IP Warmups, DKIM Keys, DKIM Security (rotation + auto-rotation), Account & Domain Webhooks, Suppressions (Bounces / Complaints / Unsubscribes / Allowlists), Routes, Mailing Lists, Templates (+ versions), Analytics, Analytics Tags, Bounce Classification, Validate (+ bulk), Inbox Placement (seedlists / results / tests / providers), Alerts, Send Alerts, Limits, Subaccounts, Custom Message Limit, Account, Users (RBAC), Keys. The deprecated v3 endpoints (Events, Stats, Tags, Domain Templates, Domain Webhooks v3, Forwards, x509) are intentionally excluded — see the "Endpoint coverage" section below for the modern replacements.
- **Typed end-to-end** — request and response DTOs for every endpoint; `System.Text.Json` with snake_case mapping, RFC-2822, and Unix-timestamp converters.
- **Two-region deployment** — `MailgunRegion.Us` → `api.mailgun.net`, `MailgunRegion.Eu` → `api.eu.mailgun.net`. Selected at construction time with no per-call overhead.
- **Subaccount impersonation** — `client.ForSubaccount("acct_id")` returns a derived client sharing the parent's transport with `X-Mailgun-On-Behalf-Of` injected on every request.
- **Idiomatic async pagination** — `AsyncPageable<T>` supports both item-by-item (`await foreach`) and page-by-page (`AsPages()`) enumeration, with `CancellationToken` propagation through `[EnumeratorCancellation]`.
- **Built-in retries** — `X-RateLimit-Reset`-aware backoff for 429 (with `Retry-After` fallback), plus idempotent-only 5xx retry (POSTs aren't replayed on 5xx).
- **Structured exceptions** — `MailgunApiException` exposes the HTTP status, parsed message + details, `X-Mailgun-Request-Id`, and rate-limit headers. `MailgunRateLimitException` is a distinct subtype for catch-block branching.
- **DI-friendly** — opt-in `mailgun-dotnet.Extensions.DependencyInjection` package wires `IMailgunClient` through `IHttpClientFactory`. The core package has zero `Microsoft.Extensions.*` dependencies so it runs in console apps, AWS Lambda, Azure Functions, Unity, etc.
- **Typed webhook receiver** — opt-in `mailgun-dotnet.Webhooks` + `mailgun-dotnet.AspNetCore` companion packages. Parses Mailgun's 8 event types into strongly-typed events, verifies HMAC-SHA256 signatures with constant-time compare and an optional anti-replay token cache, and ships a one-line `MapMailgunWebhook` endpoint helper.
- **OpenTelemetry-native tracing** — `MailgunActivitySource.Name = "Mailgun"` emits a client span per HTTP call. Zero NuGet dependencies, zero cost when no listener is attached.
- **Mutation-tested** — Stryker.NET is wired as a local tool. Current baseline: 75.4% overall mutation score, ~77% covered-code kill rate (see [Mutation testing](#mutation-testing) for the round-by-round table).
- **Multi-target** — `net8.0` and `net10.0`.

## Install

```bash
dotnet add package mailgun-dotnet
dotnet add package mailgun-dotnet.Webhooks            # optional: typed webhook event parsing + signature verification
dotnet add package mailgun-dotnet.Webhooks.DistributedCache  # optional: IDistributedCache-backed replay protection for multi-instance receivers
dotnet add package mailgun-dotnet.AspNetCore          # optional: ASP.NET Core webhook endpoint helper
dotnet add package mailgun-dotnet.Extensions.DependencyInjection  # optional: IServiceCollection.AddMailgun()
```

## Quick start

### ASP.NET Core / Worker Services (recommended)

Add the DI package and configure via `appsettings.json`:

```bash
dotnet add package mailgun-dotnet
dotnet add package mailgun-dotnet.Extensions.DependencyInjection
```

#### appsettings.json

```json
{
  "Mailgun": {
    "ApiKey": "YOUR_API_KEY",
    "Region": "Us"
  }
}
```

#### Program.cs

```csharp
using Mailgun;
using Mailgun.Exceptions;
using Mailgun.Extensions.DependencyInjection;
using Mailgun.Models.Messages;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMailgun(builder.Configuration);   // binds the "Mailgun" section by convention

var app = builder.Build();

app.MapPost("/send", async (IMailgunClient mailgun) =>
{
    try
    {
        var result = await mailgun.Messages.SendAsync("mg.example.com", new SendMessageRequest
        {
            From = "Excited User <mailgun@mg.example.com>",
            To = { "recipient@example.com" },
            Subject = "Hello",
            Text = "Testing the Mailgun .NET SDK",
        });
        return Results.Ok(new { id = result.Id });
    }
    catch (MailgunApiException ex)
    {
        // Typed exception — exposes StatusCode, ErrorMessage, RequestId, RateLimit, Details.
        return Results.Problem(
            detail: $"Mailgun {(int)ex.StatusCode}: {ex.ErrorMessage} (req {ex.RequestId})",
            statusCode: 502);
    }
});

app.Run();
```

Other binding shapes:

```csharp
builder.Services.AddMailgun(builder.Configuration.GetSection("Mailgun"));  // explicit section
builder.Services.AddMailgun(opts =>                                        // pure code
{
    opts.ApiKey = "YOUR_API_KEY";
    opts.Region = MailgunRegion.Eu;
});
```

`AddMailgun` returns `IHttpClientBuilder`, so resilience and observability policies chain naturally on top of the SDK's built-in 429/idempotent-5xx retries:

```csharp
builder.Services.AddMailgun(builder.Configuration)
    .AddStandardResilienceHandler();
```

### Without dependency injection (console apps, AWS Lambda, scripts)

```csharp
using Mailgun;
using Mailgun.Models.Messages;

using var client = new MailgunClient("YOUR_API_KEY");

var result = await client.Messages.SendAsync("mg.example.com", new SendMessageRequest
{
    From = "Excited User <mailgun@mg.example.com>",
    To = { "recipient@example.com" },
    Subject = "Hello",
    Text = "Testing the Mailgun .NET SDK",
});
Console.WriteLine($"Queued message id: {result.Id}");
```

The owned-`HttpClient` path applies the SDK's retry handler automatically. In long-lived hosts prefer the DI path so the `HttpClient` is managed by `IHttpClientFactory`.

## Fluent message builder

For chained-method syntax instead of the object-initializer form, use `NewMessage()`. Every `SendMessageRequest` property has a corresponding builder method.

```csharp
var resp = await mailgun.Messages.NewMessage()
    .From("Excited User <mailgun@mg.example.com>")
    .To("alice@example.com", "bob@example.com")
    .Cc("cc@example.com")
    .Subject("Hello")
    .Text("plain body")
    .Html("<p>html body</p>")
    .Tag("welcome", "v2")
    .Campaign("spring-2026")
    .TemplateVariable("name", "Alice")
    .Header("X-Campaign", "spring-2026")
    .CustomVariable("source", "signup")
    .Attach("invoice.pdf", pdfBytes, "application/pdf")
    .Inline("logo.png", logoBytes, "image/png")
    .DeliverAt(DateTimeOffset.UtcNow.AddHours(2))
    .TestMode()
    .SendAsync("mg.example.com");
```

Method conventions:

- **Collection setters** (`To`, `Cc`, `Bcc`, `Tag`, `Campaign`) take `params string[]` — pass one or many; calls append.
- **Dictionary setters** (`TemplateVariable`, `Header`, `CustomVariable`, `Option`) take key + value; duplicate keys overwrite.
- **Flag setters** (`TestMode`, `Dkim`, `TrackingOpens`, `RequireTls`, `SkipVerification`, `TrackingPixelLocationTop`, `TemplateText`) default to `true`, so `.TestMode()` and `.RequireTls()` are one-liners; pass `false` to disable.
- **`.Build()`** returns the underlying `SendMessageRequest` if you want to inspect, store, or send it later via the regular `Messages.SendAsync(domain, request)` overload.

The builder is a convenience layer on top of the DTO — the object-initializer form continues to work and is fully supported.

## Fluent route builder

Mailgun routes are configured with a small DSL: an *expression* (`match_recipient(...)`, `match_header(...)`, `catch_all()`, optionally combined with `and`/`or`/`not`) and an ordered list of *actions* (`forward(...)`, `store(...)`, `stop()`). The `NewRoute()` builder surfaces that DSL as methods so you don't have to construct the strings by hand.

```csharp
// Forward all support@ mail to a webhook, then store it.
var route = await client.Routes.NewRoute()
    .Priority(0)
    .Description("Forward support to webhook")
    .MatchRecipient("support@mg.example.com")
    .Forward("https://hooks.example.com/mailgun")
    .Store(notifyUrl: "https://hooks.example.com/stored")
    .Stop()
    .CreateAsync();
```

Combined expressions use the typed `RouteExpression` tree:

```csharp
await client.Routes.NewRoute()
    .Description("Support OR sales, but not noreply")
    .Match(RouteExpression.And(
        RouteExpression.Or(
            RouteExpression.MatchRecipient("support@mg.example.com"),
            RouteExpression.MatchRecipient("sales@mg.example.com")),
        RouteExpression.Not(RouteExpression.MatchRecipient("noreply@.*"))))
    .Forward("https://hooks.example.com/mailgun")
    .Stop()
    .CreateAsync();
```

DSL coverage:

- **Expression sugar (overwrites on repeat):** `MatchRecipient`, `MatchHeader`, `CatchAll`, `Match(RouteExpression)` for trees.
- **Expression combinators:** `RouteExpression.And/Or/Not`. `Raw(string)` is an escape hatch for matchers Mailgun adds later.
- **Actions (append on every call):** `Forward(urlOrEmail)`, `Store()` / `Store(notifyUrl)`, `Stop()`. `Action(rawString)` is the escape hatch.
- **Escaping:** all DSL string literals are double-quote-escaped automatically — `MatchRecipient("a\"b")` renders as `match_recipient("a\"b")`. You don't need to pre-escape.
- **Update an existing route:** chain ends with `.UpdateAsync(id)` instead of `.CreateAsync()`. The same builder maps to `UpdateRouteRequest`.

The object-initializer path (`new CreateRouteRequest { ... }` + `client.Routes.CreateAsync(...)`) remains fully supported.

## Regions

```csharp
using var client = new MailgunClient(new MailgunClientOptions
{
    ApiKey = "YOUR_API_KEY",
    Region = MailgunRegion.Eu,   // → api.eu.mailgun.net (default is Us → api.mailgun.net)
});
```

## Subaccounts

```csharp
var sub = client.ForSubaccount("acct_abc123");
await foreach (var domain in sub.Domains.ListAllAsync())
{
    Console.WriteLine(domain.Name);
}
```

## Pagination

```csharp
// Single page
var page = await client.Suppressions.Bounces.ListAsync("mg.example.com");

// Auto-paginated stream
await foreach (var bounce in client.Suppressions.Bounces.ListAllAsync("mg.example.com"))
{
    Console.WriteLine(bounce.Address);
}
```

## Error handling

```csharp
try
{
    await client.Messages.SendAsync(domain, request);
}
catch (MailgunRateLimitException ex)
{
    // Retried 3× already (configurable via MailgunClientOptions.MaxRetries). Back off.
    Console.Error.WriteLine($"Rate-limited; reset at {ex.RateLimit?.Reset}");
}
catch (MailgunApiException ex)
{
    Console.Error.WriteLine($"Mailgun returned {(int)ex.StatusCode}: {ex.Message}");
    Console.Error.WriteLine($"Request id: {ex.RequestId}");
}
```

## Receiving webhooks

Install `mailgun-dotnet.Webhooks` (and `mailgun-dotnet.AspNetCore` if you're in ASP.NET Core). Mailgun signs every webhook with HMAC-SHA256 over `timestamp + token`; the SDK verifies it with a constant-time compare and rejects timestamps outside a configurable clock-skew window (default 15 minutes).

### Raw (any framework)

```csharp
using Mailgun.Webhooks;
using Mailgun.Webhooks.Events;

if (!MailgunWebhookSignatureValidator.IsValid(
        signingKey: "YOUR_HTTP_SIGNING_KEY",   // from /v5/accounts/http_signing_key
        timestamp: signature.Timestamp,
        token:     signature.Token,
        signature: signature.Signature,
        maxAge:    TimeSpan.FromMinutes(15)))
{
    return 401;
}

var evt = MailgunWebhookParser.Parse(rawJsonBody);
switch (evt)
{
    case DeliveredEvent d:      Console.WriteLine($"delivered to {d.Recipient}"); break;
    case OpenedEvent o:         Console.WriteLine($"opened by {o.Recipient}"); break;
    case ClickedEvent c:        Console.WriteLine($"clicked {c.Url}"); break;
    case PermanentFailEvent f:  Console.WriteLine($"permanent fail: {f.Reason}"); break;
    case UnknownMailgunWebhookEvent u:
        // Mailgun added a new event type we haven't typed yet.
        Console.WriteLine($"unknown event {u.Event}; raw = {u.RawJson}");
        break;
}
```

### ASP.NET Core endpoint helper

```csharp
app.MapMailgunWebhook("/webhooks/mailgun",
    new MailgunWebhookEndpointOptions
    {
        SigningKey   = builder.Configuration["Mailgun:HttpSigningKey"]!,
        MaxClockSkew = TimeSpan.FromMinutes(15),
        TokenCache   = new InMemoryWebhookTokenCache(),   // optional: anti-replay
    },
    async (evt, ctx, ct) =>
    {
        // evt is a typed MailgunWebhookEvent (e.g. DeliveredEvent, ClickedEvent).
        // The endpoint already verified the signature + token freshness; just handle it.
        await HandleEvent(evt, ct);
    });
```

The helper returns 200 on success, 401 on invalid signature or stale timestamp, 409 on replay-cache hit, 400 on malformed JSON.

### Replay protection across multiple instances

`InMemoryWebhookTokenCache` keeps the seen-token set in a per-process `ConcurrentDictionary`. That's the right choice for a single instance but degrades silently when the receiver runs behind more than one pod / VM / container: a token replayed against a different instance hits a fresh dictionary and is accepted. For any multi-instance topology, install `mailgun-dotnet.Webhooks.DistributedCache` and wire it through `IDistributedCache`:

```bash
dotnet add package mailgun-dotnet.Webhooks.DistributedCache
```

```csharp
// 1. Register any IDistributedCache implementation — Redis, SQL Server, NCache, Cosmos, etc.
builder.Services.AddStackExchangeRedisCache(o =>
{
    o.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// 2. Register the adapter as IWebhookTokenCache.
builder.Services.AddMailgunWebhookDistributedTokenCache();

// 3. In the endpoint setup, resolve the cache from DI instead of newing up the in-memory one.
app.MapMailgunWebhook("/webhooks/mailgun",
    new MailgunWebhookEndpointOptions
    {
        SigningKey   = builder.Configuration["Mailgun:HttpSigningKey"]!,
        MaxClockSkew = TimeSpan.FromMinutes(15),
        TokenCache   = app.Services.GetRequiredService<IWebhookTokenCache>(),
    },
    /* ... handler ... */);
```

`IDistributedCache` deliberately doesn't expose atomic set-if-not-exists, so the adapter implements replay-check as get-then-set — two concurrent webhooks carrying the same token can both be treated as fresh in a small race window. For Mailgun replay protection this is acceptable (tokens are large random strings, attacks aren't typically concurrent, the second replay onward is reliably blocked); Stripe's official webhook samples accept the same trade-off. Callers needing strict atomicity should implement `IWebhookTokenCache` directly against their store's primitive (e.g. StackExchange.Redis's `SETNX`).

## OpenTelemetry tracing

The SDK emits a client span per HTTP call on the `MailgunActivitySource` (name = `"Mailgun"`). Subscribe by registering the source with your OpenTelemetry tracer provider — there are zero NuGet dependencies on OpenTelemetry, and zero cost when no listener is attached.

```csharp
using Mailgun.Http;

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(MailgunActivitySource.Name)
                       .AddOtlpExporter());
```

Emitted span tags per request:

| Tag | Value |
|---|---|
| `http.request.method` | `GET` / `POST` / … |
| `url.full` | the full request URL |
| `server.address` | the Mailgun host (US or EU) |
| `http.response.status_code` | the HTTP response status |
| `mailgun.request_id` | `X-Mailgun-Request-Id` header (when present) |
| `mailgun.rate_limit.remaining` | `X-RateLimit-Remaining` (when present) |
| `exception.type` / `exception.message` | populated on failure |

## OpenTelemetry metrics

Alongside the per-request span, the SDK emits four instruments on a `MailgunMeter` (name = `"Mailgun"`, same as the activity source). Subscribe by registering the meter with your OpenTelemetry meter provider — zero NuGet dependencies, zero cost when no listener is attached.

```csharp
using Mailgun.Http;

builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter(MailgunMeter.Name)
                       .AddOtlpExporter());
```

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `mailgun.client.request.duration` | Histogram&lt;double&gt; | `s` | `http.request.method`, `http.route`, `http.response.status_code`, `server.address` |
| `mailgun.client.request.retries` | Counter&lt;long&gt; | `{retry}` | `http.request.method`, `http.route`, `retry.reason` (`"429"` or `"5xx"`), `server.address` |
| `mailgun.client.request.errors` | Counter&lt;long&gt; | `{error}` | `http.request.method`, `http.route`, `error.type`, `server.address` |
| `mailgun.client.active_requests` | UpDownCounter&lt;long&gt; | `{request}` | `http.request.method`, `server.address` |

The `http.route` tag is a **route template** (e.g. `v3/{domain}/messages`, `v1/webhooks/{webhook_id}`) — never the runtime path. This keeps cardinality bounded regardless of how many distinct domains, addresses, or IDs your traffic touches. The per-request unique values (`mailgun.request_id`, `mailgun.rate_limit.remaining`) appear only on activity spans, not metric tags — putting them on a metric tag would blow up cardinality.

Request rate is derived from the histogram's count; error rate from filtering the duration histogram on `http.response.status_code >= 400` OR from the dedicated `request.errors` counter (it covers both 4xx/5xx-mapped exceptions and raw transport failures like `HttpRequestException` / `TaskCanceledException`).

## Dependency Injection

```csharp
builder.Services.AddMailgun(o =>
{
    o.ApiKey = builder.Configuration["Mailgun:ApiKey"];
    o.Region = MailgunRegion.Eu;
});

// Inject IMailgunClient anywhere.
```

## Endpoint coverage

The SDK ships **non-deprecated** endpoints only. Legacy surfaces explicitly **not** covered (use the modern replacement listed):

| Deprecated path | Modern replacement |
|---|---|
| `GET /v3/{domain}/events` | `POST /v1/analytics/logs` (`client.Analytics.QueryLogsAsync`) |
| `/v3/stats/*`, `/v3/{domain}/stats/*` | `POST /v1/analytics/metrics` (`client.Analytics.QueryMetricsAsync`) |
| `/v3/{domain}/tags*` | `/v1/analytics/tags` + analytics-metrics with tag dimensions (`client.AnalyticsTags`) |
| `/v3/{domain}/templates*` | `/v4/templates` (`client.Templates`) |
| `/v3/forwards` | `/v3/routes` (`client.Routes`) |
| `/v2/x509/*` | (No replacement; Mailgun manages TLS automatically.) |

## Testing

```bash
dotnet test -c Release
```

### Mutation testing

[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) is wired as a local dotnet tool. It mutates SDK source (skipping pure DTOs under `Models/`) and runs the test suite against each mutant to measure whether the tests actually *catch* bugs — a more honest signal than a passing test count.

```bash
dotnet tool restore
dotnet stryker
```

The HTML report lands in `StrykerOutput/<timestamp>/reports/mutation-report.html`. Configuration is in [stryker-config.json](./stryker-config.json).

Current baseline (311 tests, ~1293 mutants reached by tests):

- **Overall mutation score: 75.4%** (993 killed / 290 survived / 10 timeout / 38 not covered by any test).
- **Covered-code kill rate: ~77%** on the mutants the test suite reaches.
- The 38 NoCoverage survivors are concentrated in fallback / unreachable code paths (an OpenTelemetry listener that's not registered in tests; an exception-message string the tests don't assert text on; a couple of `JsonValueKind` arms in `FlattenJson` that only trigger for non-standard error envelopes; some `TimeSpan.Zero` equality boundaries in the retry-backoff math).
- The 290 covered-code survivors are predominantly equivalent mutations and boolean-combination edge cases where the mutated behavior is unobservable through the public API.
- **4 real bugs found and fixed during the survivor-by-survivor triage**: `Templates.CreateVersionAsync`, `InboxPlacement.CreateSeedlistAsync` / `CreateTestAsync`, `DynamicIpPools.CreateAsync`, and `Users.CreateAsync` were all missing required-field validation on their typed request DTOs (an empty `Name` / `Email` / `Tag` / `Seedlist` would have been sent to Mailgun as an empty string instead of throwing `ArgumentException` at the call site).

Progression — each round expanded test coverage and reran Stryker:

| Round | Tests | Killed | Survived | NoCoverage | Score |
| --- | --- | --- | --- | --- | --- |
| Baseline | 33 | 132 | 187 | 914 | 10.7% |
| + infrastructure tests | 77 | 315 | 162 | 755 | 25.6% |
| + per-endpoint service tests | 164 | 609 | 331 | 292 | 49.5% |
| + Routes / Alerts / Domains gap-fillers | 203 | 711 | 438 | 83 | 57.8% |
| + argument-validation sweep + HTTP internals | 232 | 855 | 302 | 75 | 69.4% |
| + DKIM services + extended IPs / IpPools / InboxPlacement / BounceClassification / Limits (~40 endpoints) | 269 | 896 | 364 | 73 | 67.2% |
| + survivor triage: 4 bug fixes + blank-arg sweep + HTTP-client behavior + exact-retry-count tests | 305 | 979 | 283 | 73 | 73.9% |
| + high-ROI NoCoverage killers: dead-code purge, missing `ListAllAsync` coverage, OpenTelemetry `ActivityListener` tests | 311 | 993 | 290 | 38 | 75.4% |

## License

Proprietary — see [LICENSE](./LICENSE). All rights reserved.

---

*Mailgun is a registered trademark of Sinch Email (Mailgun Technologies, Inc.). This project is an independent, community-built .NET client for Mailgun's public HTTP API and is not affiliated with, endorsed by, or sponsored by Sinch / Mailgun Technologies, Inc. All product and company names are trademarks or registered trademarks of their respective holders; use of them does not imply any affiliation or endorsement.*
