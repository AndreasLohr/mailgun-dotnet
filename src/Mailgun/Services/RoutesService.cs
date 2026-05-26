using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Models.Routes;
using Mailgun.Pagination;

namespace Mailgun.Services;

internal sealed class RoutesService : IRoutesService
{
    private readonly MailgunHttpClient _http;
    public RoutesService(MailgunHttpClient http) => _http = http;

    public Task<SkipLimitPage<Route>> ListAsync(int? limit = null, int? skip = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder().Add("limit", limit).Add("skip", skip).Build();
        return _http.GetSkipLimitPageAsync<Route, RouteListEnvelope>(
            "v3/routes", q, null, e => e.Items, e => e.Paging, e => e.TotalCount, cancellationToken,
            routeTemplate: "v3/routes");
    }

    public AsyncPageable<Route> ListAllAsync(int? limit = null)
    {
        var q = new QueryBuilder().Add("limit", limit).Build();
        return _http.GetSkipLimitPageable<Route, RouteListEnvelope>(
            "v3/routes", q, e => e.Items, e => e.Paging, e => e.TotalCount,
            routeTemplate: "v3/routes");
    }

    public async Task<Route> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var env = await _http.GetJsonAsync<RouteSingleEnvelope>($"v3/routes/{PathEscape.Segment(id)}", null, cancellationToken,
            routeTemplate: "v3/routes/{route_id}").ConfigureAwait(false);
        return env.Route;
    }

    public async Task<Route> CreateAsync(CreateRouteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Expression))
            throw new ArgumentException("Expression is required.", nameof(request));
        var fb = new FormBuilder()
            .Add("priority", request.Priority)
            .Add("description", request.Description)
            .Add("expression", request.Expression);
        foreach (var a in request.Actions)
            fb.Add("action", a);
        var env = await _http.PostFormAsync<RouteSingleEnvelope>("v3/routes", fb, cancellationToken,
            routeTemplate: "v3/routes").ConfigureAwait(false);
        return env.Route;
    }

    public async Task<Route> UpdateAsync(string id, UpdateRouteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(request);
        var fb = new FormBuilder()
            .Add("priority", request.Priority)
            .Add("description", request.Description)
            .Add("expression", request.Expression);
        foreach (var a in request.Actions)
            fb.Add("action", a);
        var env = await _http.PutFormAsync<RouteSingleEnvelope>($"v3/routes/{PathEscape.Segment(id)}", fb, cancellationToken,
            routeTemplate: "v3/routes/{route_id}").ConfigureAwait(false);
        return env.Route;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _http.DeleteNoResponseAsync($"v3/routes/{PathEscape.Segment(id)}", cancellationToken,
            routeTemplate: "v3/routes/{route_id}");
    }

    public Task<RouteMatchResult> MatchAsync(string recipient, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        var fb = new FormBuilder().Add("recipient", recipient);
        return _http.PostFormAsync<RouteMatchResult>("v3/routes/match", fb, cancellationToken,
            routeTemplate: "v3/routes/match");
    }
}
