using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;

namespace Mailgun.Services;

/// <summary>Operations on <c>/v1/dynamic_pools</c> (modern dynamic IP pool management).</summary>
public interface IDynamicIpPoolsService
{
    Task<DynamicIpPoolListResponse> ListAsync(CancellationToken cancellationToken = default);
    Task<DynamicIpPool> GetAsync(string poolId, CancellationToken cancellationToken = default);
    Task<DynamicIpPool> CreateAsync(CreateDynamicIpPoolRequest request, CancellationToken cancellationToken = default);
    Task<DynamicIpPool> UpdateAsync(string poolId, UpdateDynamicIpPoolRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string poolId, CancellationToken cancellationToken = default);
}

/// <summary>A dynamic IP pool configuration.</summary>
public sealed class DynamicIpPool
{
    [JsonPropertyName("pool_id")] public string PoolId { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("send_strategy")] public string? SendStrategy { get; init; }
    [JsonPropertyName("ips")] public List<string>? Ips { get; init; }
    [JsonPropertyName("backup_ip_pool_id")] public string? BackupIpPoolId { get; init; }
}

/// <summary>Dynamic IP pool list response.</summary>
public sealed class DynamicIpPoolListResponse
{
    [JsonPropertyName("dynamic_pools")] public List<DynamicIpPool>? DynamicPools { get; init; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; init; }
}

/// <summary>Parameters for creating a dynamic IP pool.</summary>
public sealed class CreateDynamicIpPoolRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("send_strategy")] public string? SendStrategy { get; set; }
    [JsonPropertyName("ips")] public List<string>? Ips { get; set; }
    [JsonPropertyName("backup_ip_pool_id")] public string? BackupIpPoolId { get; set; }
}

/// <summary>Parameters for updating a dynamic IP pool.</summary>
public sealed class UpdateDynamicIpPoolRequest
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("send_strategy")] public string? SendStrategy { get; set; }
    [JsonPropertyName("ips")] public List<string>? Ips { get; set; }
    [JsonPropertyName("backup_ip_pool_id")] public string? BackupIpPoolId { get; set; }
}

internal sealed class DynamicIpPoolsService : IDynamicIpPoolsService
{
    private readonly MailgunHttpClient _http;
    public DynamicIpPoolsService(MailgunHttpClient http) => _http = http;

    public Task<DynamicIpPoolListResponse> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<DynamicIpPoolListResponse>("v1/dynamic_pools", null, cancellationToken);

    public Task<DynamicIpPool> GetAsync(string poolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        return _http.GetJsonAsync<DynamicIpPool>($"v1/dynamic_pools/{PathEscape.Segment(poolId)}", null, cancellationToken);
    }

    public Task<DynamicIpPool> CreateAsync(CreateDynamicIpPoolRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        return _http.PostJsonBodyAsync<DynamicIpPool>("v1/dynamic_pools", request, cancellationToken);
    }

    public Task<DynamicIpPool> UpdateAsync(string poolId, UpdateDynamicIpPoolRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentNullException.ThrowIfNull(request);
        return _http.PutJsonBodyAsync<DynamicIpPool>($"v1/dynamic_pools/{PathEscape.Segment(poolId)}", request, cancellationToken);
    }

    public Task DeleteAsync(string poolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        return _http.DeleteNoResponseAsync($"v1/dynamic_pools/{PathEscape.Segment(poolId)}", cancellationToken);
    }
}
