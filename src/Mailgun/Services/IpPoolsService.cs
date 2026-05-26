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

    /// <summary><c>PUT /v3/ip_pools/{poolId}/ips/{ip}</c> — add a single IP to a pool.</summary>
    Task AddIpAsync(string poolId, string ip, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v3/ip_pools/{poolId}/ips/{ip}</c> — remove an IP from a pool.</summary>
    Task RemoveIpAsync(string poolId, string ip, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v3/ip_pools/{poolId}/ips.json</c> — add multiple IPs to a pool in one JSON-bodied call.
    /// </summary>
    /// <remarks>
    /// Mailgun's documented operation for this endpoint is "Add multiple IPs", NOT "replace the pool's
    /// IP list". A previous SDK release exposed this as <c>ReplaceIpsAsync</c>, which was a dangerous
    /// misnomer because callers may have assumed they were setting full desired state. There is no
    /// atomic-replace endpoint; build desired state by combining <see cref="AddIpAsync"/> /
    /// <see cref="RemoveIpAsync"/> on the diff against <see cref="GetAsync"/>.
    /// </remarks>
    Task AddIpsAsync(string poolId, IReadOnlyList<string> ips, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v3/ip_pools/{poolId}/delegate</c> — delegate the pool to one subaccount. The subaccount id
    /// is sent as a multipart <c>subaccount_id</c> form field per Mailgun's documented contract.
    /// Call once per subaccount to delegate to more than one.
    /// </summary>
    Task DelegateAsync(string poolId, string subaccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v3/ip_pools/{poolId}/delegations</c> — list subaccounts the pool is delegated to.
    /// </summary>
    Task<IpPoolDelegationsResponse> ListDelegationsAsync(string poolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v3/ip_pools/{poolId}/delegate</c> — revoke a delegation. The subaccount id is sent
    /// as a multipart <c>subaccount_id</c> form field in the request body, NOT a path segment.
    /// </summary>
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

    /// <summary>IPs to seed the pool with. Mailgun's wire format is repeated <c>ip</c> form fields (one per IP).</summary>
    public List<string> Ips { get; } = new();
}

/// <summary>
/// Parameters for <c>PATCH /v3/ip_pools/{poolId}</c>. Mailgun's edit endpoint is differential —
/// you specify what to add/remove, not the full replacement list.
/// </summary>
public sealed class UpdateIpPoolRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }

    /// <summary>Optional linked sending domain — Mailgun's <c>link_domain</c> field.</summary>
    public string? LinkDomain { get; set; }

    /// <summary>IPs to add to the pool. Each entry becomes a repeated <c>add_ip</c> form field.</summary>
    public List<string> AddIps { get; } = new();

    /// <summary>IPs to remove from the pool. Each entry becomes a repeated <c>remove_ip</c> form field.</summary>
    public List<string> RemoveIps { get; } = new();

    /// <summary>Sending domains to unlink. Each entry becomes a repeated <c>unlink_domain</c> form field.</summary>
    public List<string> UnlinkDomains { get; } = new();
}

internal sealed class IpPoolsService : IIpPoolsService
{
    private readonly MailgunHttpClient _http;
    public IpPoolsService(MailgunHttpClient http) => _http = http;

    public Task<IpPoolListResponse> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<IpPoolListResponse>("v3/ip_pools", null, cancellationToken, routeTemplate: "v3/ip_pools");

    public Task<IpPool> GetAsync(string poolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        return _http.GetJsonAsync<IpPool>($"v3/ip_pools/{PathEscape.Segment(poolId)}", null, cancellationToken, routeTemplate: "v3/ip_pools/{pool_id}");
    }

    public Task<IpPool> CreateAsync(CreateIpPoolRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.", nameof(request));
        // Mailgun documents both `name` AND `description` as required for POST /v3/ip_pools.
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ArgumentException("Description is required.", nameof(request));
        // POST /v3/ip_pools takes repeated singular `ip` form fields, not a joined `ips`.
        var fb = new FormBuilder().Add("name", request.Name).Add("description", request.Description);
        foreach (var ip in request.Ips)
            fb.Add("ip", ip);
        return _http.PostFormAsync<IpPool>("v3/ip_pools", fb, cancellationToken, routeTemplate: "v3/ip_pools");
    }

    public async Task UpdateAsync(string poolId, UpdateIpPoolRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentNullException.ThrowIfNull(request);
        // Mailgun's edit endpoint is documented as PATCH /v3/ip_pools/{pool_id} with multipart/form-data
        // and differential repeatable fields (add_ip, remove_ip, unlink_domain) — NOT PUT with a joined ips=.
        using var mp = new MultipartBuilder()
            .AddText("name", request.Name)
            .AddText("description", request.Description)
            .AddText("link_domain", request.LinkDomain);
        foreach (var ip in request.AddIps) mp.AddText("add_ip", ip);
        foreach (var ip in request.RemoveIps) mp.AddText("remove_ip", ip);
        foreach (var d in request.UnlinkDomains) mp.AddText("unlink_domain", d);
        await _http.PatchMultipartNoResponseAsync(
            $"v3/ip_pools/{PathEscape.Segment(poolId)}", mp, cancellationToken,
            routeTemplate: "v3/ip_pools/{pool_id}").ConfigureAwait(false);
    }

    public Task DeleteAsync(string poolId, string? replacementPool = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        // Route the optional replacement-pool param through the standard query channel so BuildUri
        // owns escaping (the manual `?pool_id=…` splice that lived here previously is the same
        // brittle pattern the unsubscribes endpoint just got cleaned up out of).
        var query = new QueryBuilder().Add("pool_id", replacementPool).Build();
        return _http.DeleteNoResponseAsync(
            $"v3/ip_pools/{PathEscape.Segment(poolId)}", query, cancellationToken,
            routeTemplate: "v3/ip_pools/{pool_id}");
    }

    public Task AddIpAsync(string poolId, string ip, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        // Mailgun's documented "add single IP" endpoint is PUT (no body), not POST with form.
        return _http.PutFormNoResponseAsync(
            $"v3/ip_pools/{PathEscape.Segment(poolId)}/ips/{PathEscape.Segment(ip)}",
            new FormBuilder(), cancellationToken,
            routeTemplate: "v3/ip_pools/{pool_id}/ips/{ip}");
    }

    public Task RemoveIpAsync(string poolId, string ip, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        return _http.DeleteNoResponseAsync($"v3/ip_pools/{PathEscape.Segment(poolId)}/ips/{PathEscape.Segment(ip)}", cancellationToken, routeTemplate: "v3/ip_pools/{pool_id}/ips/{ip}");
    }

    public Task AddIpsAsync(string poolId, IReadOnlyList<string> ips, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentNullException.ThrowIfNull(ips);
        if (ips.Count == 0)
            throw new ArgumentException("At least one IP is required.", nameof(ips));
        // POST /v3/ip_pools/{poolId}/ips.json with JSON body { ips: [...] } — Mailgun's
        // "Add multiple IPs" operation. Note: this APPENDS to the pool; it does not replace.
        return _http.PostJsonBodyNoResponseAsync(
            $"v3/ip_pools/{PathEscape.Segment(poolId)}/ips.json",
            new { ips },
            cancellationToken,
            routeTemplate: "v3/ip_pools/{pool_id}/ips.json");
    }

    public async Task DelegateAsync(string poolId, string subaccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subaccountId);
        // Mailgun documents: PUT (not POST), multipart/form-data (not JSON), with a singular `subaccount_id`
        // field per call. Previous SDK shape sent POST + JSON {subaccounts: [...]} and was rejected.
        using var mp = new MultipartBuilder().AddText("subaccount_id", subaccountId);
        await _http.PutMultipartNoResponseAsync(
            $"v3/ip_pools/{PathEscape.Segment(poolId)}/delegate", mp, cancellationToken,
            routeTemplate: "v3/ip_pools/{pool_id}/delegate").ConfigureAwait(false);
    }

    public Task<IpPoolDelegationsResponse> ListDelegationsAsync(string poolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        return _http.GetJsonAsync<IpPoolDelegationsResponse>(
            $"v3/ip_pools/{PathEscape.Segment(poolId)}/delegations", null, cancellationToken,
            routeTemplate: "v3/ip_pools/{pool_id}/delegations");
    }

    public async Task RevokeDelegationAsync(string poolId, string subaccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subaccountId);
        // Subaccount id goes in the multipart body, not the URL path.
        using var mp = new MultipartBuilder().AddText("subaccount_id", subaccountId);
        await _http.DeleteMultipartNoResponseAsync(
            $"v3/ip_pools/{PathEscape.Segment(poolId)}/delegate", mp, cancellationToken,
            routeTemplate: "v3/ip_pools/{pool_id}/delegate").ConfigureAwait(false);
    }
}
