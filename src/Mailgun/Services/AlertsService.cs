using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;

namespace Mailgun.Services;

/// <summary>Operations on <c>/v1/alerts</c> (events/slack/email/webhook channels).</summary>
public interface IAlertsService
{
    Task<AlertSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<AlertSettings> UpdateSettingsAsync(AlertSettings settings, CancellationToken cancellationToken = default);
    Task<AlertEventList> ListEventsAsync(CancellationToken cancellationToken = default);
    Task<AlertSlackChannelList> ListSlackChannelsAsync(CancellationToken cancellationToken = default);
    Task<AlertEmailList> ListEmailsAsync(CancellationToken cancellationToken = default);
    Task<AlertWebhookList> ListWebhooksAsync(CancellationToken cancellationToken = default);
    Task AddEmailAsync(string email, CancellationToken cancellationToken = default);
    Task RemoveEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddSlackChannelAsync(string webhookUrl, CancellationToken cancellationToken = default);
    Task RemoveSlackChannelAsync(string id, CancellationToken cancellationToken = default);
    Task AddWebhookAsync(string url, CancellationToken cancellationToken = default);
    Task RemoveWebhookAsync(string id, CancellationToken cancellationToken = default);
    Task SubscribeEventAsync(string eventType, string channel, CancellationToken cancellationToken = default);
    Task UnsubscribeEventAsync(string eventType, string channel, CancellationToken cancellationToken = default);

    // ----- Modern /v1/alerts/settings/* surface -----
    //
    // The modern alerts API treats each "alert" as a first-class resource keyed by id, with a
    // typed event_type, channel (email/slack/webhook), and channel-specific settings. The legacy
    // recipient/url/subscribe methods above remain functional but the settings/events surface is
    // what Mailgun documents as the recommended path going forward.

