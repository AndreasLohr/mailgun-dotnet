using Mailgun;
using Mailgun.Exceptions;
using Mailgun.Models.Analytics;
using Mailgun.Services;

// Ruthless read-only sweep across every SDK service the developer key can touch.
// Each endpoint is a separate Section so one 404/permission-denied doesn't kill the run.

var apiKey = Environment.GetEnvironmentVariable("MAILGUN_API_KEY")
    ?? throw new InvalidOperationException("Set MAILGUN_API_KEY first.");
var domain = Environment.GetEnvironmentVariable("MAILGUN_DOMAIN")
    ?? throw new InvalidOperationException("Set MAILGUN_DOMAIN first.");

using var client = new MailgunClient(new MailgunClientOptions { ApiKey = apiKey });

var pass = 0;
var fail = 0;
var failures = new List<string>();

async Task RunAsync(string title, Func<Task> body)
// suppress VSTHRD200: local function intentionally not in "VerbAsync" form (it wraps a section).
{
    Console.Write($"  {title,-60}");
    try { await body(); Console.WriteLine("  OK"); pass++; }
    catch (MailgunApiException ex)
    {
        Console.WriteLine($"  FAIL HTTP {(int)ex.StatusCode}: {ex.ErrorMessage}");
        fail++; failures.Add($"{title} → HTTP {(int)ex.StatusCode} {ex.ErrorMessage}");
    }
    catch (Exception ex)
    {
        var inner = ex.InnerException is null ? "" : $" | inner={ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
        Console.WriteLine($"  FAIL {ex.GetType().Name}: {ex.Message}{inner}");
        fail++; failures.Add($"{title} → {ex.GetType().Name} {ex.Message}{inner}");
    }
}

Console.WriteLine("\n── Account-level reads ──");
await RunAsync("Account.GetFeaturesAsync",                      async () => { var f = await client.Account.GetFeaturesAsync(); Console.Write($"  → {f.Count} features"); });
await RunAsync("Account.GetHttpSigningKeyAsync",                async () => { _ = await client.Account.GetHttpSigningKeyAsync(); Console.Write($"  → loaded"); });
await RunAsync("Account.ListSandboxAuthRecipientsAsync",        async () => { _ = await client.Account.ListSandboxAuthRecipientsAsync(); Console.Write($"  → loaded"); });
await RunAsync("Keys.ListAsync (limit 5)",                      async () => { var r = await client.Keys.ListAsync(limit: 5); Console.Write($"  → {r.Items.Count} keys"); });
await RunAsync("Users.ListAsync (limit 5)",                     async () => { var r = await client.Users.ListAsync(limit: 5); Console.Write($"  → {r.Items?.Count ?? 0} users"); });
await RunAsync("Limits.GetAsync",                               async () => { _ = await client.Limits.GetAsync(); Console.Write($"  → loaded"); });
await RunAsync("Limits.GetUsageAsync",                          async () => { _ = await client.Limits.GetUsageAsync(); Console.Write($"  → loaded"); });
await RunAsync("SendAlerts.GetConfigAsync",                     async () => { var c = await client.SendAlerts.GetConfigAsync(); Console.Write($"  → enabled={c.Enabled}"); });
await RunAsync("SendAlerts.ListQueuesAsync",                    async () => { var q = await client.SendAlerts.ListQueuesAsync(); Console.Write($"  → {q.Items?.Count ?? 0} queues"); });
await RunAsync("Subaccounts.ListAsync (limit 5)",               async () => { var r = await client.Subaccounts.ListAsync(limit: 5); Console.Write($"  → {r.Subaccounts?.Count ?? 0} subaccounts"); });
await RunAsync("CustomMessageLimit.GetAsync",                   async () => { var l = await client.CustomMessageLimit.GetAsync(); Console.Write($"  → limit={l.Limit}"); });

