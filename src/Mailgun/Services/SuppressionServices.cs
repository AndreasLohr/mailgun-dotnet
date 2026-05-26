using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Models.Suppressions;
using Mailgun.Pagination;

namespace Mailgun.Services;

internal sealed class BouncesService : IBouncesService
{
    private readonly MailgunHttpClient _http;
    public BouncesService(MailgunHttpClient http) => _http = http;

    public Task<SkipLimitPage<Bounce>> ListAsync(string domain, int? limit = null, int? skip = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var q = new QueryBuilder().Add("limit", limit).Add("skip", skip).Build();
        return _http.GetSkipLimitPageAsync<Bounce, BounceListEnvelope>(
            $"v3/{PathEscape.Segment(domain)}/bounces", q, null,
            e => e.Items, e => e.Paging, e => e.TotalCount, cancellationToken);
    }

    public AsyncPageable<Bounce> ListAllAsync(string domain, int? limit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var q = new QueryBuilder().Add("limit", limit).Build();
        return _http.GetSkipLimitPageable<Bounce, BounceListEnvelope>(
            $"v3/{PathEscape.Segment(domain)}/bounces", q,
            e => e.Items, e => e.Paging, e => e.TotalCount);
    }

    public Task<Bounce> GetAsync(string domain, string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return _http.GetJsonAsync<Bounce>(
            $"v3/{PathEscape.Segment(domain)}/bounces/{PathEscape.Segment(address)}", null, cancellationToken);
    }

    public Task CreateAsync(string domain, string address, string? code = null, string? error = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var fb = new FormBuilder().Add("address", address).Add("code", code).Add("error", error);
        return _http.PostFormNoResponseAsync($"v3/{PathEscape.Segment(domain)}/bounces", fb, cancellationToken);
    }

    public Task DeleteAsync(string domain, string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return _http.DeleteNoResponseAsync($"v3/{PathEscape.Segment(domain)}/bounces/{PathEscape.Segment(address)}", cancellationToken);
    }