    /// <summary>
    /// <c>POST /v1/alerts/settings/events</c> — register a new alert binding (event_type +
    /// channel + channel-specific settings). Returns the created alert's id and the persisted
    /// settings echo.
    /// </summary>
    Task<AlertSettingsEvent> AddSettingsAlertAsync(AlertSettingsEventRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v1/alerts/settings/events/{id}</c> — replace an existing alert binding.
    /// </summary>
    Task UpdateSettingsAlertAsync(string id, AlertSettingsEventRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v1/alerts/settings/events/{id}</c> — remove an alert binding.
    /// </summary>
    Task RemoveSettingsAlertAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v1/alerts/settings/slack</c> — update the account-wide Slack OAuth credentials.
    /// All four fields are required by Mailgun's schema.
    /// </summary>
    Task UpdateSlackSettingsAsync(SlackSettingsRequest settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v1/alerts/settings/slack</c> — clear the account's Slack OAuth credentials
    /// without revoking the token on Slack's side (see <see cref="RevokeSlackOAuthAsync"/>).
    /// </summary>
    Task DeleteSlackSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v1/alerts/settings/webhooks/signing_key</c> — rotate the webhook signing key
    /// Mailgun uses to sign outbound alert webhooks. Returns the new key.
    /// </summary>
    Task<AlertSigningKey> ResetWebhookSigningKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v1/alerts/slack/channels/{id}</c> — fetch metadata for a single configured
    /// Slack channel by id.
    /// </summary>
    Task<AlertSlackChannel> GetSlackChannelAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v1/alerts/slack/oauth</c> — revoke the Slack access token on Slack's side.
    /// Distinct from <see cref="DeleteSlackSettingsAsync"/>, which only forgets the credentials
    /// locally.
    /// </summary>
    Task RevokeSlackOAuthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v1/alerts/email/test</c> — send a test message for <paramref name="eventType"/>
    /// to the supplied <paramref name="emails"/>.
    /// </summary>
    Task SendEmailTestAsync(string eventType, IReadOnlyList<string> emails, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v1/alerts/slack/test</c> — send a test message for <paramref name="eventType"/>
    /// to the supplied Slack <paramref name="channelIds"/> (or all configured channels if null).
    /// </summary>
    Task SendSlackTestAsync(string eventType, IReadOnlyList<string>? channelIds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v1/alerts/webhooks/test</c> — fire a test webhook for <paramref name="eventType"/>
    /// against <paramref name="url"/>.
    /// </summary>
    Task SendWebhookTestAsync(string eventType, string url, CancellationToken cancellationToken = default);
}

/// <summary>Alert subscription settings.</summary>
public sealed class AlertSettings
{
    [JsonPropertyName("events")] public List<Dictionary<string, object>>? Events { get; set; }
}

/// <summary>Available alert event types.</summary>
public sealed class AlertEventList
{
    [JsonPropertyName("items")] public List<Dictionary<string, object>>? Items { get; init; }
}

/// <summary>Configured slack channels.</summary>
public sealed class AlertSlackChannelList
{
    [JsonPropertyName("items")] public List<Dictionary<string, object>>? Items { get; init; }
}

/// <summary>Configured email recipients.</summary>
public sealed class AlertEmailList
{
    [JsonPropertyName("items")] public List<Dictionary<string, object>>? Items { get; init; }
}

/// <summary>Configured webhook URLs.</summary>
public sealed class AlertWebhookList
{
    [JsonPropertyName("items")] public List<Dictionary<string, object>>? Items { get; init; }
}

/// <summary>
/// Request body for creating or updating an entry in <c>/v1/alerts/settings/events</c>.
/// </summary>
public sealed class AlertSettingsEventRequest
{
    /// <summary>Required. Event-type identifier (e.g. <c>send_failure</c>, <c>delivery_failure</c>).</summary>
    [JsonPropertyName("event_type")] public string EventType { get; set; } = string.Empty;
    /// <summary>Required. Channel name — typically <c>email</c>, <c>slack</c>, or <c>webhook</c>.</summary>
    [JsonPropertyName("channel")] public string Channel { get; set; } = string.Empty;
    /// <summary>Required. Channel-specific settings (recipients, urls, thresholds, etc.).</summary>
    [JsonPropertyName("settings")] public Dictionary<string, object> Settings { get; set; } = new();
}

/// <summary>Response shape for the modern alert events endpoint.</summary>
public sealed class AlertSettingsEvent
{
    /// <summary>Server-assigned alert id. Required to PUT / DELETE the entry later.</summary>
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("event_type")] public string? EventType { get; init; }
    [JsonPropertyName("channel")] public string? Channel { get; init; }
    [JsonPropertyName("settings")] public Dictionary<string, object>? Settings { get; init; }
    /// <summary>When the alert was disabled, if applicable.</summary>
    [JsonPropertyName("disabled_at")] public DateTimeOffset? DisabledAt { get; init; }
}

/// <summary>
/// PUT body for <c>/v1/alerts/settings/slack</c>. Captures the OAuth credentials Mailgun needs
/// to post into the workspace. All four fields are required by the wire schema.
/// </summary>
public sealed class SlackSettingsRequest
{
    [JsonPropertyName("token")] public string Token { get; set; } = string.Empty;
    [JsonPropertyName("team_id")] public string TeamId { get; set; } = string.Empty;
    [JsonPropertyName("team_name")] public string TeamName { get; set; } = string.Empty;
    [JsonPropertyName("scope")] public string Scope { get; set; } = string.Empty;
}

/// <summary>Response from the reset-signing-key endpoint.</summary>
public sealed class AlertSigningKey
{
    [JsonPropertyName("signing_key")] public string SigningKey { get; init; } = string.Empty;
}

/// <summary>A Slack channel as exposed by <c>GET /v1/alerts/slack/channels/{id}</c>.</summary>
public sealed class AlertSlackChannel
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("is_archived")] public bool IsArchived { get; init; }
}

internal sealed class AlertsService : IAlertsService
{
    private readonly MailgunHttpClient _http;
    public AlertsService(MailgunHttpClient http) => _http = http;

    public Task<AlertSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<AlertSettings>("v1/alerts/settings", null, cancellationToken, routeTemplate: "v1/alerts/settings");

    public Task<AlertSettings> UpdateSettingsAsync(AlertSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _http.PutJsonBodyAsync<AlertSettings>("v1/alerts/settings", settings, cancellationToken, routeTemplate: "v1/alerts/settings");
    }

    public Task<AlertEventList> ListEventsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<AlertEventList>("v1/alerts/events", null, cancellationToken, routeTemplate: "v1/alerts/events");

    public Task<AlertSlackChannelList> ListSlackChannelsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<AlertSlackChannelList>("v1/alerts/slack/channels", null, cancellationToken, routeTemplate: "v1/alerts/slack/channels");

    public Task<AlertEmailList> ListEmailsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<AlertEmailList>("v1/alerts/email/recipients", null, cancellationToken, routeTemplate: "v1/alerts/email/recipients");

    public Task<AlertWebhookList> ListWebhooksAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<AlertWebhookList>("v1/alerts/webhook/urls", null, cancellationToken, routeTemplate: "v1/alerts/webhook/urls");

    public Task AddEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var fb = new FormBuilder().Add("email", email);
        return _http.PostFormNoResponseAsync("v1/alerts/email/recipients", fb, cancellationToken, routeTemplate: "v1/alerts/email/recipients");
    }

    public Task RemoveEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return _http.DeleteNoResponseAsync($"v1/alerts/email/recipients/{PathEscape.Segment(email)}", cancellationToken, routeTemplate: "v1/alerts/email/recipients/{email}");
    }

    public Task AddSlackChannelAsync(string webhookUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(webhookUrl);
        var fb = new FormBuilder().Add("url", webhookUrl);
        return _http.PostFormNoResponseAsync("v1/alerts/slack/channels", fb, cancellationToken, routeTemplate: "v1/alerts/slack/channels");
    }

