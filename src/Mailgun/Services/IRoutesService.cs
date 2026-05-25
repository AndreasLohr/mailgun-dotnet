using Mailgun.Models.Routes;
using Mailgun.Pagination;

namespace Mailgun.Services;

/// <summary>Operations on <c>/v3/routes</c>.</summary>
public interface IRoutesService
{
    Task<SkipLimitPage<Route>> ListAsync(int? limit = null, int? skip = null, CancellationToken cancellationToken = default);
    AsyncPageable<Route> ListAllAsync(int? limit = null);
    Task<Route> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<Route> CreateAsync(CreateRouteRequest request, CancellationToken cancellationToken = default);
    Task<Route> UpdateAsync(string id, UpdateRouteRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v3/routes/match</c> — return the routes that match the supplied recipient.</summary>
    Task<RouteMatchResult> MatchAsync(string recipient, CancellationToken cancellationToken = default);
}
