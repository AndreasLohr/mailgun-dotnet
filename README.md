# mailgun-dotnet

<a href="https://github.com/AndreasLohr/mailgun-dotnet/actions/workflows/ci.yml" target="_blank" rel="noopener noreferrer"><img src="https://github.com/AndreasLohr/mailgun-dotnet/actions/workflows/ci.yml/badge.svg?branch=main" alt="CI" /></a>
<a href="https://www.nuget.org/packages/mailgun-dotnet/" target="_blank" rel="noopener noreferrer"><img src="https://img.shields.io/nuget/v/mailgun-dotnet.svg?label=mailgun-dotnet&color=brightgreen" alt="NuGet" /></a>
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512BD4?logo=dotnet)](#install)
<a href="https://dashboard.stryker-mutator.io/reports/github.com/AndreasLohr/mailgun-dotnet/main" target="_blank" rel="noopener noreferrer"><img src="https://img.shields.io/endpoint?style=flat&url=https%3A%2F%2Fbadge-api.stryker-mutator.io%2Fgithub.com%2FAndreasLohr%2Fmailgun-dotnet%2Fmain" alt="Mutation score" /></a>

A .NET SDK for the <a href="https://documentation.mailgun.com/" target="_blank" rel="noopener noreferrer">Mailgun</a> HTTP API. Covers every non-deprecated endpoint across Mailgun's `v1`–`v5` surface (Messages, Domains, Suppressions, Routes, Mailing Lists, Templates, Webhooks, Analytics, Validate, Inbox Placement, IPs, Subaccounts, Account, Users, Keys, …) with idiomatic hand-written DTOs.

## Features

- **Broad API coverage** — 26 resource services covering Mailgun's non-deprecated v1–v5 surface: Messages, Domains (including SMTP-credential CRUD), IPs, IP Pools, Dynamic IP Pools, IP Warmups, DKIM Keys, DKIM Security (rotation + auto-rotation), Account & Domain Webhooks, Suppressions (Bounces / Complaints / Unsubscribes / Allowlists), Routes, Mailing Lists, Templates (+ versions), Analytics, Analytics Tags, Bounce Classification, Validate (+ bulk), Inbox Placement (seedlists / results / tests / providers), Alerts, Send Alerts, Limits, Subaccounts, Custom Message Limit, Account, Users (RBAC), Keys. The deprecated v3 endpoints (Events, Stats, Tags, Domain Templates, Forwards, x509) are intentionally excluded — see the "Endpoint coverage" section below for the modern replacements.
- **Typed end-to-end** — request and response DTOs for every endpoint; `System.Text.Json` with snake_case mapping, RFC-2822, and Unix-timestamp converters.
- **Two-region deployment** — `MailgunRegion.Us` → `api.mailgun.net`, `MailgunRegion.Eu` → `api.eu.mailgun.net`. Selected at construction time with no per-call overhead.
- **Subaccount impersonation** — `client.ForSubaccount("acct_id")` returns a derived client sharing the parent's transport with `X-Mailgun-On-Behalf-Of` injected on every request.
- **Idiomatic async pagination** — `AsyncPageable<T>` supports both item-by-item (<a href="https://learn.microsoft.com/dotnet/api/system.collections.generic.iasyncenumerable-1" target="_blank" rel="noopener noreferrer"><code>await foreach</code></a>) and page-by-page (`AsPages()`) enumeration, with `CancellationToken` propagation through `[EnumeratorCancellation]`.
- **Built-in retries** — `X-RateLimit-Reset`-aware backoff for 429 (with `Retry-After` fallback), plus idempotent-only 5xx retry (POSTs aren't replayed on 5xx).
- **Structured exceptions** — `MailgunApiException` exposes the HTTP status, parsed message + details, `X-Mailgun-Request-Id`, and rate-limit headers. `MailgunRateLimitException` is a distinct subtype for catch-block branching.
- **DI-friendly** — opt-in `mailgun-dotnet.Extensions.DependencyInjection` package wires `IMailgunClient` through <a href="https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory" target="_blank" rel="noopener noreferrer"><code>IHttpClientFactory</code></a>. The core package has zero `Microsoft.Extensions.*` dependencies so it runs in console apps, AWS Lambda, Azure Functions, Unity, etc.
- **Typed webhook receiver** — opt-in `mailgun-dotnet.Webhooks` + `mailgun-dotnet.AspNetCore` companion packages. Parses Mailgun's 8 event types into strongly-typed events, verifies <a href="https://en.wikipedia.org/wiki/HMAC" target="_blank" rel="noopener noreferrer">HMAC-SHA256</a> signatures with constant-time compare, enforces timestamp freshness, ships **replay protection on by default**, supports subaccount `parent-signature` verification, and exposes a one-line `MapMailgunWebhook` endpoint helper.
- **<a href="https://opentelemetry.io" target="_blank" rel="noopener noreferrer">OpenTelemetry</a>-native tracing + metrics** — `MailgunActivitySource` emits a client span per HTTP call; `MailgunMeter` emits `mailgun.client.*` instruments (request duration histogram, retries / errors counters, active-requests gauge) tagged with route templates so per-endpoint dashboards stay low-cardinality. Both share the name `"Mailgun"`. Zero NuGet dependencies, zero cost when no listener is attached.
- **Mutation-tested** — Stryker.NET is wired as a local tool and the score is published to the [dashboard badge](https://dashboard.stryker-mutator.io/reports/github.com/AndreasLohr/mailgun-dotnet/main) above (see [Mutation testing](#mutation-testing) for the round-by-round history). 638 tests across `net8.0` + `net10.0`.
- **Multi-target** — `net8.0` (LTS) and `net10.0`. See [Target frameworks & support matrix](#target-frameworks--support-matrix).

## Install

```bash
dotnet add package mailgun-dotnet
dotnet add package mailgun-dotnet.Webhooks                        # optional: typed webhook event parsing + signature verification
dotnet add package mailgun-dotnet.Webhooks.DistributedCache       # optional: IDistributedCache-backed replay protection for multi-instance receivers
dotnet add package mailgun-dotnet.AspNetCore                      # optional: ASP.NET Core webhook endpoint helper
dotnet add package mailgun-dotnet.Extensions.DependencyInjection  # optional: IServiceCollection.AddMailgun()
dotnet add package mailgun-dotnet.MimeKit                         # optional: SendMimeAsync(MimeMessage) overload for S/MIME, calendar invites, pre-signed RFC-2822
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

`AddMailgun` returns <a href="https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.ihttpclientbuilder" target="_blank" rel="noopener noreferrer"><code>IHttpClientBuilder</code></a>, so resilience and observability policies chain naturally on top of the SDK's built-in 429/idempotent-5xx retries:

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

## Raw MIME sends

Mailgun's form-encoded `/messages` endpoint can't represent every MIME shape — S/MIME-signed mail, calendar invites with proper `multipart/alternative` parts, pre-built DKIM signatures, embedded `message/delivery-status` reports. For those, use the dedicated `/v3/{domain}/messages.mime` endpoint, which accepts a full RFC-2822 message body.

### From raw bytes (no extra dependency)

The core `mailgun-dotnet` package ships a `byte[]` overload:

```csharp
var rfc2822Bytes = File.ReadAllBytes("signed.eml");
var resp = await client.Messages.SendMimeAsync(
    domain: "mg.example.com",
    to: new[] { "alice@example.com" },
    mimeMessage: rfc2822Bytes);
```

The recipient list is the **envelope** `RCPT TO` — distinct from any `To:` header inside the bytes. This is what Mailgun actually delivers to.

### From a `MimeMessage` (optional `mailgun-dotnet.MimeKit` package)

If you build messages with MimeKit (the de-facto MIME library in .NET), install the companion package for a one-line send:

```bash
dotnet add package mailgun-dotnet.MimeKit
```

```csharp
using Mailgun.MimeKit;
using MimeKit;

var message = new MimeMessage();
message.From.Add(new MailboxAddress("Sender", "noreply@mg.example.com"));
message.To.Add(new MailboxAddress("Alice", "alice@example.com"));
message.Subject = "Calendar invite";
message.Body = /* multipart/alternative with text + calendar parts */;

await client.Messages.SendMimeAsync("mg.example.com", message);
```

Envelope recipients are derived from `To` + `Cc` + `Bcc` automatically (case-insensitively deduplicated, first-seen-wins). Pass `envelopeRecipients:` explicitly to override — the legacy-SMTP pattern of BCC'ing audit copies that don't appear in any visible header.

### Migrating from `System.Net.Mail.SmtpClient`

Requires the `mailgun-dotnet.MimeKit` companion package:

```bash
dotnet add package mailgun-dotnet.MimeKit
```

Microsoft formally deprecated `SmtpClient`; the recommended replacement is MimeKit's MailKit / direct MIME construction. The conversion is straightforward — translate the `MailMessage` to a `MimeMessage` once, then send via the SDK:

```csharp
using MimeKit;

static MimeMessage FromMailMessage(System.Net.Mail.MailMessage src)
{
    var m = new MimeMessage();
    m.From.Add(MailboxAddress.Parse(src.From!.Address));
    foreach (var to in src.To)   m.To.Add(MailboxAddress.Parse(to.Address));
    foreach (var cc in src.CC)   m.Cc.Add(MailboxAddress.Parse(cc.Address));
    foreach (var bcc in src.Bcc) m.Bcc.Add(MailboxAddress.Parse(bcc.Address));
    m.Subject = src.Subject;
    m.Body = new TextPart(src.IsBodyHtml ? "html" : "plain") { Text = src.Body };
    return m;
}

// Replace `smtp.Send(mailMessage)` with:
await client.Messages.SendMimeAsync("mg.example.com", FromMailMessage(mailMessage));
```

Note: the SDK deliberately does **not** ship an `SmtpClient`-shaped shim — `SmtpClient.Send` is synchronous (sync-over-async deadlock risk under load) and exposes properties (`Credentials`, `Host`, `Port`, `EnableSsl`, `DeliveryMethod`) that have no meaning against an HTTP API. An explicit conversion like the snippet above is honest about what's happening.

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

Requires the `mailgun-dotnet.Webhooks` package (plus `mailgun-dotnet.AspNetCore` for the endpoint helper):

```bash
dotnet add package mailgun-dotnet.Webhooks
dotnet add package mailgun-dotnet.AspNetCore   # optional: only if you want the MapMailgunWebhook endpoint helper
```

Mailgun signs every webhook with HMAC-SHA256 over `timestamp + token`; the SDK verifies it with a constant-time compare and rejects timestamps outside a configurable clock-skew window (default 15 minutes).

**Security model at a glance:**

- **Signature** — `signature == HMAC-SHA256(signing_key, timestamp + token)`, compared with `CryptographicOperations.FixedTimeEquals` (no early-exit timing leak). An empty signing key throws rather than computing a forgeable digest from public inputs.
- **Timestamp freshness** — the signature timestamp must be within `MaxClockSkew` of now (default 15 minutes), so a captured-but-stale request is rejected even if its HMAC is valid. Out-of-range timestamps return `false`, never throw.
- **Replay protection** — **on by default**: `MailgunWebhookEndpointOptions.TokenCache` defaults to an in-process `InMemoryWebhookTokenCache`, so a token replayed within the clock-skew window is rejected (409). Single-process only — see [Replay protection across multiple instances](#replay-protection-across-multiple-instances) for the distributed cache. Set `TokenCache = null` to disable (not recommended).
- **Subaccount `parent-signature`** — payloads from a subaccount domain also carry a `parent-signature` (the same message signed with the *parent* account's key). The default `SignaturePolicy = WebhookSignaturePolicy.AcceptEither` verifies whichever signature matches your configured key, so a parent account can validate every subaccount's webhooks with **one** signing key. See [Subaccount webhooks](#subaccount-webhooks-parent-signature).
- **Body size cap** — the endpoint helper rejects bodies larger than `MaxRequestBytes` (default 256 KB) with 413 before reading or parsing them.

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

Requires the `mailgun-dotnet.AspNetCore` package (in addition to `mailgun-dotnet.Webhooks`):

```bash
dotnet add package mailgun-dotnet.AspNetCore
```

```csharp
app.MapMailgunWebhook("/webhooks/mailgun",
    new MailgunWebhookEndpointOptions
    {
        SigningKey   = builder.Configuration["Mailgun:HttpSigningKey"]!,
        MaxClockSkew = TimeSpan.FromMinutes(15),
        // TokenCache defaults to an InMemoryWebhookTokenCache (replay protection is ON by default);
        // SignaturePolicy defaults to AcceptEither (handles subaccount parent-signatures).
    },
    async (evt, ctx, ct) =>
    {
        // evt is a typed MailgunWebhookEvent (e.g. DeliveredEvent, ClickedEvent).
        // The endpoint already verified the signature + freshness + replay; just handle it.
        await HandleEvent(evt, ct);
    });
```

The helper returns 200 on success, 401 on invalid signature or stale timestamp, 409 on replay-cache hit, 400 on malformed JSON.

### Subaccount webhooks (parent-signature)

When a webhook fires for a domain owned by a **subaccount**, Mailgun's signature object includes an extra `parent-signature` field — the same `timestamp + token` message signed with the **parent account's** signing key, in addition to the subaccount-keyed `signature`. This lets a parent account verify every subaccount's webhooks with a single signing key instead of tracking one key per subaccount.

The endpoint helper handles this automatically: with the default `SignaturePolicy = WebhookSignaturePolicy.AcceptEither`, configuring the helper with your **parent** signing key validates both your own domains' webhooks (via `signature`) and all subaccount webhooks (via `parent-signature`), with no behavioral change for non-subaccount payloads (which carry no parent signature).

```csharp
app.MapMailgunWebhook("/webhooks/mailgun",
    new MailgunWebhookEndpointOptions
    {
        SigningKey      = builder.Configuration["Mailgun:ParentHttpSigningKey"]!, // parent account key
        SignaturePolicy = WebhookSignaturePolicy.AcceptEither,                    // default — shown for clarity
    },
    async (evt, ctx, ct) => await HandleEvent(evt, ct));
```

Verifying raw (any framework) — pass the parsed `parent-signature` and a policy to the validator:

```csharp
var sig = evt.Signature!;  // or MailgunWebhookParser.TryExtractSignature(bodyBytes, out var sig)
bool ok = MailgunWebhookSignatureValidator.IsValid(
    signingKey:      parentSigningKey,
    timestamp:       sig.Timestamp,
    token:           sig.Token,
    signature:       sig.Signature,
    parentSignature: sig.ParentSignature,            // null for non-subaccount payloads
    policy:          WebhookSignaturePolicy.AcceptEither,
    maxAge:          TimeSpan.FromMinutes(15));
```

Policy options: `AcceptEither` (default — verify whichever field matches your key), `ChildSignatureOnly` (only the domain/subaccount-keyed `signature`), `ParentSignatureOnly` (only the `parent-signature`; rejects payloads without one). All three still require a valid HMAC under your configured key, so none weakens forgery resistance.

### Replay protection across multiple instances

Requires the `mailgun-dotnet.Webhooks.DistributedCache` package (when running behind more than one pod / VM / container):

```bash
dotnet add package mailgun-dotnet.Webhooks.DistributedCache
```

`InMemoryWebhookTokenCache` keeps the seen-token set in a per-process `ConcurrentDictionary`. That's the right choice for a single instance but degrades silently when the receiver runs behind more than one pod / VM / container: a token replayed against a different instance hits a fresh dictionary and is accepted. For any multi-instance topology, swap the in-memory cache for an <a href="https://learn.microsoft.com/aspnet/core/performance/caching/distributed" target="_blank" rel="noopener noreferrer"><code>IDistributedCache</code></a>-backed one:

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

The SDK emits a <a href="https://learn.microsoft.com/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs" target="_blank" rel="noopener noreferrer">client span</a> per HTTP call on the `MailgunActivitySource` (name = `"Mailgun"`). Subscribe by registering the source with your OpenTelemetry tracer provider — there are zero NuGet dependencies on OpenTelemetry, and zero cost when no listener is attached.

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
| `http.route` | the parameterized route template (e.g. `v3/{domain}/bounces/{address}`) |
| `url.full` | **PII-redacted** URL — the route template, never the runtime path/query (see below) |
| `server.address` | the Mailgun host (US or EU) |
| `http.response.status_code` | the HTTP response status |
| `mailgun.request_id` | `X-Mailgun-Request-Id` header (when present) |
| `mailgun.rate_limit.remaining` | `X-RateLimit-Remaining` (when present) |
| `exception.type` / `exception.message` | populated on failure |

> **PII redaction.** `url.full` is deliberately set to the low-cardinality **route template**
> (`https://api.mailgun.net/v3/{domain}/bounces/{address}`), not the raw request URL. Several Mailgun
> endpoints carry recipient email addresses in the path (`/bounces/{address}`, `/complaints/{address}`,
> `/unsubscribes/{address}`, mailing-list members) or the query (`/v4/address/validate?address=…`).
> Emitting the raw URL would ship recipient PII to whatever tracing backend is attached — frequently a
> third-party APM with broad access and long retention. The SDK substitutes the template (the same value
> used for the low-cardinality `http.route` metric tag) so addresses never leave the process. This follows
> OpenTelemetry's HTTP semantic conventions, which call for redacting sensitive values from `url.full`.

## OpenTelemetry metrics

Alongside the per-request span, the SDK emits four <a href="https://learn.microsoft.com/dotnet/core/diagnostics/metrics-instrumentation" target="_blank" rel="noopener noreferrer">instruments</a> on a `MailgunMeter` (name = `"Mailgun"`, same as the activity source). Subscribe by registering the meter with your OpenTelemetry meter provider — zero NuGet dependencies, zero cost when no listener is attached.

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

Requires the `mailgun-dotnet.Extensions.DependencyInjection` package:

```bash
dotnet add package mailgun-dotnet.Extensions.DependencyInjection
```

```csharp
builder.Services.AddMailgun(o =>
{
    o.ApiKey = builder.Configuration["Mailgun:ApiKey"];
    o.Region = MailgunRegion.Eu;
});

// Inject IMailgunClient anywhere.
```

### Production ASP.NET Core registration

`AddMailgun` registers `IMailgunClient` as a **singleton** whose transport is the named
`IHttpClientFactory` client (`MailgunServiceCollectionExtensions.HttpClientName == "Mailgun"`), so
handler rotation, DNS refresh, and socket pooling follow standard .NET guidance — you do **not** manage
`HttpClient` lifetime yourself. It returns the `IHttpClientBuilder`, so resilience and observability
policies chain on top of the SDK's built-in 429 / idempotent-5xx retries:

```csharp
builder.Services
    .AddMailgun(builder.Configuration)          // binds the "Mailgun" config section
    .AddStandardResilienceHandler();            // Polly-backed retry/circuit-breaker (optional)

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(MailgunActivitySource.Name).AddOtlpExporter())
    .WithMetrics(m => m.AddMeter(MailgunMeter.Name).AddOtlpExporter());
```

Every `MailgunClientOptions` value (including `AllowInsecureBaseUrl`, `MaxResponseContentBytes`,
`OnResponse`, and `OnBehalfOf`) is honored identically whether you construct `MailgunClient` directly or
through DI — the DI projection clones the full options object, so direct-vs-DI behavior never diverges.

> **Options snapshot.** The singleton reads its options once at first resolve; rotating the API key in
> `appsettings.json` after startup does not propagate to the running client. Rebuild the provider or
> restart the process for a key rotation to take effect.

## Configuration & resilience

`MailgunClientOptions` controls the transport. Key knobs:

| Option | Default | Behavior |
|---|---|---|
| `MaxRetries` | `3` | Retries on HTTP 429 (honoring `X-RateLimit-Reset`, then `Retry-After`, then exponential backoff with ±20 % jitter) and on idempotent-method 5xx (GET/HEAD/PUT/DELETE/OPTIONS — POSTs are never replayed). Action endpoints like DKIM `…/rotate` are excluded so a transient 5xx can't double-rotate. |
| `Timeout` | `100 s` | Per-request timeout. Ignored when you supply your own `HttpClient` (you own its `Timeout`). |
| `PooledConnectionLifetime` | `2 min` | The SDK-owned transport uses `SocketsHttpHandler` and recycles pooled connections on this cadence so a long-lived singleton client picks up DNS changes after a Mailgun failover. Ignored when you supply your own `HttpClient` (or use DI — `IHttpClientFactory` manages handler rotation). |
| `MaxResponseContentBytes` | `64 MiB` | Hard cap on the response body the SDK buffers into memory; a compromised or MITM'd endpoint streaming an oversized body is rejected (`MailgunSerializationException`) instead of exhausting memory. Enforced even when you supply your own `HttpClient`. |
| `AllowInsecureBaseUrl` | `false` | When `false`, a non-HTTPS, non-loopback `BaseUrl` throws at construction (the Basic-auth API key must not be sent in cleartext). Set `true` only for a trusted self-hosted gateway on a private network. |
| `OnResponse` | `null` | Per-request callback (status, request id, rate-limit headers) that fires synchronously on the caller's flow — the concurrency-safe alternative to `LastResponseMetadata`. |

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

<a href="https://stryker-mutator.io/docs/stryker-net/introduction/" target="_blank" rel="noopener noreferrer">Stryker.NET</a> is wired as a local dotnet tool. It mutates SDK source (skipping pure DTOs under `Models/`) and runs the test suite against each mutant to measure whether the tests actually *catch* bugs — a more honest signal than a passing test count.

```bash
dotnet tool restore
dotnet stryker
```

The HTML report lands in `StrykerOutput/<timestamp>/reports/mutation-report.html`. Configuration is in [stryker-config.json](./stryker-config.json).

The test suite is currently **638 tests** (net8.0 + net10.0). The mutation-score figures below are from the last full Stryker run captured in the progression table; the score moves as the SDK's code surface and the survivor-triage passes evolve, so treat the live [dashboard badge](https://dashboard.stryker-mutator.io/reports/github.com/AndreasLohr/mailgun-dotnet/main) at the top of this README as the source of truth and re-run `dotnet stryker` after substantial changes.

- **4 real bugs found and fixed during earlier survivor-by-survivor triage**: `Templates.CreateVersionAsync`, `InboxPlacement.CreateSeedlistAsync` / `CreateTestAsync`, `DynamicIpPools.CreateAsync`, and `Users.CreateAsync` were all missing required-field validation on their typed request DTOs (an empty `Name` / `Email` / `Tag` / `Seedlist` would have been sent to Mailgun as an empty string instead of throwing `ArgumentException` at the call site).

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
| **v0.8.x feature snapshot** — OpenTelemetry metrics surface, distributed-cache replay protection, webhook crypto / multipart-copy hardening, Roslyn cardinality guardrail. New code surface added faster than survivor triage caught up; awaiting a dedicated triage round | 445 | 1143 | 592 | 77 | 63.2% |

## Target frameworks & support matrix

The SDK targets **`net8.0` (LTS)** and **`net10.0`** — both currently in Microsoft support. This is a
deliberate modern-only policy: there is no `netstandard2.0` target.

- **Rationale.** The SDK leans on modern BCL primitives that have no clean `netstandard2.0` equivalent —
  `SocketsHttpHandler.PooledConnectionLifetime`, `CryptographicOperations.FixedTimeEquals`,
  `JsonNamingPolicy.SnakeCaseLower` (.NET 8+), `IAsyncEnumerable<T>` pagination, `init`-only setters, and
  nullable reference types. Backporting via polyfills would trade real safety/clarity for reach that
  every in-support .NET runtime already provides. If you need an older runtime, pin an earlier SDK or
  open an issue describing the target.
- **CI** builds and runs the full test suite on **Ubuntu and Windows**, against **both** target
  frameworks, on every push and PR (`.github/workflows/ci.yml`), plus a `.NET 8 SDK-only` job that
  exercises the stricter .NET 8 analyzer baseline in isolation. Analyzer warnings are treated as errors
  and nullable is clean.

| Package | TFMs | Purpose |
|---|---|---|
| `mailgun-dotnet` | net8.0, net10.0 | Core client, all resource services, telemetry. Zero `Microsoft.Extensions.*` deps. |
| `mailgun-dotnet.Extensions.DependencyInjection` | net8.0, net10.0 | `IServiceCollection.AddMailgun()` via `IHttpClientFactory`. |
| `mailgun-dotnet.Webhooks` | net8.0, net10.0 | Typed webhook parsing + signature verification. |
| `mailgun-dotnet.AspNetCore` | net8.0, net10.0 | `MapMailgunWebhook` endpoint helper. |
| `mailgun-dotnet.Webhooks.DistributedCache` | net8.0, net10.0 | `IDistributedCache`-backed replay protection. |
| `mailgun-dotnet.MimeKit` | net8.0, net10.0 | `SendMimeAsync(MimeMessage)` interop. |

### Trimming & Native AOT

The SDK serializes through reflection-based `System.Text.Json` — `MailgunClient` is annotated
`[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` to surface that to the compiler.

- **Trimming** — works with `TrimMode=partial` when the SDK assembly is rooted (the default for a
  referenced library). `TrimMode=full` will emit trim warnings and may drop reflection metadata at
  runtime; either keep partial trimming or add `<TrimmerRootAssembly Include="Mailgun" />` (and the
  companion assemblies you use).
- **Native AOT is not supported.** This is intentional, not an oversight: the public surface exposes
  arbitrary-shape Mailgun JSON as `Dictionary<string, object>` (webhook `user-variables`, alert
  `settings`, and similar caller-defined blobs). `object`-typed serialization is inherently
  runtime-typed and cannot be made AOT-safe without either dropping those fields or replacing them with
  `JsonElement` — both worse for the 99% of users who never publish AOT. If first-class AOT matters to
  you, open an issue; a source-generated `JsonSerializerContext` over the closed-shape DTOs (excluding
  the `object` blobs) is the migration path under consideration.

## Troubleshooting

| Symptom | Likely cause & fix |
|---|---|
| `ArgumentException: …ApiKey is required` at construction | `ApiKey` is unset. In DI, ensure the `"Mailgun"` config section (or your `configureOptions`) sets it; `AddMailgun` validates it on first resolve. |
| `ArgumentException: …BaseUrl must use HTTPS` | You set a non-HTTPS, non-loopback `BaseUrl`. Use `https://`, a loopback host for local tests, or set `AllowInsecureBaseUrl = true` for a trusted private gateway. |
| `401 Unauthorized` from the API | Wrong key for the operation — sending keys only authorize `POST /v3/{domain}/messages[.mime]`; account-scoped calls need a primary/account key. Check you're using the right **region** (`MailgunRegion.Eu` → `api.eu.mailgun.net`); a US key on the EU host (or vice-versa) 401s. |
| `MailgunRateLimitException` despite retries | The SDK already retried `MaxRetries` times honoring `X-RateLimit-Reset`. Back off further, lower send concurrency, or raise `MaxRetries`. Inspect `ex.RateLimit?.Reset`. |
| `MailgunSerializationException: …exceeds the configured …-byte limit` | A response body exceeded `MaxResponseContentBytes` (default 64 MiB). Raise the cap if you legitimately fetch very large payloads, or investigate an unexpected/oversized response. |
| Auto-pagination throws `MailgunSerializationException` mid-stream | A server-supplied `paging.next` link pointed off-origin or to a non-HTTPS URL; the SDK refuses to follow it (it would forward your API key). Expected hardening — not a bug. |
| Webhook receiver returns `401` | Signature/timestamp check failed: wrong signing key, clock skew beyond `MaxClockSkew`, or (for subaccount domains) you configured a child key but receive `parent-signature` only — use the parent key with the default `AcceptEither` policy. |
| Webhook receiver returns `409` | Replay protection rejected a token already seen within the window. Behind multiple instances, wire up `mailgun-dotnet.Webhooks.DistributedCache` so all instances share the seen-token set. |
| `LastResponseMetadata` shows another request's data | It's a single shared field — unsafe under concurrency (e.g. the DI singleton). Use the `OnResponse` callback, which fires per-request on the caller's flow. |

## License

Proprietary — see [LICENSE](./LICENSE). All rights reserved.

---

*Mailgun is a registered trademark of Sinch Email (Mailgun Technologies, Inc.). This project is an independent, community-built .NET client for Mailgun's public HTTP API and is not affiliated with, endorsed by, or sponsored by Sinch / Mailgun Technologies, Inc. All product and company names are trademarks or registered trademarks of their respective holders; use of them does not imply any affiliation or endorsement.*