    public Task DeleteAllAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.DeleteNoResponseAsync($"v3/{PathEscape.Segment(domain)}/bounces", cancellationToken);
    }

    public async Task ImportCsvAsync(string domain, Stream csvStream, string fileName = "bounces.csv", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(csvStream);
        using var mp = new MultipartBuilder().AddFile("file", fileName, csvStream, "text/csv");
        await _http.PostMultipartNoResponseAsync($"v3/{PathEscape.Segment(domain)}/bounces/import", mp, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ComplaintsService : IComplaintsService
{
    private readonly MailgunHttpClient _http;
    public ComplaintsService(MailgunHttpClient http) => _http = http;

    public Task<SkipLimitPage<Complaint>> ListAsync(string domain, int? limit = null, int? skip = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var q = new QueryBuilder().Add("limit", limit).Add("skip", skip).Build();
        return _http.GetSkipLimitPageAsync<Complaint, ComplaintListEnvelope>(
            $"v3/{PathEscape.Segment(domain)}/complaints", q, null,
            e => e.Items, e => e.Paging, e => e.TotalCount, cancellationToken);
    }

    public AsyncPageable<Complaint> ListAllAsync(string domain, int? limit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var q = new QueryBuilder().Add("limit", limit).Build();
        return _http.GetSkipLimitPageable<Complaint, ComplaintListEnvelope>(
            $"v3/{PathEscape.Segment(domain)}/complaints", q,
            e => e.Items, e => e.Paging, e => e.TotalCount);
    }

    public Task<Complaint> GetAsync(string domain, string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return _http.GetJsonAsync<Complaint>(
            $"v3/{PathEscape.Segment(domain)}/complaints/{PathEscape.Segment(address)}", null, cancellationToken);
    }

    public Task CreateAsync(string domain, string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var fb = new FormBuilder().Add("address", address);
        return _http.PostFormNoResponseAsync($"v3/{PathEscape.Segment(domain)}/complaints", fb, cancellationToken);
    }

    public Task DeleteAsync(string domain, string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return _http.DeleteNoResponseAsync($"v3/{PathEscape.Segment(domain)}/complaints/{PathEscape.Segment(address)}", cancellationToken);
    }

    public Task DeleteAllAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.DeleteNoResponseAsync($"v3/{PathEscape.Segment(domain)}/complaints", cancellationToken);
    }

    public async Task ImportCsvAsync(string domain, Stream csvStream, string fileName = "complaints.csv", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(csvStream);
        using var mp = new MultipartBuilder().AddFile("file", fileName, csvStream, "text/csv");
        await _http.PostMultipartNoResponseAsync($"v3/{PathEscape.Segment(domain)}/complaints/import", mp, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class UnsubscribesService : IUnsubscribesService
{
    private readonly MailgunHttpClient _http;
    public UnsubscribesService(MailgunHttpClient http) => _http = http;

    public Task<SkipLimitPage<Unsubscribe>> ListAsync(string domain, int? limit = null, int? skip = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var q = new QueryBuilder().Add("limit", limit).Add("skip", skip).Build();
        return _http.GetSkipLimitPageAsync<Unsubscribe, UnsubscribeListEnvelope>(
            $"v3/{PathEscape.Segment(domain)}/unsubscribes", q, null,
            e => e.Items, e => e.Paging, e => e.TotalCount, cancellationToken);
    }

    public AsyncPageable<Unsubscribe> ListAllAsync(string domain, int? limit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var q = new QueryBuilder().Add("limit", limit).Build();
        return _http.GetSkipLimitPageable<Unsubscribe, UnsubscribeListEnvelope>(
            $"v3/{PathEscape.Segment(domain)}/unsubscribes", q,
            e => e.Items, e => e.Paging, e => e.TotalCount);
    }

    public Task<Unsubscribe> GetAsync(string domain, string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return _http.GetJsonAsync<Unsubscribe>(
            $"v3/{PathEscape.Segment(domain)}/unsubscribes/{PathEscape.Segment(address)}", null, cancellationToken);
    }

    public Task CreateAsync(string domain, string address, IReadOnlyList<string>? tags = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var fb = new FormBuilder().Add("address", address);
        if (tags is { Count: > 0 })
            fb.Add("tags", string.Join(",", tags));
        return _http.PostFormNoResponseAsync($"v3/{PathEscape.Segment(domain)}/unsubscribes", fb, cancellationToken);
    }

    public Task DeleteAsync(string domain, string address, string? tag = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        // Mailgun accepts ?tag= on DELETE for unsubscribes; we route the optional filter through the
        // standard query channel rather than splicing it into the path so BuildUri owns the encoding.
        var query = new QueryBuilder().Add("tag", tag).Build();
        return _http.DeleteNoResponseAsync(
            $"v3/{PathEscape.Segment(domain)}/unsubscribes/{PathEscape.Segment(address)}",
            query, cancellationToken);
    }

    public Task DeleteAllAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.DeleteNoResponseAsync($"v3/{PathEscape.Segment(domain)}/unsubscribes", cancellationToken);
    }

    public async Task ImportCsvAsync(string domain, Stream csvStream, string fileName = "unsubscribes.csv", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(csvStream);
        using var mp = new MultipartBuilder().AddFile("file", fileName, csvStream, "text/csv");
        await _http.PostMultipartNoResponseAsync($"v3/{PathEscape.Segment(domain)}/unsubscribes/import", mp, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class AllowlistsService : IAllowlistsService
{
    private readonly MailgunHttpClient _http;
    public AllowlistsService(MailgunHttpClient http) => _http = http;

    public Task<SkipLimitPage<AllowlistEntry>> ListAsync(string domain, int? limit = null, int? skip = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var q = new QueryBuilder().Add("limit", limit).Add("skip", skip).Build();
        return _http.GetSkipLimitPageAsync<AllowlistEntry, AllowlistListEnvelope>(
            $"v3/{PathEscape.Segment(domain)}/whitelists", q, null,
            e => e.Items, e => e.Paging, e => e.TotalCount, cancellationToken);
    }

    public AsyncPageable<AllowlistEntry> ListAllAsync(string domain, int? limit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var q = new QueryBuilder().Add("limit", limit).Build();
        return _http.GetSkipLimitPageable<AllowlistEntry, AllowlistListEnvelope>(
            $"v3/{PathEscape.Segment(domain)}/whitelists", q,
            e => e.Items, e => e.Paging, e => e.TotalCount);
    }

    public Task<AllowlistEntry> GetAsync(string domain, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return _http.GetJsonAsync<AllowlistEntry>(
            $"v3/{PathEscape.Segment(domain)}/whitelists/{PathEscape.Segment(value)}", null, cancellationToken);
    }

    public Task CreateAsync(string domain, string? address = null, string? domainValue = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        // Mailgun's whitelist entry is either an address entry or a domain entry — never both.
        // Sending both fields produces an ambiguous request on the wire; the interface doc has
        // always said "not both" but the previous implementation only rejected the neither case.
        var hasAddress = !string.IsNullOrWhiteSpace(address);
        var hasDomain = !string.IsNullOrWhiteSpace(domainValue);
        if (hasAddress == hasDomain)
            throw new ArgumentException("Supply exactly one of address or domainValue.");
        var fb = new FormBuilder();
        fb.Add("address", address);
        fb.Add("domain", domainValue);
        return _http.PostFormNoResponseAsync($"v3/{PathEscape.Segment(domain)}/whitelists", fb, cancellationToken);
    }

    public Task DeleteAsync(string domain, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return _http.DeleteNoResponseAsync(
            $"v3/{PathEscape.Segment(domain)}/whitelists/{PathEscape.Segment(value)}", cancellationToken);
    }

    public Task DeleteAllAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.DeleteNoResponseAsync($"v3/{PathEscape.Segment(domain)}/whitelists", cancellationToken);
    }

    public async Task ImportCsvAsync(string domain, Stream csvStream, string fileName = "whitelists.csv", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(csvStream);
        using var mp = new MultipartBuilder().AddFile("file", fileName, csvStream, "text/csv");
        await _http.PostMultipartNoResponseAsync($"v3/{PathEscape.Segment(domain)}/whitelists/import", mp, cancellationToken).ConfigureAwait(false);
    }
}
