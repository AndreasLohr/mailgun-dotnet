using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Serialization;

namespace Mailgun.Services;

/// <summary>
/// Operations on DKIM keys: account-wide listing under <c>/v1/dkim/keys</c> and
/// per-domain-authority management under <c>/v4/domains/{authority}/keys/...</c>.
/// </summary>
public interface IDkimKeysService
{
    /// <summary><c>GET /v1/dkim/keys</c> — list every DKIM key on the account, optionally filtered by signing domain or selector and paginated via the opaque <paramref name="page"/> cursor.</summary>
    Task<DkimKeyListResponse> ListAllAsync(int? limit = null, string? signingDomain = null, string? selector = null, string? page = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v1/dkim/keys</c> — create a DKIM key for the given signing domain (legacy
    /// surface; prefer <see cref="CreateForAuthorityAsync(string, CreateDkimKeyRequest, CancellationToken)"/>).
    /// </summary>
    Task CreateAsync(CreateDkimKeyForSigningDomainRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v1/dkim/keys?signing_domain={signingDomain}&amp;selector={selector}</c> — delete a key
    /// by signing-domain + selector.
    /// </summary>
    Task DeleteAsync(string signingDomain, string selector, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/domains/{authority}/keys</c> — list DKIM keys for a specific domain authority.</summary>
    Task<DkimKeyListResponse> ListForAuthorityAsync(string authority, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v4/domains/{authority}/keys</c> — create a new DKIM key for a domain authority.</summary>
    Task<DkimKey> CreateForAuthorityAsync(string authority, CreateDkimKeyRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v4/domains/{authority}/keys/{selector}/activate</c> — activate a DKIM key (start using it to sign mail).
    /// DNS records must be valid for activation to succeed.
    /// </summary>
    Task<DkimKeyActivationResult> ActivateForAuthorityAsync(string authority, string selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v4/domains/{authority}/keys/{selector}/deactivate</c> — deactivate a DKIM key (stop using it for signing).
    /// </summary>
    Task<DkimKeyActivationResult> DeactivateForAuthorityAsync(string authority, string selector, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v4/domains/{authority}/keys/{selector}</c> — delete a DKIM key under a domain authority.</summary>
    Task DeleteForAuthorityAsync(string authority, string selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v3/domains/{domain}/dkim_authority</c> — set whether the domain is its own DKIM authority.
    /// When <paramref name="self"/> is <c>true</c>, the domain signs its own mail even if a root-domain
    /// authority is registered on the same Mailgun account.
    /// </summary>
    Task UpdateDkimAuthorityAsync(string domain, bool self, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v3/domains/{domain}/dkim_selector</c> — change the DKIM selector used for a domain's outgoing signature.
    /// </summary>
    Task UpdateDkimSelectorAsync(string domain, string dkimSelector, CancellationToken cancellationToken = default);
}

/// <summary>A DKIM key as returned by Mailgun.</summary>
public sealed class DkimKey
{
    [JsonPropertyName("signing_domain")] public string? SigningDomain { get; init; }
    [JsonPropertyName("selector")] public string Selector { get; init; } = string.Empty;
    [JsonPropertyName("public_key")] public string? PublicKey { get; init; }
    [JsonPropertyName("activated")] public bool? Activated { get; init; }
    [JsonPropertyName("size")] public int? Size { get; init; }
    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>List-of-DKIM-keys response.</summary>
public sealed class DkimKeyListResponse
{
    [JsonPropertyName("items")] public List<DkimKey>? Items { get; init; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; init; }
}

/// <summary>
/// Result returned by <c>PUT /v4/domains/{authority}/keys/{selector}/(de)activate</c>.
/// </summary>
public sealed class DkimKeyActivationResult
{
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("authority")] public string? Authority { get; init; }
    [JsonPropertyName("selector")] public string? Selector { get; init; }
    [JsonPropertyName("active")] public bool? Active { get; init; }
}

/// <summary>Parameters for the legacy <c>POST /v1/dkim/keys</c>.</summary>
public sealed class CreateDkimKeyForSigningDomainRequest
{
    [JsonPropertyName("signing_domain")] public string SigningDomain { get; set; } = string.Empty;
    [JsonPropertyName("selector")] public string Selector { get; set; } = string.Empty;
    [JsonPropertyName("bits")] public int? Bits { get; set; }

    /// <summary>
    /// Optional PEM-encoded private key. Set this to bring your own DKIM key instead of letting
    /// Mailgun generate one (<see cref="Bits"/> is then ignored).
    /// </summary>
    [JsonPropertyName("pem")] public string? Pem { get; set; }
}

/// <summary>Parameters for <c>POST /v4/domains/{authority}/keys</c>.</summary>
public sealed class CreateDkimKeyRequest
{
    [JsonPropertyName("signing_domain")] public string SigningDomain { get; set; } = string.Empty;
    [JsonPropertyName("selector")] public string Selector { get; set; } = string.Empty;
    [JsonPropertyName("bits")] public int? Bits { get; set; }
}

internal sealed class DkimKeysService : IDkimKeysService
{
    private readonly MailgunHttpClient _http;
    public DkimKeysService(MailgunHttpClient http) => _http = http;

    public Task<DkimKeyListResponse> ListAllAsync(int? limit = null, string? signingDomain = null, string? selector = null, string? page = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder()
            .Add("limit", limit)
            .Add("signing_domain", signingDomain)
            .Add("selector", selector)
            .Add("page", page)
            .Build();
        return _http.GetJsonAsync<DkimKeyListResponse>("v1/dkim/keys", q, cancellationToken,
            routeTemplate: "v1/dkim/keys");
    }

    public Task CreateAsync(CreateDkimKeyForSigningDomainRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SigningDomain);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Selector);
        return _http.PostJsonBodyNoResponseAsync("v1/dkim/keys", request, cancellationToken,
            routeTemplate: "v1/dkim/keys");
    }

    public Task DeleteAsync(string signingDomain, string selector, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingDomain);
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        var query = new QueryBuilder()
            .Add("signing_domain", signingDomain)
            .Add("selector", selector)
            .Build();
        return _http.DeleteNoResponseAsync("v1/dkim/keys", query, cancellationToken,
            routeTemplate: "v1/dkim/keys");
    }

    public Task<DkimKeyListResponse> ListForAuthorityAsync(string authority, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        return _http.GetJsonAsync<DkimKeyListResponse>($"v4/domains/{PathEscape.Segment(authority)}/keys", null, cancellationToken,
            routeTemplate: "v4/domains/{authority}/keys");
    }

    public Task<DkimKey> CreateForAuthorityAsync(string authority, CreateDkimKeyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SigningDomain);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Selector);
        return _http.PostJsonBodyAsync<DkimKey>($"v4/domains/{PathEscape.Segment(authority)}/keys", request, cancellationToken,
            routeTemplate: "v4/domains/{authority}/keys");
    }

    public Task<DkimKeyActivationResult> ActivateForAuthorityAsync(string authority, string selector, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        // Mailgun's documented endpoint: PUT /v4/domains/{authority}/keys/{selector}/activate, empty body.
        return _http.PutFormAsync<DkimKeyActivationResult>(
            $"v4/domains/{PathEscape.Segment(authority)}/keys/{PathEscape.Segment(selector)}/activate",
            new FormBuilder(), cancellationToken,
            routeTemplate: "v4/domains/{authority}/keys/{selector}/activate");
    }

    public Task<DkimKeyActivationResult> DeactivateForAuthorityAsync(string authority, string selector, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        return _http.PutFormAsync<DkimKeyActivationResult>(
            $"v4/domains/{PathEscape.Segment(authority)}/keys/{PathEscape.Segment(selector)}/deactivate",
            new FormBuilder(), cancellationToken,
            routeTemplate: "v4/domains/{authority}/keys/{selector}/deactivate");
    }

    public Task DeleteForAuthorityAsync(string authority, string selector, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        return _http.DeleteNoResponseAsync(
            $"v4/domains/{PathEscape.Segment(authority)}/keys/{PathEscape.Segment(selector)}",
            cancellationToken,
            routeTemplate: "v4/domains/{authority}/keys/{selector}");
    }

    public async Task UpdateDkimAuthorityAsync(string domain, bool self, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        // Mailgun's /v3/domains/{domain}/dkim_authority is documented multipart/form-data only,
        // with "true"/"false" string values for the self flag.
        using var mp = new MultipartBuilder().AddText("self", self ? "true" : "false");
        await _http.PutMultipartNoResponseAsync(
            $"v3/domains/{PathEscape.Segment(domain)}/dkim_authority", mp, cancellationToken,
            routeTemplate: "v3/domains/{domain}/dkim_authority").ConfigureAwait(false);
    }

    public async Task UpdateDkimSelectorAsync(string domain, string dkimSelector, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(dkimSelector);
        // Mailgun's /v3/domains/{domain}/dkim_selector is documented multipart/form-data only.
        using var mp = new MultipartBuilder().AddText("dkim_selector", dkimSelector);
        await _http.PutMultipartNoResponseAsync(
            $"v3/domains/{PathEscape.Segment(domain)}/dkim_selector", mp, cancellationToken,
            routeTemplate: "v3/domains/{domain}/dkim_selector").ConfigureAwait(false);
    }
}
