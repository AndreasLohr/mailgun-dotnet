using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Models.Keys;
using Mailgun.Pagination;

namespace Mailgun.Services;

internal sealed class KeysService : IKeysService
{
    private readonly MailgunHttpClient _http;
    public KeysService(MailgunHttpClient http) => _http = http;

    public Task<SkipLimitPage<ApiKey>> ListAsync(int? limit = null, int? skip = null, string? kind = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder().Add("limit", limit).Add("skip", skip).Add("kind", kind).Build();
        return _http.GetSkipLimitPageAsync<ApiKey, KeyListEnvelope>(
            "v1/keys", q, null, e => e.Items, e => e.Paging, e => e.TotalCount, cancellationToken,
            routeTemplate: "v1/keys");
    }

    public AsyncPageable<ApiKey> ListAllAsync(string? kind = null)
    {
        var q = new QueryBuilder().Add("kind", kind).Build();
        return _http.GetSkipLimitPageable<ApiKey, KeyListEnvelope>(
            "v1/keys", q, e => e.Items, e => e.Paging, e => e.TotalCount,
            routeTemplate: "v1/keys");
    }

    public Task<CreatedApiKey> CreateAsync(CreateApiKeyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var fb = new FormBuilder()
            .Add("description", request.Description)
            .Add("role", request.Role)
            .Add("kind", request.Kind)
            .Add("domain", request.Domain)
            .Add("expires_at", request.ExpiresAt);
        return _http.PostFormAsync<CreatedApiKey>("v1/keys", fb, cancellationToken,
            routeTemplate: "v1/keys");
    }

    public Task DeleteAsync(string keyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        return _http.DeleteNoResponseAsync($"v1/keys/{PathEscape.Segment(keyId)}", cancellationToken,
            routeTemplate: "v1/keys/{key_id}");
    }

    public Task<RegeneratedPublicKey> RegeneratePublicKeyAsync(CancellationToken cancellationToken = default) =>
        _http.PostJsonBodyAsync<RegeneratedPublicKey>("v1/keys/public", new { }, cancellationToken,
            routeTemplate: "v1/keys/public");
}