Console.WriteLine("\n── Top-level entity lists ──");
await RunAsync("Domains.ListAsync (limit 5)",                   async () => { var r = await client.Domains.ListAsync(new() { Limit = 5 }); Console.Write($"  → {r.Items.Count} domains"); });
await RunAsync("Routes.ListAsync (limit 5)",                    async () => { var r = await client.Routes.ListAsync(limit: 5); Console.Write($"  → {r.Items.Count} routes"); });
await RunAsync("Templates.ListAsync (limit 5)",                 async () => { var r = await client.Templates.ListAsync(limit: 5); Console.Write($"  → {r.Items.Count} templates"); });
await RunAsync("MailingLists.ListAsync (limit 5)",              async () => { var r = await client.MailingLists.ListAsync(limit: 5); Console.Write($"  → {r.Items.Count} lists"); });
await RunAsync("Ips.ListAsync",                                 async () => { var r = await client.Ips.ListAsync(); Console.Write($"  → {r.Items?.Count ?? 0} ips"); });
await RunAsync("IpPools.ListAsync",                             async () => { var r = await client.IpPools.ListAsync(); Console.Write($"  → {r.IpPools?.Count ?? 0} pools"); });
await RunAsync("IpWarmups.ListAsync",                           async () => { var r = await client.IpWarmups.ListAsync(); Console.Write($"  → {r.Items?.Count ?? 0} warmups"); });
await RunAsync("DynamicIpPools.ListAsync",                      async () => { var r = await client.DynamicIpPools.ListAsync(); Console.Write($"  → {r.DynamicPools?.Count ?? 0} dynamic-pools"); });
await RunAsync("BounceClassification.ListAsync",                async () => { var r = await client.BounceClassification.ListAsync(); Console.Write($"  → {r.Items?.Count ?? 0} classifications"); });

Console.WriteLine("\n── Alerts ──");
await RunAsync("Alerts.GetSettingsAsync",                       async () => { var s = await client.Alerts.GetSettingsAsync(); Console.Write($"  → loaded"); });
await RunAsync("Alerts.ListEventsAsync",                        async () => { var r = await client.Alerts.ListEventsAsync(); Console.Write($"  → {r.Items?.Count ?? 0} events"); });
await RunAsync("Alerts.ListSlackChannelsAsync",                 async () => { var r = await client.Alerts.ListSlackChannelsAsync(); Console.Write($"  → {r.Items?.Count ?? 0} channels"); });
await RunAsync("Alerts.ListEmailsAsync",                        async () => { var r = await client.Alerts.ListEmailsAsync(); Console.Write($"  → {r.Items?.Count ?? 0} emails"); });
await RunAsync("Alerts.ListWebhooksAsync",                      async () => { var r = await client.Alerts.ListWebhooksAsync(); Console.Write($"  → {r.Items?.Count ?? 0} webhooks"); });

Console.WriteLine("\n── DKIM / Webhooks (account-level) ──");
await RunAsync("DkimKeys.ListAllAsync (limit 5)",               async () => { var r = await client.DkimKeys.ListAllAsync(limit: 5); Console.Write($"  → {r.Items?.Count ?? 0} keys"); });
await RunAsync("Webhooks.ListAccountWebhooksAsync (new)",       async () => { var r = await client.Webhooks.ListAccountWebhooksAsync(); Console.Write($"  → {r.Webhooks.Count} webhooks"); });

Console.WriteLine($"\n── Per-domain reads ({domain}) ──");
await RunAsync("Domains.GetAsync",                              async () => { var r = await client.Domains.GetAsync(domain); Console.Write($"  → state={r.Domain?.State}"); });
await RunAsync("Domains.GetTrackingAsync",                      async () => { var r = await client.Domains.GetTrackingAsync(domain); Console.Write($"  → click={r.Click?.Active} open={r.Open?.Active}"); });
await RunAsync("Domains.ListSmtpCredentialsAsync (limit 5)",    async () => { var r = await client.Domains.ListSmtpCredentialsAsync(domain, limit: 5); Console.Write($"  → {r.Items.Count} credentials"); });
await RunAsync("DkimKeys.ListForAuthorityAsync",                async () => { var r = await client.DkimKeys.ListForAuthorityAsync(domain); Console.Write($"  → {r.Items?.Count ?? 0} keys"); });
await RunAsync("DkimSecurity.GetAutoRotationAsync",             async () => { var r = await client.DkimSecurity.GetAutoRotationAsync(domain); Console.Write($"  → enabled={r.Enabled}"); });
await RunAsync("Webhooks.ListDomainAsync",                      async () => { var r = await client.Webhooks.ListDomainAsync(domain); Console.Write($"  → {r.Webhooks.Count} webhooks"); });

Console.WriteLine("\n── Suppressions (per-domain) ──");
await RunAsync("Suppressions.Bounces.ListAsync",                async () => { var r = await client.Suppressions.Bounces.ListAsync(domain); Console.Write($"  → {r.Items.Count} bounces"); });
await RunAsync("Suppressions.Complaints.ListAsync",             async () => { var r = await client.Suppressions.Complaints.ListAsync(domain); Console.Write($"  → {r.Items.Count} complaints"); });
await RunAsync("Suppressions.Unsubscribes.ListAsync",           async () => { var r = await client.Suppressions.Unsubscribes.ListAsync(domain); Console.Write($"  → {r.Items.Count} unsubs"); });
await RunAsync("Suppressions.Allowlists.ListAsync",             async () => { var r = await client.Suppressions.Allowlists.ListAsync(domain); Console.Write($"  → {r.Items.Count} entries"); });

