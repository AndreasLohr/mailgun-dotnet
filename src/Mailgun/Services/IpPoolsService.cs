using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Serialization;

namespace Mailgun.Services;

/// <summary>Operations on <c>/v3/ip_pools</c>.</summary>
public interface IIpPoolsService
{
    Task<IpPoolListResponse> ListAsync(CancellationToken cancellationToken = default);
    Task<IpPool> GetAsync(string poolId, CancellationToken cancellationToken = default);
    Task<IpPool> CreateAsync(CreateIpPoolRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(string poolId, UpdateIpPoolRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string poolId, string? replacementPool = null, CancellationToken cancellationToken = default);
    /// <summary><c>POST /v3/ip_pools/{poolId}/ips</c> — add IPs to a pool.</summary>
    Task AddIpsAsync(string poolId, IReadOnlyList<string> ips, CancellationToken cancellationToken = default);
    /// <summary><c>DELETE /v3/ip_pools/{poolId}/ips/{ip}</c> — remove an IP from a pool.</summary>
    Task RemoveIpAsync(string poolId, string ip, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v3/ip_pools/{poolId}/ips.json</c> — replace the IP list of a pool in a single
    /// JSON-bodied call. Use when adding/removing many IPs at once to avoid the per-IP fan-out.
    /// </summary>
    Task ReplaceIpsAsync(string poolId, IReadOnlyList<string> ips, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v3/ip_pools/{poolId}/delegate</c> — delegate a pool to one or more subaccounts.
    /// </summary>
    Task DelegateAsync(string poolId, IReadOnlyList<string> subaccountIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v3/ip_pools/{poolId}/delegations</c> — list subaccounts the pool is delegated to.
    /// </summary>
    Task<IpPoolDelegationsResponse> ListDelegationsAsync(string poolId, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v3/ip_pools/{poolId}/delegate/{subaccountId}</c> — revoke a delegation.</summary>
    Task RevokeDelegationAsync(string poolId, string subaccountId, CancellationToken cancellationToken = default);
}

/// <summary>List of subaccounts a pool is delegated to.</summary>
public sealed class IpPoolDelegationsResponse
{
    [JsonPropertyName("subaccounts")] public List<string>? Subaccounts { get; init; }
}

/// <summary>A Mailgun IP pool.</summary>
public sealed class IpPool
{
    [JsonPropertyName("pool_id")] public string PoolId { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("ips")] public List<string>? Ips { get; init; }
    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>List response.</summary>
public sealed class IpPoolListResponse
{
    [JsonPropertyName("ip_pools")] public List<IpPool>? IpPools { get; init; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; init; }
}

/// <summary>Parameters for <c>POST /v3/ip_pools</c>.</summary>
public sealed class CreateIpPoolRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Ips { get; } = new();
}

/// <summary>Parameters for <c>PATCH /v3/ip_pools/{poolId}</c>.</summary>
public sealed class UpdateIpPoolRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<string> Ips { get; } = new();
}

internal sealed class IpPoolsService : IIpPoolsService
{
    private readonly MailgunHttpClient _http;
    public IpPoolsService(MailgunHttpClient http) => _http = http;

    public Task<IpPoolListResponse> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<IpPoolListResponse>("v3/ip_pools", null, cancellationToken);

    public Task<IpPool> GetAsync(string poolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        return _http.GetJsonAsync<IpPool>($"v3/ip_pools/{PathEscape.Segment(poolId)}", null, cancellationToken);
    }

    public Task<IpPool> CreateAsync(CreateIpPoolRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.", nameof(request));
        var fb = new FormBuilder().Add("name", request.Name).Add("description", request.Description);
        if (request.Ips.Count > 0)
            fb.Add("ips", string.Join(",", request.Ips));
        return _http.PostFormAsync<IpPool>("v3/ip_pools", fb, cancellationToken);
    }

    public Task UpdateAsync(string poolId, UpdateIpPoolRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentNullException.ThrowIfNull(request);
        var fb = new FormBuilder().Add("name", request.Name).Add("description", request.Description);
        if (request.Ips.Count > 0)
            fb.Add("ips", string.Join(",", request.Ips));
        return _http.PutFormNoResponseAsync($"v3/ip_pools/{PathEscape.Segment(poolId)}", fb, cancellationToken);
    }

    public Task DeleteAsync(string poolId, string? replacementPool = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        var path = $"v3/ip_pools/{PathEscape.Segment(poolId)}";
        if (!string.IsNullOrEmpty(replacementPool))
            path += "?pool_id=" + Uri.EscapeDataString(replacementPool);
        return _http.DeleteNoResponseAsync(path, cancellationToken);
    }

    public Task AddIpsAsync(string poolId, IReadOnlyList<string> ips, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentNullException.ThrowIfNull(ips);
        var fb = new FormBuilder();
        foreach (var ip in ips)
            fb.Add("ip", ip);
        return _http.PostFormNoResponseAsync($"v3/ip_pools/{PathEscape.Segment(poolId)}/ips", fb, cancellationToken);
    }

    public Task RemoveIpAsync(string poolId, string ip, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        return _http.DeleteNoResponseAsync($"v3/ip_pools/{PathEscape.Segment(poolId)}/ips/{PathEscape.Segment(ip)}", cancellationToken);
    }

    public Task ReplaceIpsAsync(string poolId, IReadOnlyList<string> ips, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentNullException.ThrowIfNull(ips);
        return _http.PostJsonBodyNoResponseAsync(
            $"v3/ip_pools/{PathEscape.Segment(poolId)}/ips.json",
            new { ips },
            cancellationToken);
    }

    public Task DelegateAsync(string poolId, IReadOnlyList<string> subaccountIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentNullException.ThrowIfNull(subaccountIds);
        if (subaccountIds.Count == 0)
            throw new ArgumentException("At least one subaccount id is required.", nameof(subaccountIds));
        return _http.PostJsonBodyNoResponseAsync(
            $"v3/ip_pools/{PathEscape.Segment(poolId)}/delegate",
            new { subaccounts = subaccountIds },
            cancellationToken);
    }

    public Task<IpPoolDelegationsResponse> ListDelegationsAsync(string poolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        return _http.GetJsonAsync<IpPoolDelegationsResponse>(
            $"v3/ip_pools/{PathEscape.Segment(poolId)}/delegations", null, cancellationToken);
    }

    public Task RevokeDelegationAsync(string poolId, string subaccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subaccountId);
        return _http.DeleteNoResponseAsync(
            $"v3/ip_pools/{PathEscape.Segment(poolId)}/delegate/{PathEscape.Segment(subaccountId)}",
            cancellationToken);
    }
}