    public Task RemoveSlackChannelAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _http.DeleteNoResponseAsync($"v1/alerts/slack/channels/{PathEscape.Segment(id)}", cancellationToken, routeTemplate: "v1/alerts/slack/channels/{id}");
    }

    public Task AddWebhookAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var fb = new FormBuilder().Add("url", url);
        return _http.PostFormNoResponseAsync("v1/alerts/webhook/urls", fb, cancellationToken, routeTemplate: "v1/alerts/webhook/urls");
    }

    public Task RemoveWebhookAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _http.DeleteNoResponseAsync($"v1/alerts/webhook/urls/{PathEscape.Segment(id)}", cancellationToken, routeTemplate: "v1/alerts/webhook/urls/{id}");
    }

    public Task SubscribeEventAsync(string eventType, string channel, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        var fb = new FormBuilder().Add("event", eventType).Add("channel", channel);
        return _http.PostFormNoResponseAsync("v1/alerts/events/subscribe", fb, cancellationToken, routeTemplate: "v1/alerts/events/subscribe");
    }

    public Task UnsubscribeEventAsync(string eventType, string channel, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        var fb = new FormBuilder().Add("event", eventType).Add("channel", channel);
        return _http.PostFormNoResponseAsync("v1/alerts/events/unsubscribe", fb, cancellationToken, routeTemplate: "v1/alerts/events/unsubscribe");
    }

    // ---------- Modern /v1/alerts/settings/* surface ----------

    public Task<AlertSettingsEvent> AddSettingsAlertAsync(AlertSettingsEventRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Channel);
        ArgumentNullException.ThrowIfNull(request.Settings);
        return _http.PostJsonBodyAsync<AlertSettingsEvent>(
            "v1/alerts/settings/events", request, cancellationToken,
            routeTemplate: "v1/alerts/settings/events");
    }

    public Task UpdateSettingsAlertAsync(string id, AlertSettingsEventRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Channel);
        ArgumentNullException.ThrowIfNull(request.Settings);
        return _http.PutJsonBodyNoResponseAsync(
            $"v1/alerts/settings/events/{PathEscape.Segment(id)}", request, cancellationToken,
            routeTemplate: "v1/alerts/settings/events/{id}");
    }

    public Task RemoveSettingsAlertAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _http.DeleteNoResponseAsync(
            $"v1/alerts/settings/events/{PathEscape.Segment(id)}", cancellationToken,
            routeTemplate: "v1/alerts/settings/events/{id}");
    }

    public Task UpdateSlackSettingsAsync(SlackSettingsRequest settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Token, nameof(settings));
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.TeamId, nameof(settings));
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.TeamName, nameof(settings));
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Scope, nameof(settings));
        return _http.PutJsonBodyNoResponseAsync(
            "v1/alerts/settings/slack", settings, cancellationToken,
            routeTemplate: "v1/alerts/settings/slack");
    }

    public Task DeleteSlackSettingsAsync(CancellationToken cancellationToken = default) =>
        _http.DeleteNoResponseAsync("v1/alerts/settings/slack", cancellationToken,
            routeTemplate: "v1/alerts/settings/slack");

    public Task<AlertSigningKey> ResetWebhookSigningKeyAsync(CancellationToken cancellationToken = default) =>
        _http.PutJsonBodyAsync<AlertSigningKey>(
            "v1/alerts/settings/webhooks/signing_key", new { }, cancellationToken,
            routeTemplate: "v1/alerts/settings/webhooks/signing_key");

    public Task<AlertSlackChannel> GetSlackChannelAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _http.GetJsonAsync<AlertSlackChannel>(
            $"v1/alerts/slack/channels/{PathEscape.Segment(id)}", null, cancellationToken,
            routeTemplate: "v1/alerts/slack/channels/{id}");
    }

    public Task RevokeSlackOAuthAsync(CancellationToken cancellationToken = default) =>
        _http.DeleteNoResponseAsync("v1/alerts/slack/oauth", cancellationToken,
            routeTemplate: "v1/alerts/slack/oauth");

    public Task SendEmailTestAsync(string eventType, IReadOnlyList<string> emails, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(emails);
        if (emails.Count == 0)
            throw new ArgumentException("At least one recipient is required.", nameof(emails));
        var body = new { event_type = eventType, emails };
        return _http.PostJsonBodyNoResponseAsync(
            "v1/alerts/email/test", body, cancellationToken,
            routeTemplate: "v1/alerts/email/test");
    }

    public Task SendSlackTestAsync(string eventType, IReadOnlyList<string>? channelIds = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        var body = new { event_type = eventType, channel_ids = channelIds };
        return _http.PostJsonBodyNoResponseAsync(
            "v1/alerts/slack/test", body, cancellationToken,
            routeTemplate: "v1/alerts/slack/test");
    }

    public Task SendWebhookTestAsync(string eventType, string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var body = new { event_type = eventType, url };
        return _http.PostJsonBodyNoResponseAsync(
            "v1/alerts/webhooks/test", body, cancellationToken,
            routeTemplate: "v1/alerts/webhooks/test");
    }
}
