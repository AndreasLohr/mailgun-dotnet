using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Serialization;

namespace Mailgun.Services;

/// <summary>Operations on <c>/v3/ip_warmups</c>.</summary>
public interface IIpWarmupsService
{
    Task<IpWarmupListResponse> ListAsync(CancellationToken cancellationToken = default);
    Task<IpWarmup> GetAsync(string ip, CancellationToken cancellationToken = default);
    Task<IpWarmup> StartAsync(string ip, CancellationToken cancellationToken = default);
    Task StopAsync(string ip, CancellationToken cancellationToken = default);
}

/// <summary>A Mailgun IP warmup record.</summary>
public sealed class IpWarmup
{
    [JsonPropertyName("ip")] public string Ip { get; init; } = string.Empty;
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("started_at")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? StartedAt { get; init; }
    [JsonPropertyName("completed_at")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? CompletedAt { get; init; }
    [JsonPropertyName("stage")] public int? Stage { get; init; }
}

/// <summary>List response.</summary>
public sealed class IpWarmupListResponse
{
    [JsonPropertyName("items")] public List<IpWarmup>? Items { get; init; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; init; }
}

internal sealed class IpWarmupsService : IIpWarmupsService
{
    private readonly MailgunHttpClient _http;
    public IpWarmupsService(MailgunHttpClient http) => _http = http;

    public Task<IpWarmupListResponse> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<IpWarmupListResponse>("v3/ip_warmups", null, cancellationToken);

    public Task<IpWarmup> GetAsync(string ip, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        return _http.GetJsonAsync<IpWarmup>($"v3/ip_warmups/{PathEscape.Segment(ip)}", null, cancellationToken);
    }

    public Task<IpWarmup> StartAsync(string ip, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        return _http.PostFormAsync<IpWarmup>($"v3/ip_warmups/{PathEscape.Segment(ip)}", new FormBuilder(), cancellationToken);
    }

    public Task StopAsync(string ip, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        return _http.DeleteNoResponseAsync($"v3/ip_warmups/{PathEscape.Segment(ip)}", cancellationToken);
    }
}
