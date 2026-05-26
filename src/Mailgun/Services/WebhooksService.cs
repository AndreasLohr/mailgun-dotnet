using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Models.Webhooks;

namespace Mailgun.Services;

internal sealed class WebhooksService : IWebhooksService
{
    private readonly MailgunHttpClient _http;
    public WebhooksService(MailgunHttpClient http) => _http = http;

    public Task<WebhooksMap> ListDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.GetJsonAsync<WebhooksMap>($"v3/domains/{PathEscape.Segment(domain)}/webhooks", null, cancellationToken, routeTemplate: "v3/domains/{domain}/webhooks");
    }

    public Task<WebhookResponse> GetDomainAsync(string domain, string eventType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return _http.GetJsonAsync<WebhookResponse>(
            $"v3/domains/{PathEscape.Segment(domain)}/webhooks/{PathEscape.Segment(eventType)}", null, cancellationToken,
            routeTemplate: "v3/domains/{domain}/webhooks/{event_type}");
    }

    public Task<WebhookResponse> CreateDomainAsync(string domain, string eventType, IReadOnlyList<string> urls, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return _http.PostFormAsync<WebhookResponse>(
            $"v3/domains/{PathEscape.Segment(domain)}/webhooks",
            BuildWebhookForm(eventType, urls),
            cancellationToken,
            routeTemplate: "v3/domains/{domain}/webhooks");
    }

    public Task<WebhookResponse> UpdateDomainAsync(string domain, string eventType, IReadOnlyList<string> urls, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        var fb = new FormBuilder();
        AddUrls(fb, urls);
        return _http.PutFormAsync<WebhookResponse>(
            $"v3/domains/{PathEscape.Segment(domain)}/webhooks/{PathEscape.Segment(eventType)}",
            fb, cancellationToken,
            routeTemplate: "v3/domains/{domain}/webhooks/{event_type}");
    }

    public Task DeleteDomainAsync(string domain, string eventType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return _http.DeleteNoResponseAsync(
            $"v3/domains/{PathEscape.Segment(domain)}/webhooks/{PathEscape.Segment(eventType)}", cancellationToken,
            routeTemplate: "v3/domains/{domain}/webhooks/{event_type}");
    }

    // ---------- Modern ID-based account-webhook API ----------

    public Task<AccountWebhookListResponse> ListAccountWebhooksAsync(IReadOnlyList<string>? webhookIds = null, CancellationToken cancellationToken = default)
    {
        // Mailgun documents webhook_ids as a single comma-separated query parameter
        // (e.g. ?webhook_ids=a,b,c) — NOT as repeated webhook_ids=a&webhook_ids=b query params.
        var q = new QueryBuilder().Add("webhook_ids", JoinIdsOrNull(webhookIds)).Build();
        return _http.GetJsonAsync<AccountWebhookListResponse>("v1/webhooks", q, cancellationToken, routeTemplate: "v1/webhooks");
    }

    public Task<AccountWebhook> GetAccountWebhookAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _http.GetJsonAsync<AccountWebhook>($"v1/webhooks/{PathEscape.Segment(id)}", null, cancellationToken, routeTemplate: "v1/webhooks/{webhook_id}");
    }

    public async Task<AccountWebhook> CreateAccountWebhookAsync(
        string url,
        IReadOnlyList<string> eventTypes,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(eventTypes);
        if (eventTypes.Count == 0)
            throw new ArgumentException("At least one event type is required.", nameof(eventTypes));

        // Mailgun documents v1/webhooks as multipart/form-data only.
        using var mp = new MultipartBuilder()
            .AddText("url", url)
            .AddText("description", description)
            .AddTextArray("event_types", eventTypes);
        return await _http.PostMultipartAsync<AccountWebhook>("v1/webhooks", mp, cancellationToken, routeTemplate: "v1/webhooks").ConfigureAwait(false);
    }

    public async Task UpdateAccountWebhookAsync(
        string id,
        string url,
        IReadOnlyList<string> eventTypes,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(eventTypes);
        if (eventTypes.Count == 0)
            throw new ArgumentException("At least one event type is required.", nameof(eventTypes));

        using var mp = new MultipartBuilder()
            .AddText("url", url)
            .AddText("description", description)
            .AddTextArray("event_types", eventTypes);
        // Mailgun returns 204 No Content on success — no JSON body to parse.
        await _http.PutMultipartNoResponseAsync(
            $"v1/webhooks/{PathEscape.Segment(id)}", mp, cancellationToken,
            routeTemplate: "v1/webhooks/{webhook_id}").ConfigureAwait(false);
    }

    public Task DeleteAccountWebhookAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _http.DeleteNoResponseAsync($"v1/webhooks/{PathEscape.Segment(id)}", cancellationToken, routeTemplate: "v1/webhooks/{webhook_id}");
    }

    public Task DeleteAccountWebhooksAsync(
        IReadOnlyList<string>? webhookIds = null,
        bool all = false,
        CancellationToken cancellationToken = default)
    {
        if (!all && (webhookIds is null || webhookIds.Count == 0))
            throw new ArgumentException("Supply webhookIds or set all=true.", nameof(webhookIds));

        // Same comma-separated convention as the LIST endpoint — NOT repeated query params.
        var qb = new QueryBuilder().Add("webhook_ids", JoinIdsOrNull(webhookIds));
        if (all)
            qb.Add("all", "true");
        return _http.DeleteNoResponseAsync("v1/webhooks", qb.Build(), cancellationToken, routeTemplate: "v1/webhooks");
    }

    private static string? JoinIdsOrNull(IReadOnlyList<string>? ids) =>
        ids is null || ids.Count == 0 ? null : string.Join(",", ids);

    private static FormBuilder BuildWebhookForm(string eventType, IReadOnlyList<string> urls)
    {
        var fb = new FormBuilder().Add("id", eventType);
        AddUrls(fb, urls);
        return fb;
    }

    private static void AddUrls(FormBuilder fb, IReadOnlyList<string> urls)
    {
        ArgumentNullException.ThrowIfNull(urls);
        if (urls.Count == 0)
            throw new ArgumentException("At least one URL is required.", nameof(urls));
        if (urls.Count > 3)
            throw new ArgumentException("Mailgun allows at most 3 webhook URLs per event type.", nameof(urls));
        foreach (var u in urls)
            fb.Add("url", u);
    }
}
