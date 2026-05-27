using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Serialization;

namespace Mailgun.Services;

/// <summary>Operations on <c>/v3/ips</c>.</summary>
public interface IIpsService
{
    /// <summary><c>GET /v3/ips</c> — list IPs assigned to the account.</summary>
    Task<IpsListResponse> ListAsync(bool? dedicated = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v3/ips/{ip}</c> — info for a specific IP.</summary>
    Task<IpInfo> GetAsync(string ip, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v3/ips/{ip}/domains</c> — list domains assigned to this IP.</summary>
    Task<IpDomainsResponse> ListDomainsAsync(string ip, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v3/domains/{domain}/ips</c> — list IPs assigned to a domain.</summary>
    Task<IpsListResponse> ListByDomainAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v3/domains/{domain}/ips</c> — attach an IP to a domain.</summary>
    Task AttachToDomainAsync(string domain, string ip, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v3/domains/{domain}/ips/{ip}</c> — detach an IP from a domain.</summary>
    Task DetachFromDomainAsync(string domain, string ip, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v3/ips/request/new</c> — request a new dedicated IP.</summary>
    Task RequestNewAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /v3/ips/{ip}/ip_band</c> — current reputation band for an IP.</summary>
    Task<IpReputationBand> GetReputationBandAsync(string ip, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v3/ips/details</c> — full details for every account-assigned IP, including warm-up + reputation.</summary>
    Task<IpsListResponse> ListDetailedAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /v3/ips/all</c> — all IPs available to the account (dedicated + shared pools).</summary>
    Task<IpsListResponse> ListAllAccountIpsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v3/ips/{ip}/domains</c> — detach the IP from every account domain in one call.
    /// <paramref name="alternativeIp"/> is required (Mailgun routes affected domains to it during
    /// the swap).
    /// </summary>
    Task<IpsBulkOperationResponse> DetachIpFromAllDomainsAsync(string ip, string alternativeIp, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v3/ips/details/all</c> — page through every account-assigned IP with full details
    /// (dedicated + warm-up + reputation). All filter parameters are optional.
    /// </summary>
    Task<IpsDetailedListResponse> ListAllDetailedAsync(
        int? limit = null,
        int? skip = null,
        string? poolId = null,
        string? domainId = null,
        string? subaccountId = null,
        string? ip = null,
        string? sortBy = null,
        string? sortOrder = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Response wrapper for IP bulk operations like detach-from-all-domains.</summary>
public sealed class IpsBulkOperationResponse
{
    [JsonPropertyName("message")] public string? Message { get; init; }
    /// <summary>Operation reference id Mailgun emits for tracking async-completed work.</summary>
    [JsonPropertyName("reference_id")] public string? ReferenceId { get; init; }
}

/// <summary>Paged response from <c>GET /v3/ips/details/all</c>.</summary>
public sealed class IpsDetailedListResponse
{
    [JsonPropertyName("items")] public List<IpInfo>? Items { get; init; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; init; }
}

/// <summary>Mailgun-assigned reputation band for an IP.</summary>
public sealed class IpReputationBand
{
    [JsonPropertyName("ip")] public string? Ip { get; init; }
    [JsonPropertyName("band")] public string? Band { get; init; }
    [JsonPropertyName("score")] public double? Score { get; init; }
    [JsonPropertyName("last_updated_at")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? LastUpdatedAt { get; init; }
}

/// <summary>A Mailgun IP record.</summary>
public sealed class IpInfo
{
    [JsonPropertyName("ip")] public string Ip { get; init; } = string.Empty;
    [JsonPropertyName("dedicated")] public bool? Dedicated { get; init; }
    [JsonPropertyName("rdns")] public string? Rdns { get; init; }
    [JsonPropertyName("warmup_state")] public string? WarmupState { get; init; }
    [JsonPropertyName("warmup_started_at")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? WarmupStartedAt { get; init; }
}

/// <summary>List response from <c>GET /v3/ips</c>.</summary>
/// <remarks>
/// Mailgun's response shape: <c>{"items":["159.x.x.x", …], "details":[{…IpInfo…}, …], "total_count":N}</c>.
/// <see cref="Items"/> is the simple string list, <see cref="Details"/> is the rich object list, and
/// <see cref="Ips"/> is kept as an alias for endpoints that return <c>ips</c> instead of <c>items</c>.
/// </remarks>
public sealed class IpsListResponse
{
    /// <summary>Plain IP addresses as Mailgun returns them under <c>items</c>.</summary>
    [JsonPropertyName("items")] public List<string>? Items { get; init; }

    /// <summary>Rich IP objects (dedicated / enabled / warmup state) returned under <c>details</c>.</summary>
    [JsonPropertyName("details")] public List<IpInfo>? Details { get; init; }

    [JsonPropertyName("total_count")] public long? TotalCount { get; init; }

    /// <summary>Alias for <see cref="Items"/>; some endpoints return <c>ips</c> instead of <c>items</c>.</summary>
    [JsonPropertyName("ips")] public List<string>? Ips { get; init; }
}

/// <summary>Response from <c>GET /v3/ips/{ip}/domains</c>.</summary>
public sealed class IpDomainsResponse
{
    [JsonPropertyName("items")] public List<string>? Items { get; init; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; init; }
}

internal sealed class IpsService : IIpsService
{
    private readonly MailgunHttpClient _http;
    public IpsService(MailgunHttpClient http) => _http = http;

    public Task<IpsListResponse> ListAsync(bool? dedicated = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder().Add("dedicated", dedicated).Build();
        return _http.GetJsonAsync<IpsListResponse>("v3/ips", q, cancellationToken,
            routeTemplate: "v3/ips");
    }

    public Task<IpInfo> GetAsync(string ip, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        return _http.GetJsonAsync<IpInfo>($"v3/ips/{PathEscape.Segment(ip)}", null, cancellationToken,
            routeTemplate: "v3/ips/{ip}");
    }

    public Task<IpDomainsResponse> ListDomainsAsync(string ip, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        return _http.GetJsonAsync<IpDomainsResponse>($"v3/ips/{PathEscape.Segment(ip)}/domains", null, cancellationToken,
            routeTemplate: "v3/ips/{ip}/domains");
    }

    public Task<IpsListResponse> ListByDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.GetJsonAsync<IpsListResponse>($"v3/domains/{PathEscape.Segment(domain)}/ips", null, cancellationToken,
            routeTemplate: "v3/domains/{domain}/ips");
    }

    public Task AttachToDomainAsync(string domain, string ip, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        var fb = new FormBuilder().Add("ip", ip);
        return _http.PostFormNoResponseAsync($"v3/domains/{PathEscape.Segment(domain)}/ips", fb, cancellationToken,
            routeTemplate: "v3/domains/{domain}/ips");
    }

    public Task DetachFromDomainAsync(string domain, string ip, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        return _http.DeleteNoResponseAsync($"v3/domains/{PathEscape.Segment(domain)}/ips/{PathEscape.Segment(ip)}", cancellationToken,
            routeTemplate: "v3/domains/{domain}/ips/{ip}");
    }

    public Task RequestNewAsync(CancellationToken cancellationToken = default) =>
        _http.PostFormNoResponseAsync("v3/ips/request/new", new FormBuilder(), cancellationToken,
            routeTemplate: "v3/ips/request/new");

    public Task<IpReputationBand> GetReputationBandAsync(string ip, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        return _http.GetJsonAsync<IpReputationBand>($"v3/ips/{PathEscape.Segment(ip)}/ip_band", null, cancellationToken,
            routeTemplate: "v3/ips/{ip}/ip_band");
    }

    public Task<IpsListResponse> ListDetailedAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<IpsListResponse>("v3/ips/details", null, cancellationToken,
            routeTemplate: "v3/ips/details");

    public Task<IpsListResponse> ListAllAccountIpsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<IpsListResponse>("v3/ips/all", null, cancellationToken,
            routeTemplate: "v3/ips/all");

    public Task<IpsBulkOperationResponse> DetachIpFromAllDomainsAsync(string ip, string alternativeIp, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        ArgumentException.ThrowIfNullOrWhiteSpace(alternativeIp);
        var q = new QueryBuilder().Add("alternative", alternativeIp).Build();
        return _http.DeleteJsonAsync<IpsBulkOperationResponse>(
            $"v3/ips/{PathEscape.Segment(ip)}/domains", q, cancellationToken,
            routeTemplate: "v3/ips/{ip}/domains");
    }

    public Task<IpsDetailedListResponse> ListAllDetailedAsync(
        int? limit = null,
        int? skip = null,
        string? poolId = null,
        string? domainId = null,
        string? subaccountId = null,
        string? ip = null,
        string? sortBy = null,
        string? sortOrder = null,
        CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder()
            .Add("limit", limit)
            .Add("skip", skip)
            .Add("pool_id", poolId)
            .Add("domain_id", domainId)
            .Add("subaccount_id", subaccountId)
            .Add("ip", ip)
            .Add("sort_by", sortBy)
            .Add("sort_order", sortOrder)
            .Build();
        return _http.GetJsonAsync<IpsDetailedListResponse>("v3/ips/details/all", q, cancellationToken,
            routeTemplate: "v3/ips/details/all");
    }
}
