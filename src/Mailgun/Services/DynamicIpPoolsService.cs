using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Pagination;

namespace Mailgun.Services;

/// <summary>
/// Operations on Mailgun's Dynamic IP Pools (DIPP) — a feature that automatically routes a
/// domain's traffic to the appropriate IP pool based on its reputation band (good / new / poor).
/// </summary>
/// <remarks>
/// <para>
/// The surface spans two API versions. <c>/v3/dynamic_pools</c> covers per-pool CRUD plus the
/// global all-pools initialise + delete operations and domain-enrollment endpoints. <c>/v1/dynamic_pools</c>
/// holds the history / preview / override sub-endpoints — Mailgun has not migrated those to v3.
/// </para>
/// <para>
/// The five lowest-numbered methods (<see cref="ListAsync"/>, <see cref="GetAsync"/>, <see cref="CreateAsync"/>,
/// <see cref="UpdateAsync"/>, <see cref="DeleteAsync"/>) previously targeted v1 paths exclusively;
/// <see cref="ListAsync"/> now uses <c>v3/dynamic_pools</c> (the documented OpenAPI shape). The
/// remaining four kept their v1 paths — Mailgun's HTML docs still expose them at v1 and there is
/// no v3 equivalent for individual-pool CRUD in the OpenAPI spec.
/// </para>
/// </remarks>
public interface IDynamicIpPoolsService
{
    // ----- Pool CRUD (mostly v1 for back-compat; List is v3) -----

