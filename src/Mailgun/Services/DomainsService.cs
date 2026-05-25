using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Models.Domains;
using Mailgun.Models.Domains.Envelopes;
using Mailgun.Pagination;

namespace Mailgun.Services;

internal sealed class DomainsService : IDomainsService
{
    private readonly MailgunHttpClient _http;

    public DomainsService(MailgunHttpClient http) => _http = http;

    public Task<SkipLimitPage<Domain>> ListAsync(ListDomainsOptions? options = null, CancellationToken cancellationToken = default)
    {
        return _http.GetSkipLimitPageAsync<Domain, DomainListEnvelope>(
            path: "v4/domains",
            query: BuildListQuery(options),
            absoluteUrlOrNull: null,
            itemsSelector: e => e.Items,
            pagingSelector: e => e.Paging,
            totalCountSelector: e => e.TotalCount,
            ct: cancellationToken);
    }

    public AsyncPageable<Domain> ListAllAsync(ListDomainsOptions? options = null)
    {
        return _http.GetSkipLimitPageable<Domain, DomainListEnvelope>(
            path: "v4/domains",
            firstPageQuery: BuildListQuery(options),
            itemsSelector: e => e.Items,
            pagingSelector: e => e.Paging,
            totalCountSelector: e => e.TotalCount);
    }

    public Task<DomainResponse> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _http.GetJsonAsync<DomainResponse>($"v4/domains/{PathEscape.Segment(name)}", query: null, cancellationToken);
    }

    public Task<DomainResponse> CreateAsync(CreateDomainRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("CreateDomainRequest.Name is required.", nameof(request));

        var fb = new FormBuilder()
            .Add("name", request.Name)
            .Add("smtp_password", request.SmtpPassword)
            .Add("spam_action", request.SpamAction)
            .Add("wildcard", request.Wildcard)
            .Add("force_dkim_authority", request.ForceDkimAuthority)
            .Add("dkim_key_size", request.DkimKeySize)
            .Add("pool_id", request.PoolId)
            .Add("web_scheme", request.WebScheme)
            .Add("use_automatic_sender_security", request.UseAutomaticSenderSecurity);
        if (request.Ips.Count > 0)
            fb.Add("ips", string.Join(",", request.Ips));
        return _http.PostFormAsync<DomainResponse>("v4/domains", fb, cancellationToken);
    }

    public Task<DomainResponse> UpdateAsync(string name, UpdateDomainRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(request);
        var fb = new FormBuilder()
            .Add("spam_action", request.SpamAction)
            .Add("wildcard", request.Wildcard)
            .Add("web_scheme", request.WebScheme)
            .Add("use_automatic_sender_security", request.UseAutomaticSenderSecurity);
        return _http.PutFormAsync<DomainResponse>($"v4/domains/{PathEscape.Segment(name)}", fb, cancellationToken);
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        // Delete still lives on the v3 path per current docs.
        return _http.DeleteNoResponseAsync($"v3/domains/{PathEscape.Segment(name)}", cancellationToken);
    }

    public Task<DomainResponse> VerifyAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _http.PutFormAsync<DomainResponse>($"v4/domains/{PathEscape.Segment(name)}/verify", new FormBuilder(), cancellationToken);
    }

    public Task<TrackingSettings> GetTrackingAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _http.GetJsonAsync<TrackingSettings>($"v3/domains/{PathEscape.Segment(name)}/tracking", query: null, cancellationToken);
    }

    public Task UpdateOpenTrackingAsync(string name, bool active, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var fb = new FormBuilder().Add("active", active);
        return _http.PutFormNoResponseAsync($"v3/domains/{PathEscape.Segment(name)}/tracking/open", fb, cancellationToken);
    }

    public Task UpdateClickTrackingAsync(string name, string active, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(active);
        var fb = new FormBuilder().Add("active", active);
        return _http.PutFormNoResponseAsync($"v3/domains/{PathEscape.Segment(name)}/tracking/click", fb, cancellationToken);
    }

    public Task UpdateUnsubscribeTrackingAsync(string name, bool active, string? htmlFooter = null, string? textFooter = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var fb = new FormBuilder().Add("active", active);
        fb.Add("html_footer", htmlFooter);
        fb.Add("text_footer", textFooter);
        return _http.PutFormNoResponseAsync($"v3/domains/{PathEscape.Segment(name)}/tracking/unsubscribe", fb, cancellationToken);
    }

    public Task<SkipLimitPage<SmtpCredential>> ListSmtpCredentialsAsync(string domain, int? limit = null, int? skip = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var q = new QueryBuilder().Add("limit", limit).Add("skip", skip).Build();
        return _http.GetSkipLimitPageAsync<SmtpCredential, SmtpCredentialsEnvelope>(
            path: $"v3/domains/{PathEscape.Segment(domain)}/credentials",
            query: q,
            absoluteUrlOrNull: null,
            itemsSelector: e => e.Items,
            pagingSelector: e => e.Paging,
            totalCountSelector: e => e.TotalCount,
            ct: cancellationToken);
    }

    public Task CreateSmtpCredentialAsync(string domain, string login, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var fb = new FormBuilder().Add("login", login).Add("password", password);
        return _http.PostFormNoResponseAsync($"v3/domains/{PathEscape.Segment(domain)}/credentials", fb, cancellationToken);
    }

    public Task UpdateSmtpCredentialAsync(string domain, string login, string newPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);
        var fb = new FormBuilder().Add("password", newPassword);
        return _http.PutFormNoResponseAsync(
            $"v3/domains/{PathEscape.Segment(domain)}/credentials/{PathEscape.Segment(login)}",
            fb, cancellationToken);
    }

    public Task DeleteSmtpCredentialAsync(string domain, string login, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        return _http.DeleteNoResponseAsync(
            $"v3/domains/{PathEscape.Segment(domain)}/credentials/{PathEscape.Segment(login)}",
            cancellationToken);
    }

    public Task UpdateConnectionSettingsAsync(string domain, bool? requireTls = null, bool? skipVerification = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var fb = new FormBuilder().Add("require_tls", requireTls).Add("skip_verification", skipVerification);
        return _http.PutFormNoResponseAsync($"v3/domains/{PathEscape.Segment(domain)}/connection", fb, cancellationToken);
    }

    private static IReadOnlyList<KeyValuePair<string, string?>> BuildListQuery(ListDomainsOptions? options)
    {
        var qb = new QueryBuilder()
            .Add("limit", options?.Limit)
            .Add("skip", options?.Skip)
            .Add("filter", options?.Filter)
            .Add("state", options?.State);
        return qb.Build();
    }
}
