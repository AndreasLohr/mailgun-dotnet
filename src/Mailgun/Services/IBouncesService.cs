using Mailgun.Models.Suppressions;
using Mailgun.Pagination;

namespace Mailgun.Services;

/// <summary>Endpoints under <c>/v3/{domain}/bounces</c>.</summary>
public interface IBouncesService
{
    /// <summary><c>GET /v3/{domain}/bounces</c> — list one page of bounces.</summary>
    Task<SkipLimitPage<Bounce>> ListAsync(string domain, int? limit = null, int? skip = null, CancellationToken cancellationToken = default);

    /// <summary>Auto-paginated stream of all bounces on the domain.</summary>
    AsyncPageable<Bounce> ListAllAsync(string domain, int? limit = null);

    /// <summary><c>GET /v3/{domain}/bounces/{address}</c> — get a single bounce.</summary>
    Task<Bounce> GetAsync(string domain, string address, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v3/{domain}/bounces</c> — add a bounce.</summary>
    Task CreateAsync(string domain, string address, string? code = null, string? error = null, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v3/{domain}/bounces/{address}</c> — delete one bounce.</summary>
    Task DeleteAsync(string domain, string address, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v3/{domain}/bounces</c> — delete all bounces for the domain.</summary>
    Task DeleteAllAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v3/{domain}/bounces/import</c> — bulk import bounces from a CSV stream
    /// (columns: <c>address,code,error,created_at</c>).
    /// </summary>
    Task ImportCsvAsync(string domain, Stream csvStream, string fileName = "bounces.csv", CancellationToken cancellationToken = default);
}

/// <summary>Endpoints under <c>/v3/{domain}/complaints</c>.</summary>
public interface IComplaintsService
{
    Task<SkipLimitPage<Complaint>> ListAsync(string domain, int? limit = null, int? skip = null, CancellationToken cancellationToken = default);
    AsyncPageable<Complaint> ListAllAsync(string domain, int? limit = null);
    Task<Complaint> GetAsync(string domain, string address, CancellationToken cancellationToken = default);
    Task CreateAsync(string domain, string address, CancellationToken cancellationToken = default);
    Task DeleteAsync(string domain, string address, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(string domain, CancellationToken cancellationToken = default);
    Task ImportCsvAsync(string domain, Stream csvStream, string fileName = "complaints.csv", CancellationToken cancellationToken = default);
}

/// <summary>Endpoints under <c>/v3/{domain}/unsubscribes</c>.</summary>
public interface IUnsubscribesService
{
    Task<SkipLimitPage<Unsubscribe>> ListAsync(string domain, int? limit = null, int? skip = null, CancellationToken cancellationToken = default);
    AsyncPageable<Unsubscribe> ListAllAsync(string domain, int? limit = null);
    Task<Unsubscribe> GetAsync(string domain, string address, CancellationToken cancellationToken = default);
    Task CreateAsync(string domain, string address, IReadOnlyList<string>? tags = null, CancellationToken cancellationToken = default);
    /// <summary>Remove an unsubscribe; if <paramref name="tag"/> is supplied, only remove for that tag.</summary>
    Task DeleteAsync(string domain, string address, string? tag = null, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(string domain, CancellationToken cancellationToken = default);
    Task ImportCsvAsync(string domain, Stream csvStream, string fileName = "unsubscribes.csv", CancellationToken cancellationToken = default);
}

/// <summary>Endpoints under <c>/v3/{domain}/whitelists</c>.</summary>
public interface IAllowlistsService
{
    Task<SkipLimitPage<AllowlistEntry>> ListAsync(string domain, int? limit = null, int? skip = null, CancellationToken cancellationToken = default);
    AsyncPageable<AllowlistEntry> ListAllAsync(string domain, int? limit = null);
    Task<AllowlistEntry> GetAsync(string domain, string value, CancellationToken cancellationToken = default);
    /// <summary>Either <paramref name="address"/> or <paramref name="domainValue"/> must be set (not both).</summary>
    Task CreateAsync(string domain, string? address = null, string? domainValue = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(string domain, string value, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(string domain, CancellationToken cancellationToken = default);
    Task ImportCsvAsync(string domain, Stream csvStream, string fileName = "whitelists.csv", CancellationToken cancellationToken = default);
}
