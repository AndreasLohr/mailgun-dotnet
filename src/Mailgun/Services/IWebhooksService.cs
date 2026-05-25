using Mailgun.Models.Webhooks;

namespace Mailgun.Services;

/// <summary>
/// Account-level webhooks (<c>/v1/webhooks</c>) and per-domain webhooks (<c>/v4/domains/{domain}/webhooks</c>).
/// Event types: <c>accepted</c>, <c>delivered</c>, <c>opened</c>, <c>clicked</c>, <c>unsubscribed</c>,
/// <c>complained</c>, <c>permanent_fail</c>, <c>temporary_fail</c>. Each event supports up to 3 destination URLs.
/// </summary>
public interface IWebhooksService
{
    /// <summary><c>GET /v4/domains/{domain}/webhooks</c> — full webhook map for the domain.</summary>
    Task<WebhooksMap> ListDomainAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/domains/{domain}/webhooks/{eventType}</c> — single webhook for the domain.</summary>
    Task<WebhookResponse> GetDomainAsync(string domain, string eventType, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v4/domains/{domain}/webhooks</c> — register a webhook for the domain.</summary>
    Task<WebhookResponse> CreateDomainAsync(string domain, string eventType, IReadOnlyList<string> urls, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v4/domains/{domain}/webhooks/{eventType}</c> — replace a webhook's URLs.</summary>
    Task<WebhookResponse> UpdateDomainAsync(string domain, string eventType, IReadOnlyList<string> urls, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v4/domains/{domain}/webhooks/{eventType}</c> — delete a webhook.</summary>
    Task DeleteDomainAsync(string domain, string eventType, CancellationToken cancellationToken = default);

    // ----- Modern ID-based account-webhook API (multiple webhooks per event type) -----
    //
    // The historical event-type-keyed methods (ListAccountAsync/GetAccountAsync/CreateAccountAsync
    // /UpdateAccountAsync/DeleteAccountAsync) targeted an obsolete /v1/webhooks shape that Mailgun
    // no longer serves. They were removed in favor of the ID-based methods below.
    //
    // Each webhook has its own id, optional description, a set of subscribed event_types, and one url.
    // To register more than one URL for the same event type, create multiple webhooks (Mailgun still
    // caps total destinations at 3 URLs per event type across all webhooks).

    /// <summary><c>GET /v1/webhooks</c> — list account-level webhooks in the ID-based shape. Optionally filter by ids.</summary>
    Task<AccountWebhookListResponse> ListAccountWebhooksAsync(IReadOnlyList<string>? webhookIds = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v1/webhooks/{id}</c> — fetch a single account-level webhook by id.</summary>
    Task<AccountWebhook> GetAccountWebhookAsync(string id, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v1/webhooks</c> — create a new account-level webhook with description + event types + url.</summary>
    Task<AccountWebhook> CreateAccountWebhookAsync(
        string url,
        IReadOnlyList<string> eventTypes,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v1/webhooks/{id}</c> — replace an account-level webhook. Mailgun's PUT is full-replace
    /// semantics: <paramref name="url"/> and <paramref name="eventTypes"/> are required even when only
    /// one field is conceptually changing. The endpoint returns <c>204 No Content</c> on success;
    /// re-fetch with <see cref="GetAccountWebhookAsync"/> if you need the updated state.
    /// </summary>
    Task UpdateAccountWebhookAsync(
        string id,
        string url,
        IReadOnlyList<string> eventTypes,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v1/webhooks/{id}</c> — delete one account-level webhook by id.</summary>
    Task DeleteAccountWebhookAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v1/webhooks</c> — bulk delete. Supply <paramref name="webhookIds"/> to delete a specific set,
    /// or set <paramref name="all"/> = <c>true</c> to wipe every account-level webhook.
    /// </summary>
    Task DeleteAccountWebhooksAsync(
        IReadOnlyList<string>? webhookIds = null,
        bool all = false,
        CancellationToken cancellationToken = default);
}