Console.WriteLine("\n── Analytics (query-only POST endpoints, no state change) ──");
var now = DateTimeOffset.UtcNow;
var weekAgo = now.AddDays(-7);
await RunAsync("Analytics.QueryMetricsAsync (last 7 days)", async () =>
{
    var r = await client.Analytics.QueryMetricsAsync(new MetricsRequest
    {
        Start = weekAgo.ToString("r"),
        End = now.ToString("r"),
        Resolution = "day",
        Dimensions = new() { "time" },
        Metrics = new() { "accepted_count", "delivered_count" },
    });
    Console.Write($"  → {r.Items?.Count ?? 0} rows");
});
await RunAsync("Analytics.QueryUsageMetricsAsync (last 7 days)", async () =>
{
    var r = await client.Analytics.QueryUsageMetricsAsync(new UsageMetricsRequest
    {
        Start = weekAgo.ToString("r"),
        End = now.ToString("r"),
        Resolution = "day",
    });
    Console.Write($"  → {r.Items?.Count ?? 0} rows");
});
await RunAsync("Analytics.QueryLogsAsync (last 3 days)", async () =>
{
    // Mailgun caps log retention at 5 days for this plan, so we ask for 3.
    var r = await client.Analytics.QueryLogsAsync(new LogsRequest
    {
        // Logs endpoint is strict — requires the -0000 form (not GMT).
        Start = AnalyticsTime.Format(now.AddDays(-3)),
        End = AnalyticsTime.Format(now),
        Events = new() { "delivered", "failed" },
    });
    Console.Write($"  → {r.Items?.Count ?? 0} log lines");
});
await RunAsync("AnalyticsTags.ListAsync (POST)",                async () => { var r = await client.AnalyticsTags.ListAsync(new() { Pagination = new() { Limit = 5 } }); Console.Write($"  → {r.Items?.Count ?? 0} tags"); });
await RunAsync("AnalyticsTags.GetLimitsAsync",                  async () => { var r = await client.AnalyticsTags.GetLimitsAsync(); Console.Write($"  → limit={r.Limit} count={r.Count}"); });

Console.WriteLine("\n── Inbox Placement (read) ──");
await RunAsync("InboxPlacement.ListSeedlistsAsync",             async () => { var r = await client.InboxPlacement.ListSeedlistsAsync(); Console.Write($"  → {r.Items?.Count ?? 0} seedlists"); });
await RunAsync("InboxPlacement.ListResultsAsync",               async () => { var r = await client.InboxPlacement.ListResultsAsync(); Console.Write($"  → {r.Items?.Count ?? 0} results"); });
await RunAsync("InboxPlacement.ListProvidersAsync",             async () => { var r = await client.InboxPlacement.ListProvidersAsync(); Console.Write($"  → {r.Items?.Count ?? 0} providers"); });

Console.WriteLine("\n── Validate (single-address, read) ──");
await RunAsync("Validate.ValidateAsync (alice@example.com)",    async () => { var v = await client.Validate.ValidateAsync("alice@example.com"); Console.Write($"  → result={v.Result} risk={v.Risk}"); });
await RunAsync("Validate.ValidateAsync (intentional typo)",     async () => { var v = await client.Validate.ValidateAsync("alice@gmial.cmo"); Console.Write($"  → result={v.Result} did_you_mean={v.DidYouMean ?? "(none)"}"); });
await RunAsync("Validate.ListBulkAsync",                        async () => { var r = await client.Validate.ListBulkAsync(); Console.Write($"  → {r.Jobs?.Count ?? 0} bulk jobs"); });
await RunAsync("Validate.ListBulkPreviewsAsync",                async () => { var r = await client.Validate.ListBulkPreviewsAsync(); Console.Write($"  → {r.Previews?.Count ?? 0} previews"); });

Console.WriteLine($"\n══════════════════════════════════════════════════════════════════");
Console.WriteLine($"  Result: {pass} pass / {fail} fail");
if (fail > 0)
{
    Console.WriteLine("\n  Failures:");
    foreach (var f in failures) Console.WriteLine($"    • {f}");
}
return fail == 0 ? 0 : 1;
