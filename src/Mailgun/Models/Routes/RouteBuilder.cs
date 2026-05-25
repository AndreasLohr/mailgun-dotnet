using Mailgun.Services;

namespace Mailgun.Models.Routes;

/// <summary>
/// Fluent builder for <see cref="CreateRouteRequest"/>. Surfaces Mailgun's route DSL
/// (<c>match_recipient</c>, <c>match_header</c>, <c>catch_all</c>, <c>forward</c>, <c>store</c>,
/// <c>stop</c>) as discoverable methods so callers don't have to construct DSL strings by hand.
/// </summary>
/// <remarks>
/// Not thread-safe. Use one builder per route. The expression and action methods perform string
/// escaping for the Mailgun DSL, but otherwise do not validate — the server is the source of truth.
/// </remarks>
public sealed class RouteBuilder
{
    private readonly IRoutesService _routes;
    private readonly CreateRouteRequest _req = new();
    private RouteExpression? _expression;

    internal RouteBuilder(IRoutesService routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        _routes = routes;
    }

    // ---------- Scalars ----------

    /// <summary>Sets <see cref="CreateRouteRequest.Priority"/>. Lower values run first; defaults to 0 if unset.</summary>
    public RouteBuilder Priority(int priority)
    {
        _req.Priority = priority;
        return this;
    }

    /// <summary>Sets <see cref="CreateRouteRequest.Description"/>.</summary>
    public RouteBuilder Description(string description)
    {
        _req.Description = description;
        return this;
    }

    // ---------- Expression (overwrites on repeat call) ----------

    /// <summary>Sugar for <c>match_recipient("pattern")</c>. Overwrites any previously-set expression.</summary>
    public RouteBuilder MatchRecipient(string pattern) => Match(RouteExpression.MatchRecipient(pattern));

    /// <summary>Sugar for <c>match_header("header", "pattern")</c>. Overwrites any previously-set expression.</summary>
    public RouteBuilder MatchHeader(string headerName, string pattern) =>
        Match(RouteExpression.MatchHeader(headerName, pattern));

    /// <summary>Sugar for <c>catch_all()</c>. Overwrites any previously-set expression.</summary>
    public RouteBuilder CatchAll() => Match(RouteExpression.CatchAll());

    /// <summary>Sets an arbitrary <see cref="RouteExpression"/> (for AND/OR/NOT trees). Overwrites any previously-set expression.</summary>
    public RouteBuilder Match(RouteExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        _expression = expression;
        return this;
    }

    // ---------- Actions (append on every call) ----------

    /// <summary>Appends a <c>forward("urlOrEmail")</c> action.</summary>
    public RouteBuilder Forward(string urlOrEmail)
    {
        ArgumentException.ThrowIfNullOrEmpty(urlOrEmail);
        _req.Actions.Add($"forward({RouteExpression.EscapeQuoted(urlOrEmail)})");
        return this;
    }

    /// <summary>Appends a <c>store()</c> action, or <c>store(notify="url")</c> when <paramref name="notifyUrl"/> is supplied.</summary>
    public RouteBuilder Store(string? notifyUrl = null)
    {
        _req.Actions.Add(string.IsNullOrEmpty(notifyUrl)
            ? "store()"
            : $"store(notify={RouteExpression.EscapeQuoted(notifyUrl)})");
        return this;
    }

    /// <summary>Appends a <c>stop()</c> action — short-circuits remaining routes once this one matches.</summary>
    public RouteBuilder Stop()
    {
        _req.Actions.Add("stop()");
        return this;
    }

    /// <summary>Escape hatch: appends a raw action string (e.g. <c>"forward(\"x\", \"y\")"</c>) unchanged.</summary>
    public RouteBuilder Action(string raw)
    {
        ArgumentException.ThrowIfNullOrEmpty(raw);
        _req.Actions.Add(raw);
        return this;
    }

    // ---------- Terminals ----------

    /// <summary>
    /// Returns the underlying <see cref="CreateRouteRequest"/>. Each call re-materializes the current
    /// expression into <see cref="CreateRouteRequest.Expression"/>; subsequent builder calls continue
    /// to mutate the same instance.
    /// </summary>
    public CreateRouteRequest Build()
    {
        _req.Expression = _expression?.Render() ?? string.Empty;
        return _req;
    }

    /// <summary>Dispatches the built request via <see cref="IRoutesService.CreateAsync"/>.</summary>
    public Task<Route> CreateAsync(CancellationToken cancellationToken = default) =>
        _routes.CreateAsync(Build(), cancellationToken);

    /// <summary>Dispatches the built request as a <see cref="UpdateRouteRequest"/> via <see cref="IRoutesService.UpdateAsync"/>.</summary>
    public Task<Route> UpdateAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        var update = new UpdateRouteRequest
        {
            Priority = _req.Priority,
            Description = _req.Description,
            Expression = _expression?.Render(),
        };
        foreach (var a in _req.Actions) update.Actions.Add(a);
        return _routes.UpdateAsync(id, update, cancellationToken);
    }
}

/// <summary>
/// Fluent-builder entry points for <see cref="IRoutesService"/>.
/// </summary>
public static class RouteBuilderExtensions
{
    /// <summary>
    /// Starts a fluent <see cref="RouteBuilder"/> chain.
    /// Terminate with <c>CreateAsync()</c> or <c>UpdateAsync(id)</c>; or <c>Build()</c> to extract the request.
    /// </summary>
    public static RouteBuilder NewRoute(this IRoutesService routes) => new(routes);
}
