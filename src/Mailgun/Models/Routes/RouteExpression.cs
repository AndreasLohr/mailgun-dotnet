using System.Text;

namespace Mailgun.Models.Routes;

/// <summary>
/// Typed representation of a Mailgun route filter expression
/// (<see href="https://documentation.mailgun.com/docs/mailgun/api-reference/openapi-final/tag/Routes/">Mailgun Routes</see>).
/// Build leaf matchers with the static factories (<see cref="MatchRecipient"/>, <see cref="MatchHeader"/>,
/// <see cref="CatchAll"/>) and combine them with <see cref="And"/>, <see cref="Or"/>, <see cref="Not"/>.
/// Call <see cref="Render"/> to produce the string that ships to Mailgun.
/// </summary>
public abstract class RouteExpression
{
    /// <summary>Renders this expression to its Mailgun DSL form.</summary>
    public abstract string Render();

    /// <summary><c>match_recipient("pattern")</c> — matches an SMTP recipient against the supplied regex.</summary>
    public static RouteExpression MatchRecipient(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return new MatchRecipientExpression(pattern);
    }

    /// <summary><c>match_header("header", "pattern")</c> — matches a MIME header value against the supplied regex.</summary>
    public static RouteExpression MatchHeader(string headerName, string pattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(headerName);
        ArgumentNullException.ThrowIfNull(pattern);
        return new MatchHeaderExpression(headerName, pattern);
    }

    /// <summary><c>catch_all()</c> — matches any recipient. Use as a fallback route at the lowest priority.</summary>
    public static RouteExpression CatchAll() => CatchAllExpression.Instance;

    /// <summary>Logical AND of two or more child expressions.</summary>
    public static RouteExpression And(params RouteExpression[] children) => Combine("and", children);

    /// <summary>Logical OR of two or more child expressions.</summary>
    public static RouteExpression Or(params RouteExpression[] children) => Combine("or", children);

    /// <summary>Logical NOT of a single child expression.</summary>
    public static RouteExpression Not(RouteExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new UnaryExpression("not", expression);
    }

    /// <summary>Escape hatch for an expression Mailgun supports that the typed API does not yet wrap.</summary>
    public static RouteExpression Raw(string expression)
    {
        ArgumentException.ThrowIfNullOrEmpty(expression);
        return new RawExpression(expression);
    }

    internal static string EscapeQuoted(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            if (c == '\\' || c == '"') sb.Append('\\');
            sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }

    // Return type is the concrete NaryExpression (not the abstract RouteExpression) so the .NET 8
    // SDK's CA1859 analyzer is satisfied — even though the public factory methods (And/Or) erase to
    // RouteExpression at their callers' call sites.
    private static NaryExpression Combine(string op, RouteExpression[] children)
    {
        ArgumentNullException.ThrowIfNull(children);
        if (children.Length < 2)
            throw new ArgumentException($"'{op}' requires at least two child expressions.", nameof(children));
        foreach (var c in children) ArgumentNullException.ThrowIfNull(c);
        return new NaryExpression(op, children);
    }

    private sealed class MatchRecipientExpression(string pattern) : RouteExpression
    {
        public override string Render() => $"match_recipient({EscapeQuoted(pattern)})";
    }

    private sealed class MatchHeaderExpression(string headerName, string pattern) : RouteExpression
    {
        public override string Render() => $"match_header({EscapeQuoted(headerName)}, {EscapeQuoted(pattern)})";
    }

    private sealed class CatchAllExpression : RouteExpression
    {
        public static readonly CatchAllExpression Instance = new();
        public override string Render() => "catch_all()";
    }

    private sealed class NaryExpression(string op, RouteExpression[] children) : RouteExpression
    {
        public override string Render() => $"{op}({string.Join(", ", children.Select(c => c.Render()))})";
    }

    private sealed class UnaryExpression(string op, RouteExpression child) : RouteExpression
    {
        public override string Render() => $"{op}({child.Render()})";
    }

    private sealed class RawExpression(string raw) : RouteExpression
    {
        public override string Render() => raw;
    }
}
