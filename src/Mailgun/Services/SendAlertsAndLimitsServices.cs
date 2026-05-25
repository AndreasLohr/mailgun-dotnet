using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;

namespace Mailgun.Services;

/// <summary>Operations on <c>/v1/thresholds/alerts/send</c> (send-alert thresholds).</summary>
public interface ISendAlertsService
{
    /// <summary><c>GET /v1/thresholds/alerts/send/config</c> — get current send-alert configuration.</summary>
    Task<SendAlertConfig> GetConfigAsync(CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v1/thresholds/alerts/send/config</c> — update send-alert configuration.</summary>
    Task<SendAlertConfig> UpdateConfigAsync(SendAlertConfig config, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v1/thresholds/alerts/send/queues</c> — list send-alert queue states.</summary>
    Task<SendAlertQueueList> ListQueuesAsync(CancellationToken cancellationToken = default);

    /// <summary><c>POST /v1/thresholds/alerts/send/queues/pause</c> — pause sending.</summary>
    Task PauseQueueAsync(string? domain = null, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v1/thresholds/alerts/send/queues/resume</c> — resume sending.</summary>
    Task ResumeQueueAsync(string? domain = null, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v1/thresholds/alerts/send/queues/clear</c> — clear queued messages.</summary>
    Task ClearQueueAsync(string? domain = null, CancellationToken cancellationToken = default);
}

/// <summary>Send-alert configuration record.</summary>
public sealed class SendAlertConfig
{
    [JsonPropertyName("bounce_rate")] public double? BounceRate { get; set; }
    [JsonPropertyName("complaint_rate")] public double? ComplaintRate { get; set; }
    [JsonPropertyName("auto_pause")] public bool? AutoPause { get; set; }
    [JsonPropertyName("enabled")] public bool? Enabled { get; set; }
}

/// <summary>Queue state list.</summary>
public sealed class SendAlertQueueList
{
    [JsonPropertyName("items")] public List<Dictionary<string, object>>? Items { get; init; }
}

internal sealed class SendAlertsService : ISendAlertsService
{
    private readonly MailgunHttpClient _http;
    public SendAlertsService(MailgunHttpClient http) => _http = http;

    public Task<SendAlertConfig> GetConfigAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<SendAlertConfig>("v1/thresholds/alerts/send/config", null, cancellationToken);

    public Task<SendAlertConfig> UpdateConfigAsync(SendAlertConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        return _http.PutJsonBodyAsync<SendAlertConfig>("v1/thresholds/alerts/send/config", config, cancellationToken);
    }

    public Task<SendAlertQueueList> ListQueuesAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<SendAlertQueueList>("v1/thresholds/alerts/send/queues", null, cancellationToken);

    public Task PauseQueueAsync(string? domain = null, CancellationToken cancellationToken = default)
    {
        var fb = new FormBuilder().Add("domain", domain);
        return _http.PostFormNoResponseAsync("v1/thresholds/alerts/send/queues/pause", fb, cancellationToken);
    }

    public Task ResumeQueueAsync(string? domain = null, CancellationToken cancellationToken = default)
    {
        var fb = new FormBuilder().Add("domain", domain);
        return _http.PostFormNoResponseAsync("v1/thresholds/alerts/send/queues/resume", fb, cancellationToken);
    }

    public Task ClearQueueAsync(string? domain = null, CancellationToken cancellationToken = default)
    {
        var fb = new FormBuilder().Add("domain", domain);
        return _http.PostFormNoResponseAsync("v1/thresholds/alerts/send/queues/clear", fb, cancellationToken);
    }
}

/// <summary>Operations on <c>/v1/thresholds/limits</c>.</summary>
public interface ILimitsService
{
    /// <summary><c>GET /v1/thresholds/limits</c> — list account limit thresholds.</summary>
    Task<LimitsConfig> GetAsync(CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v1/thresholds/limits</c> — update account limit thresholds.</summary>
    Task<LimitsConfig> UpdateAsync(LimitsConfig limits, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v1/thresholds/limits/enable</c> — start enforcing limit thresholds.</summary>
    Task EnableAsync(CancellationToken cancellationToken = default);

    /// <summary><c>POST /v1/thresholds/limits/disable</c> — stop enforcing limit thresholds.</summary>
    Task DisableAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /v1/thresholds/limits/usage</c> — current usage against the configured limits.</summary>
    Task<LimitsUsage> GetUsageAsync(CancellationToken cancellationToken = default);
}

/// <summary>Current usage against the configured send-limit thresholds.</summary>
public sealed class LimitsUsage
{
    [JsonPropertyName("daily_used")] public long? DailyUsed { get; init; }
    [JsonPropertyName("daily_remaining")] public long? DailyRemaining { get; init; }
    [JsonPropertyName("hourly_used")] public long? HourlyUsed { get; init; }
    [JsonPropertyName("hourly_remaining")] public long? HourlyRemaining { get; init; }
    [JsonPropertyName("monthly_used")] public long? MonthlyUsed { get; init; }
    [JsonPropertyName("monthly_remaining")] public long? MonthlyRemaining { get; init; }
    [JsonPropertyName("enabled")] public bool? Enabled { get; init; }
}

/// <summary>Limit threshold configuration.</summary>
public sealed class LimitsConfig
{
    [JsonPropertyName("daily_send_limit")] public long? DailySendLimit { get; set; }
    [JsonPropertyName("monthly_send_limit")] public long? MonthlySendLimit { get; set; }
    [JsonPropertyName("hourly_send_limit")] public long? HourlySendLimit { get; set; }
    [JsonPropertyName("auto_pause_on_breach")] public bool? AutoPauseOnBreach { get; set; }
}

internal sealed class LimitsService : ILimitsService
{
    private readonly MailgunHttpClient _http;
    public LimitsService(MailgunHttpClient http) => _http = http;

    public Task<LimitsConfig> GetAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<LimitsConfig>("v1/thresholds/limits", null, cancellationToken);

    public Task<LimitsConfig> UpdateAsync(LimitsConfig limits, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(limits);
        return _http.PutJsonBodyAsync<LimitsConfig>("v1/thresholds/limits", limits, cancellationToken);
    }

    public Task EnableAsync(CancellationToken cancellationToken = default) =>
        _http.PostJsonBodyNoResponseAsync("v1/thresholds/limits/enable", new { }, cancellationToken);

    public Task DisableAsync(CancellationToken cancellationToken = default) =>
        _http.PostJsonBodyNoResponseAsync("v1/thresholds/limits/disable", new { }, cancellationToken);

    public Task<LimitsUsage> GetUsageAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<LimitsUsage>("v1/thresholds/limits/usage", null, cancellationToken);
}
