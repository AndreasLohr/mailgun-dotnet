using Mailgun.Models.Keys;
using Mailgun.Pagination;

namespace Mailgun.Services;

/// <summary>Operations on <c>/v1/keys</c>.</summary>
public interface IKeysService
{
    /// <summary><c>GET /v1/keys</c> — list API keys.</summary>
    Task<SkipLimitPage<ApiKey>> ListAsync(int? limit = null, int? skip = null, string? kind = null, CancellationToken cancellationToken = default);

    /// <summary>Auto-paginated stream of all keys.</summary>
    AsyncPageable<ApiKey> ListAllAsync(string? kind = null);

    /// <summary><c>POST /v1/keys</c> — create a new API key. The returned <see cref="CreatedApiKey.Key"/> is shown once.</summary>
    Task<CreatedApiKey> CreateAsync(CreateApiKeyRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v1/keys/{id}</c> — delete an API key.</summary>
    Task DeleteAsync(string keyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v1/keys/public</c> — regenerate the account's public API key. The new key is returned
    /// in the response; the previous key stops working immediately.
    /// </summary>
    Task<RegeneratedPublicKey> RegeneratePublicKeyAsync(CancellationToken cancellationToken = default);
}
