using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;

namespace Mailgun.Services;

/// <summary>
/// Operations on <c>/v2/ip_whitelist</c> — Mailgun's account-level IP allowlist (formerly
/// "IP whitelist" on the wire). Entries control which source IPs may use the account's API
/// keys; when the allowlist is empty the account accepts requests from any source.
/// </summary>
public interface IIpAllowlistService
{
    /// <summary><c>GET /v2/ip_whitelist</c> — list every allowlisted IP entry.</summary>
    Task<IpAllowlistResponse> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v2/ip_whitelist</c> — add an IP to the allowlist with an optional description.
    /// </summary>
    Task<IpAllowlistResponse> AddAsync(string address, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v2/ip_whitelist</c> — update an existing entry's description. <paramref name="address"/>
    /// identifies the row; <paramref name="description"/> is the new description.
    /// </summary>
    Task<IpAllowlistResponse> UpdateDescriptionAsync(string address, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v2/ip_whitelist?address=…</c> — remove an IP from the allowlist.
    /// </summary>
    Task<IpAllowlistResponse> DeleteAsync(string address, CancellationToken cancellationToken = default);
}

/// <summary>A single allowlist entry returned by <c>/v2/ip_whitelist</c>.</summary>
public sealed class IpAllowlistEntry
{
    [JsonPropertyName("ip_address")] public string IpAddress { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
}

/// <summary>Response envelope for every <c>/v2/ip_whitelist</c> operation.</summary>
public sealed class IpAllowlistResponse
{
    [JsonPropertyName("addresses")] public List<IpAllowlistEntry>? Addresses { get; init; }
}

internal sealed class IpAllowlistService : IIpAllowlistService
{
    private readonly MailgunHttpClient _http;
    public IpAllowlistService(MailgunHttpClient http) => _http = http;

    // Path + route template are deliberately inlined as string literals at every callsite — the
    // cardinality-guard analyzer (RouteTemplateLiteralTests) enforces literal-only routeTemplate
    // arguments to keep metric tag cardinality bounded.

    public Task<IpAllowlistResponse> ListAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<IpAllowlistResponse>("v2/ip_whitelist", null, cancellationToken, routeTemplate: "v2/ip_whitelist");

    public Task<IpAllowlistResponse> AddAsync(string address, string? description = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        using var mp = new MultipartBuilder()
            .AddText("address", address)
            .AddText("description", description);
        return _http.PostMultipartAsync<IpAllowlistResponse>("v2/ip_whitelist", mp, cancellationToken, routeTemplate: "v2/ip_whitelist");
    }

    public Task<IpAllowlistResponse> UpdateDescriptionAsync(string address, string? description = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        using var mp = new MultipartBuilder()
            .AddText("address", address)
            .AddText("description", description);
        return _http.PutMultipartAsync<IpAllowlistResponse>("v2/ip_whitelist", mp, cancellationToken, routeTemplate: "v2/ip_whitelist");
    }

    public Task<IpAllowlistResponse> DeleteAsync(string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var q = new QueryBuilder().Add("address", address).Build();
        return _http.DeleteJsonAsync<IpAllowlistResponse>("v2/ip_whitelist", q, cancellationToken, routeTemplate: "v2/ip_whitelist");
    }
}
