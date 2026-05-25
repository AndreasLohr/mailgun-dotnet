using Mailgun;
using Mailgun.Exceptions;
using Mailgun.Models.Analytics;
using Mailgun.Models.Messages;
using Mailgun.Webhooks;

var apiKey = Environment.GetEnvironmentVariable("MAILGUN_API_KEY");
var domain = Environment.GetEnvironmentVariable("MAILGUN_DOMAIN");
var regionEnv = Environment.GetEnvironmentVariable("MAILGUN_REGION");

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Set the MAILGUN_API_KEY environment variable to run this sample.");
    return 1;
}

var region = string.Equals(regionEnv, "eu", StringComparison.OrdinalIgnoreCase)
    ? MailgunRegion.Eu
    : MailgunRegion.Us;

using var client = new MailgunClient(new MailgunClientOptions
{
    ApiKey = apiKey,
    Region = region,
});

try
{
    if (!string.IsNullOrWhiteSpace(domain))
    {
        Console.WriteLine($"== Send a test message via {domain} ==");
        var sent = await client.Messages.SendAsync(domain, new SendMessageRequest
        {
            From = $"Mailgun .NET SDK Demo <postmaster@{domain}>",
            To = { $"andreas+demo@{domain}" },
            Subject = "mailgun-dotnet demo",
            Text = "Hello from the mailgun-dotnet SDK sample app.",
            TestMode = true,
        });
        Console.WriteLine($"  queued id: {sent.Id} — {sent.Message}");
    }

    Console.WriteLine("\n== List first page of domains ==");
    var domainsPage = await client.Domains.ListAsync(new() { Limit = 5 });
    foreach (var d in domainsPage.Items)
    {
        Console.WriteLine($"  {d.Name} [{d.State}]");
    }

    Console.WriteLine("\n== Query last-7-days metrics ==");
    var end = DateTimeOffset.UtcNow;
    var start = end.AddDays(-7);
    var metrics = await client.Analytics.QueryMetricsAsync(new MetricsRequest
    {
        Start = start.ToString("r"),
        End = end.ToString("r"),
        Resolution = "1d",
        Dimensions = new() { "time" },
        Metrics = new() { "accepted_count", "delivered_count", "failed_count" },
    });
    if (metrics.Items is { } items)
    {
        foreach (var row in items)
        {
            var time = row.Dimensions?.FirstOrDefault()?.Value;
            var accepted = row.Metrics?.GetValueOrDefault("accepted_count");
            var delivered = row.Metrics?.GetValueOrDefault("delivered_count");
            var failed = row.Metrics?.GetValueOrDefault("failed_count");
            Console.WriteLine($"  {time}: accepted={accepted} delivered={delivered} failed={failed}");
        }
    }

    if (!string.IsNullOrWhiteSpace(domain))
    {
        Console.WriteLine("\n== Iterate first 10 bounces (auto-paginated) ==");
        var count = 0;
        await foreach (var bounce in client.Suppressions.Bounces.ListAllAsync(domain))
        {
            Console.WriteLine($"  {bounce.Address} [{bounce.Code}]");
            if (++count >= 10) break;
        }
    }

    Console.WriteLine("\n== Verify a webhook signature ==");
    var signingKey = "key-demo";
    var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
    var token = "demo-token";
    using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.ASCII.GetBytes(signingKey));
    var hash = hmac.ComputeHash(System.Text.Encoding.ASCII.GetBytes(ts + token));
    var signature = Convert.ToHexString(hash).ToLowerInvariant();
    var valid = MailgunWebhookSignatureValidator.IsValid(signingKey, ts, token, signature, TimeSpan.FromMinutes(15));
    Console.WriteLine($"  fresh signature valid: {valid}");

    if (client.LastResponseMetadata is { } md)
    {
        Console.WriteLine($"\nLast request id: {md.RequestId ?? "(none)"}, rate-limit remaining: {md.RateLimitRemaining?.ToString() ?? "(none)"}");
    }
}
catch (MailgunRateLimitException ex)
{
    Console.Error.WriteLine($"Rate limited. Reset at: {ex.RateLimit?.Reset}");
    return 3;
}
catch (MailgunApiException ex)
{
    Console.Error.WriteLine($"Mailgun API error: HTTP {(int)ex.StatusCode} {ex.StatusCode}: {ex.ErrorMessage}");
    foreach (var d in ex.Details)
        Console.Error.WriteLine($"  - {d}");
    return 2;
}

return 0;