    /// <summary><c>GET /v3/dynamic_pools</c> — list all dynamic IP pools.</summary>
    Task<DynamicIpPoolListResponse> ListAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /v1/dynamic_pools/{poolId}</c> — fetch one pool by id.</summary>
    Task<DynamicIpPool> GetAsync(string poolId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v1/dynamic_pools</c> — create a new pool.</summary>
    Task<DynamicIpPool> CreateAsync(CreateDynamicIpPoolRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v1/dynamic_pools/{poolId}</c> — replace a pool's full configuration.</summary>
    Task<DynamicIpPool> UpdateAsync(string poolId, UpdateDynamicIpPoolRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v1/dynamic_pools/{poolId}</c> — delete one pool.</summary>
    Task DeleteAsync(string poolId, CancellationToken cancellationToken = default);

    // ----- v3 pool IP / enrollment operations -----

    /// <summary>
    /// <c>PATCH /v3/dynamic_pools/{poolName}</c> — atomically swap one IP for another inside a
    /// named pool. Both <paramref name="addIp"/> and <paramref name="removeIp"/> are required.
    /// </summary>
    Task UpdatePoolIpsAsync(string poolName, string addIp, string removeIp, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v3/dynamic_pools/{poolName}/{ip}</c> — add an IP to a named pool.</summary>
    Task AddIpToPoolAsync(string poolName, string ip, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v3/dynamic_pools/all</c> — initialise/replace the three reputation pools in one
    /// shot. Each argument is a Mailgun pool name to act as the receiver for that reputation band.
    /// </summary>
    Task InitializeAllPoolsAsync(string goodReputation, string poorReputation, string newSenders, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v3/dynamic_pools/all</c> — remove every dynamic IP pool on the account.</summary>
    Task DeleteAllPoolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v3/domains/dynamic_pools/assignable</c> — list domains eligible for DIPP enrollment.
    /// Both filters are optional.
    /// </summary>
    Task<Dictionary<string, object>> ListAssignableDomainsAsync(string? subaccountId = null, string? domain = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v3/domains/all/dynamic_pools/enroll?include_subaccounts=…</c> — enroll every
    /// account (and optionally subaccount) domain into DIPP.
    /// </summary>
    Task EnrollAllDomainsAsync(bool includeSubaccounts, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v3/domains/{name}/dynamic_pools?replacement_ip=…</c> — enroll one domain into
    /// DIPP. <paramref name="replacementIp"/> is the IP Mailgun should park the domain at while
    /// DIPP figures out its band.
    /// </summary>
    Task EnrollDomainAsync(string domain, string replacementIp, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v3/domains/{name}/dynamic_pools?replacement_ip=…&amp;replacement_pool_id=…</c> —
    /// unenroll one domain from DIPP. Both replacement parameters are required.
    /// </summary>
    Task UnenrollDomainAsync(string domain, string replacementIp, string replacementPoolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v3/domains/{name}/pool/{ip}</c> — remove an IP from a domain's resolved DIPP,
    /// optionally swapping in a replacement IP or pool.
    /// </summary>
    Task RemoveIpFromDomainPoolAsync(string domain, string ip, string? replacementIp = null, string? replacementPoolId = null, CancellationToken cancellationToken = default);

    // ----- v1 sub-endpoints (history / preview / override) -----

    /// <summary><c>GET /v1/dynamic_pools/domains</c> — list every domain currently assigned to DIPP.</summary>
    Task<DynamicIpPoolDomainPage> ListAssignedDomainsAsync(
        int? limit = null,
        string? account = null,
        string? pool = null,
        string? sortBy = null,
        string? sortOrder = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v1/dynamic_pools/domains/{name}/history</c> — the latest band-transition record for
    /// the domain (issued band, reason, processed count, etc.).
    /// </summary>
    Task<DynamicIpPoolDomainHistory> GetDomainHistoryAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v1/dynamic_pools/domains/{name}/preview</c> — what DIPP would assign the domain
    /// to right now if it re-evaluated.
    /// </summary>
    Task<Dictionary<string, object>> GetDomainPreviewAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v1/dynamic_pools/history</c> — paged account-wide band-transition history.</summary>
    Task<DynamicIpPoolHistoryPage> GetAccountHistoryAsync(
        int? limit = null,
        bool? includeSubaccounts = null,
        string? domain = null,
        string? before = null,
        string? after = null,
        string? movedTo = null,
        string? movedFrom = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v1/dynamic_pools/domains/{name}/override</c> — pin a domain to a specific pool
    /// (escape hatch for when DIPP's automatic assignment is wrong).
    /// </summary>
    Task OverrideDomainAssignmentAsync(string domain, string poolName, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v1/dynamic_pools/domains/{name}/override</c> — remove a domain's pinned-pool
    /// override and return it to automatic assignment.
    /// </summary>
    Task RemoveDomainOverrideAsync(string domain, CancellationToken cancellationToken = default);
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

/// <summary>
/// Dynamic IP pool list response. The v3 endpoint returns a map (<c>{pool_id: {...}}</c>) while
/// the legacy v1 endpoint returned a list under <c>dynamic_pools</c>; this DTO exposes both shapes
/// so consumers can read whichever the wire delivered.
/// </summary>
public sealed class DynamicIpPoolListResponse
{
    /// <summary>List shape (v1 wire format). May be null when the server returns the v3 map shape.</summary>
    [JsonPropertyName("dynamic_pools")] public List<DynamicIpPool>? DynamicPools { get; init; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; init; }
    /// <summary>
    /// Map shape captured under <c>pools</c> for callers that want the v3 wire response directly.
    /// JSON-extension catches everything not bound above — Mailgun's v3 emits pool-id keys at the
    /// top level so they land here.
    /// </summary>
    [JsonExtensionData] public Dictionary<string, object>? AdditionalProperties { get; init; }
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

/// <summary>One row of the <c>/v1/dynamic_pools/domains</c> listing.</summary>
public sealed class DynamicIpPoolDomain
{
    [JsonPropertyName("domain_id")] public string? DomainId { get; init; }
    [JsonPropertyName("domain_name")] public string? DomainName { get; init; }
    [JsonPropertyName("pool_id")] public string? PoolId { get; init; }
    [JsonPropertyName("account_id")] public string? AccountId { get; init; }
    /// <summary>Bounce-rate value at the last band evaluation (when present).</summary>
    [JsonPropertyName("bounce_rate")] public double? BounceRate { get; init; }
    [JsonPropertyName("complaint_rate")] public double? ComplaintRate { get; init; }
}

/// <summary>Paged response from <c>/v1/dynamic_pools/domains</c>.</summary>
public sealed class DynamicIpPoolDomainPage
{
    [JsonPropertyName("items")] public List<DynamicIpPoolDomain>? Items { get; init; }
    [JsonPropertyName("total_items")] public long? TotalItems { get; init; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; init; }
}

/// <summary>A single band-transition event for a DIPP-assigned domain.</summary>
public sealed class DynamicIpPoolDomainHistory
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("owning_account_id")] public string? OwningAccountId { get; init; }
    [JsonPropertyName("account_id")] public string? AccountId { get; init; }
    [JsonPropertyName("account_name")] public string? AccountName { get; init; }
    [JsonPropertyName("domain_id")] public string? DomainId { get; init; }
    [JsonPropertyName("domain_name")] public string? DomainName { get; init; }
    [JsonPropertyName("new_band")] public string? NewBand { get; init; }
    [JsonPropertyName("prev_band")] public string? PrevBand { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
    [JsonPropertyName("bounce_rate")] public double? BounceRate { get; init; }
    [JsonPropertyName("complaint_rate")] public double? ComplaintRate { get; init; }
    [JsonPropertyName("processed_count")] public long? ProcessedCount { get; init; }
    [JsonPropertyName("initiated_by")] public string? InitiatedBy { get; init; }
    [JsonPropertyName("timestamp")] public DateTimeOffset? Timestamp { get; init; }
}

/// <summary>Paged account-wide DIPP history.</summary>
public sealed class DynamicIpPoolHistoryPage
{
    [JsonPropertyName("items")] public List<DynamicIpPoolDomainHistory>? Items { get; init; }
    [JsonPropertyName("total_items")] public long? TotalItems { get; init; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; init; }
}

internal sealed class DynamicIpPoolsService : IDynamicIpPoolsService
{
    private readonly MailgunHttpClient _http;
    public DynamicIpPoolsService(MailgunHttpClient http) => _http = http;

    // ----- Pool CRUD -----

    public Task<DynamicIpPoolListResponse> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<DynamicIpPoolListResponse>("v3/dynamic_pools", null, cancellationToken,
            routeTemplate: "v3/dynamic_pools");

    public Task<DynamicIpPool> GetAsync(string poolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        return _http.GetJsonAsync<DynamicIpPool>($"v1/dynamic_pools/{PathEscape.Segment(poolId)}", null, cancellationToken,
            routeTemplate: "v1/dynamic_pools/{pool_id}");
    }

    public Task<DynamicIpPool> CreateAsync(CreateDynamicIpPoolRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        return _http.PostJsonBodyAsync<DynamicIpPool>("v1/dynamic_pools", request, cancellationToken,
            routeTemplate: "v1/dynamic_pools");
    }

    public Task<DynamicIpPool> UpdateAsync(string poolId, UpdateDynamicIpPoolRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        ArgumentNullException.ThrowIfNull(request);
        return _http.PutJsonBodyAsync<DynamicIpPool>($"v1/dynamic_pools/{PathEscape.Segment(poolId)}", request, cancellationToken,
            routeTemplate: "v1/dynamic_pools/{pool_id}");
    }

    public Task DeleteAsync(string poolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);
        return _http.DeleteNoResponseAsync($"v1/dynamic_pools/{PathEscape.Segment(poolId)}", cancellationToken,
            routeTemplate: "v1/dynamic_pools/{pool_id}");
    }

    // ----- v3 IP/enrollment operations -----

    public async Task UpdatePoolIpsAsync(string poolName, string addIp, string removeIp, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(addIp);
        ArgumentException.ThrowIfNullOrWhiteSpace(removeIp);
        using var mp = new MultipartBuilder()
            .AddText("add_ip", addIp)
            .AddText("remove_ip", removeIp);
        await _http.PatchMultipartNoResponseAsync(
            $"v3/dynamic_pools/{PathEscape.Segment(poolName)}", mp, cancellationToken,
            routeTemplate: "v3/dynamic_pools/{pool_name}").ConfigureAwait(false);
    }

    public Task AddIpToPoolAsync(string poolName, string ip, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        return _http.PostFormNoResponseAsync(
            $"v3/dynamic_pools/{PathEscape.Segment(poolName)}/{PathEscape.Segment(ip)}",
            new FormBuilder(), cancellationToken,
            routeTemplate: "v3/dynamic_pools/{pool_name}/{ip}");
    }

    public async Task InitializeAllPoolsAsync(string goodReputation, string poorReputation, string newSenders, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goodReputation);
        ArgumentException.ThrowIfNullOrWhiteSpace(poorReputation);
        ArgumentException.ThrowIfNullOrWhiteSpace(newSenders);
        using var mp = new MultipartBuilder()
            .AddText("good_reputation", goodReputation)
            .AddText("poor_reputation", poorReputation)
            .AddText("new_senders", newSenders);
        await _http.PostMultipartNoResponseAsync(
            "v3/dynamic_pools/all", mp, cancellationToken,
            routeTemplate: "v3/dynamic_pools/all").ConfigureAwait(false);
    }

    public Task DeleteAllPoolsAsync(CancellationToken cancellationToken = default) =>
        _http.DeleteNoResponseAsync("v3/dynamic_pools/all", cancellationToken,
            routeTemplate: "v3/dynamic_pools/all");

    public Task<Dictionary<string, object>> ListAssignableDomainsAsync(string? subaccountId = null, string? domain = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder().Add("subaccount_id", subaccountId).Add("domain", domain).Build();
        return _http.GetJsonAsync<Dictionary<string, object>>(
            "v3/domains/dynamic_pools/assignable", q, cancellationToken,
            routeTemplate: "v3/domains/dynamic_pools/assignable");
    }

    public Task EnrollAllDomainsAsync(bool includeSubaccounts, CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder().Add("include_subaccounts", includeSubaccounts).Build();
        return _http.PostFormNoResponseAsync(
            BuildPathWithQuery("v3/domains/all/dynamic_pools/enroll", q),
            new FormBuilder(), cancellationToken,
            routeTemplate: "v3/domains/all/dynamic_pools/enroll");
    }

    public Task EnrollDomainAsync(string domain, string replacementIp, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementIp);
        var q = new QueryBuilder().Add("replacement_ip", replacementIp).Build();
        return _http.PostFormNoResponseAsync(
            BuildPathWithQuery($"v3/domains/{PathEscape.Segment(domain)}/dynamic_pools", q),
            new FormBuilder(), cancellationToken,
            routeTemplate: "v3/domains/{name}/dynamic_pools");
    }

    public Task UnenrollDomainAsync(string domain, string replacementIp, string replacementPoolId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementIp);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementPoolId);
        var q = new QueryBuilder()
            .Add("replacement_ip", replacementIp)
            .Add("replacement_pool_id", replacementPoolId)
            .Build();
        return _http.DeleteNoResponseAsync(
            $"v3/domains/{PathEscape.Segment(domain)}/dynamic_pools", q, cancellationToken,
            routeTemplate: "v3/domains/{name}/dynamic_pools");
    }

    public Task RemoveIpFromDomainPoolAsync(string domain, string ip, string? replacementIp = null, string? replacementPoolId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        // Spec quirks: query parameters share names with path parameters (ip, pool_id) — the
        // query versions describe what to swap in, not which IP/pool to remove. The path
        // identifies the target; the query identifies the replacement.
        var q = new QueryBuilder().Add("ip", replacementIp).Add("pool_id", replacementPoolId).Build();
        return _http.DeleteNoResponseAsync(
            $"v3/domains/{PathEscape.Segment(domain)}/pool/{PathEscape.Segment(ip)}", q, cancellationToken,
            routeTemplate: "v3/domains/{name}/pool/{ip}");
    }

    // ----- v1 sub-endpoints -----

    public Task<DynamicIpPoolDomainPage> ListAssignedDomainsAsync(
        int? limit = null,
        string? account = null,
        string? pool = null,
        string? sortBy = null,
        string? sortOrder = null,
        CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder()
            .Add("limit", limit)
            .Add("account", account)
            .Add("pool", pool)
            .Add("sort_by", sortBy)
            .Add("sort_order", sortOrder)
            .Build();
        return _http.GetJsonAsync<DynamicIpPoolDomainPage>(
            "v1/dynamic_pools/domains", q, cancellationToken,
            routeTemplate: "v1/dynamic_pools/domains");
    }

    public Task<DynamicIpPoolDomainHistory> GetDomainHistoryAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.GetJsonAsync<DynamicIpPoolDomainHistory>(
            $"v1/dynamic_pools/domains/{PathEscape.Segment(domain)}/history", null, cancellationToken,
            routeTemplate: "v1/dynamic_pools/domains/{name}/history");
    }

    public Task<Dictionary<string, object>> GetDomainPreviewAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.GetJsonAsync<Dictionary<string, object>>(
            $"v1/dynamic_pools/domains/{PathEscape.Segment(domain)}/preview", null, cancellationToken,
            routeTemplate: "v1/dynamic_pools/domains/{name}/preview");
    }

    public Task<DynamicIpPoolHistoryPage> GetAccountHistoryAsync(
        int? limit = null,
        bool? includeSubaccounts = null,
        string? domain = null,
        string? before = null,
        string? after = null,
        string? movedTo = null,
        string? movedFrom = null,
        CancellationToken cancellationToken = default)
    {
        // The spec capitalises the `Limit` param (PascalCase) — preserve it on the wire.
        var q = new QueryBuilder()
            .Add("Limit", limit)
            .Add("include_subaccounts", includeSubaccounts)
            .Add("domain", domain)
            .Add("before", before)
            .Add("after", after)
            .Add("moved_to", movedTo)
            .Add("moved_from", movedFrom)
            .Build();
        return _http.GetJsonAsync<DynamicIpPoolHistoryPage>(
            "v1/dynamic_pools/history", q, cancellationToken,
            routeTemplate: "v1/dynamic_pools/history");
    }

    public async Task OverrideDomainAssignmentAsync(string domain, string poolName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        using var mp = new MultipartBuilder().AddText("pool", poolName);
        await _http.PutMultipartNoResponseAsync(
            $"v1/dynamic_pools/domains/{PathEscape.Segment(domain)}/override", mp, cancellationToken,
            routeTemplate: "v1/dynamic_pools/domains/{name}/override").ConfigureAwait(false);
    }

    public Task RemoveDomainOverrideAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.DeleteNoResponseAsync(
            $"v1/dynamic_pools/domains/{PathEscape.Segment(domain)}/override", cancellationToken,
            routeTemplate: "v1/dynamic_pools/domains/{name}/override");
    }

    private static string BuildPathWithQuery(string path, IReadOnlyList<KeyValuePair<string, string?>> query)
    {
        if (query.Count == 0) return path;
        var sb = new System.Text.StringBuilder(path).Append('?');
        var first = true;
        foreach (var kv in query)
        {
            if (kv.Value is null) continue;
            if (!first) sb.Append('&');
            sb.Append(Uri.EscapeDataString(kv.Key)).Append('=').Append(Uri.EscapeDataString(kv.Value));
            first = false;
        }
        return sb.ToString();
    }
}
