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

internal sealed class AlertsService : IAlertsService
{
    private readonly MailgunHttpClient _http;
    public AlertsService(MailgunHttpClient http) => _http = http;

    public Task<AlertSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<AlertSettings>("v1/alerts/settings", null, cancellationToken);

    public Task<AlertSettings> UpdateSettingsAsync(AlertSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _http.PutJsonBodyAsync<AlertSettings>("v1/alerts/settings", settings, cancellationToken);
    }

    public Task<AlertEventList> ListEventsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<AlertEventList>("v1/alerts/events", null, cancellationToken);

    public Task<AlertSlackChannelList> ListSlackChannelsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<AlertSlackChannelList>("v1/alerts/slack/channels", null, cancellationToken);

    public Task<AlertEmailList> ListEmailsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<AlertEmailList>("v1/alerts/email/recipients", null, cancellationToken);

    public Task<AlertWebhookList> ListWebhooksAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<AlertWebhookList>("v1/alerts/webhook/urls", null, cancellationToken);

    public Task AddEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var fb = new FormBuilder().Add("email", email);
        return _http.PostFormNoResponseAsync("v1/alerts/email/recipients", fb, cancellationToken);
    }

    public Task RemoveEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return _http.DeleteNoResponseAsync($"v1/alerts/email/recipients/{PathEscape.Segment(email)}", cancellationToken);
    }

    public Task AddSlackChannelAsync(string webhookUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(webhookUrl);
        var fb = new FormBuilder().Add("url", webhookUrl);
        return _http.PostFormNoResponseAsync("v1/alerts/slack/channels", fb, cancellationToken);
    }

    public Task RemoveSlackChannelAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _http.DeleteNoResponseAsync($"v1/alerts/slack/channels/{PathEscape.Segment(id)}", cancellationToken);
    }

    public Task AddWebhookAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var fb = new FormBuilder().Add("url", url);
        return _http.PostFormNoResponseAsync("v1/alerts/webhook/urls", fb, cancellationToken);
    }

    public Task RemoveWebhookAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _http.DeleteNoResponseAsync($"v1/alerts/webhook/urls/{PathEscape.Segment(id)}", cancellationToken);
    }

    public Task SubscribeEventAsync(string eventType, string channel, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        var fb = new FormBuilder().Add("event", eventType).Add("channel", channel);
        return _http.PostFormNoResponseAsync("v1/alerts/events/subscribe", fb, cancellationToken);
    }

    public Task UnsubscribeEventAsync(string eventType, string channel, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        var fb = new FormBuilder().Add("event", eventType).Add("channel", channel);
        return _http.PostFormNoResponseAsync("v1/alerts/events/unsubscribe", fb, cancellationToken);
    }
}
