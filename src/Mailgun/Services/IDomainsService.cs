using Mailgun.Models.Domains;
using Mailgun.Pagination;

namespace Mailgun.Services;

/// <summary>
/// Endpoints under <c>/v4/domains</c> (modern), plus tracking, SMTP credentials, and DKIM sub-services.
/// </summary>
public interface IDomainsService
{
    /// <summary><c>GET /v4/domains</c> — list domains (one page).</summary>
    Task<SkipLimitPage<Domain>> ListAsync(ListDomainsOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/domains</c> — auto-paginated stream of all domains.</summary>
    AsyncPageable<Domain> ListAllAsync(ListDomainsOptions? options = null);

    /// <summary><c>GET /v4/domains/{name}</c> — get one domain (with DNS records).</summary>
    Task<DomainResponse> GetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v4/domains</c> — create a domain.</summary>
    Task<DomainResponse> CreateAsync(CreateDomainRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v4/domains/{name}</c> — update a domain.</summary>
    Task<DomainResponse> UpdateAsync(string name, UpdateDomainRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v3/domains/{name}</c> — delete a domain.</summary>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v4/domains/{name}/verify</c> — re-run DNS verification.</summary>
    Task<DomainResponse> VerifyAsync(string name, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v3/domains/{name}/tracking</c> — get the open/click/unsubscribe tracking settings.</summary>
    Task<TrackingSettings> GetTrackingAsync(string name, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v3/domains/{name}/tracking/open</c> — update open tracking.</summary>
    Task UpdateOpenTrackingAsync(string name, bool active, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v3/domains/{name}/tracking/click</c> — update click tracking. <paramref name="active"/> = <c>"yes" | "no" | "htmlonly"</c>.</summary>
    Task UpdateClickTrackingAsync(string name, string active, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v3/domains/{name}/tracking/unsubscribe</c> — update unsubscribe tracking.</summary>
    Task UpdateUnsubscribeTrackingAsync(string name, bool active, string? htmlFooter = null, string? textFooter = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v3/domains/{domain}/credentials</c> — list SMTP credentials for the domain.</summary>
    Task<SkipLimitPage<SmtpCredential>> ListSmtpCredentialsAsync(string domain, int? limit = null, int? skip = null, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v3/domains/{domain}/credentials</c> — create an SMTP credential.</summary>
    Task CreateSmtpCredentialAsync(string domain, string login, string password, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v3/domains/{domain}/credentials/{login}</c> — update an SMTP credential's password.</summary>
    Task UpdateSmtpCredentialAsync(string domain, string login, string newPassword, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v3/domains/{domain}/credentials/{login}</c> — delete an SMTP credential.</summary>
    Task DeleteSmtpCredentialAsync(string domain, string login, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v3/domains/{domain}/credentials</c> — delete every SMTP credential under the domain.
    /// The response carries the count of credentials Mailgun removed.
    /// </summary>
    Task<DeleteAllSmtpCredentialsResponse> DeleteAllSmtpCredentialsAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v3/domains/{domain}/connection</c> — toggle <c>require_tls</c> / <c>skip_verification</c>.</summary>
    Task UpdateConnectionSettingsAsync(string domain, bool? requireTls = null, bool? skipVerification = null, CancellationToken cancellationToken = default);
}
