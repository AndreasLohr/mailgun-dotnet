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
        return _http.GetJsonAsync<WebhooksMap>($"v4/domains/{PathEscape.Segment(domain)}/webhooks", null, cancellationToken);
    }

    public Task<WebhookResponse> GetDomainAsync(string domain, string eventType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return _http.GetJsonAsync<WebhookResponse>(
            $"v4/domains/{PathEscape.Segment(domain)}/webhooks/{PathEscape.Segment(eventType)}", null, cancellationToken);
    }

    public Task<WebhookResponse> CreateDomainAsync(string domain, string eventType, IReadOnlyList<string> urls, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return _http.PostFormAsync<WebhookResponse>(
            $"v4/domains/{PathEscape.Segment(domain)}/webhooks",
            BuildWebhookForm(eventType, urls),
            cancellationToken);
    }

    public Task<WebhookResponse> UpdateDomainAsync(string domain, string eventType, IReadOnlyList<string> urls, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        var fb = new FormBuilder();
        AddUrls(fb, urls);
        return _http.PutFormAsync<WebhookResponse>(
            $"v4/domains/{PathEscape.Segment(domain)}/webhooks/{PathEscape.Segment(eventType)}",
            fb, cancellationToken);
    }

    public Task DeleteDomainAsync(string domain, string eventType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return _http.DeleteNoResponseAsync(
            $"v4/domains/{PathEscape.Segment(domain)}/webhooks/{PathEscape.Segment(eventType)}", cancellationToken);
    }

    // ---------- Modern ID-based account-webhook API ----------

    public Task<AccountWebhookListResponse> ListAccountWebhooksAsync(IReadOnlyList<string>? webhookIds = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder().AddArray("webhook_ids", webhookIds).Build();
        return _http.GetJsonAsync<AccountWebhookListResponse>("v1/webhooks", q, cancellationToken);
    }

    public Task<AccountWebhook> GetAccountWebhookAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _http.GetJsonAsync<AccountWebhook>($"v1/webhooks/{PathEscape.Segment(id)}", null, cancellationToken);
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
        return await _http.PostMultipartAsync<AccountWebhook>("v1/webhooks", mp, cancellationToken).ConfigureAwait(false);
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
            $"v1/webhooks/{PathEscape.Segment(id)}", mp, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAccountWebhookAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _http.DeleteNoResponseAsync($"v1/webhooks/{PathEscape.Segment(id)}", cancellationToken);
    }

    public Task DeleteAccountWebhooksAsync(
        IReadOnlyList<string>? webhookIds = null,
        bool all = false,
        CancellationToken cancellationToken = default)
    {
        if (!all && (webhookIds is null || webhookIds.Count == 0))
            throw new ArgumentException("Supply webhookIds or set all=true.", nameof(webhookIds));

        var q = new QueryBuilder().AddArray("webhook_ids", webhookIds);
        if (all)
            q.Add("all", "true");
        var qb = q.Build();
        var query = qb.Count == 0 ? string.Empty :
            "?" + string.Join("&", qb.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
        return _http.DeleteNoResponseAsync($"v1/webhooks{query}", cancellationToken);
    }

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
