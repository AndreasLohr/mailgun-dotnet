using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mailgun.Tests.Telemetry;

/// <summary>
/// Static (compile-time) cardinality guardrail for the metrics surface. Every
/// <c>_http.&lt;X&gt;(...)</c> call across all <c>src/Mailgun/Services/*.cs</c> files must pass a
/// <c>routeTemplate:</c> argument, and that argument MUST be a plain string literal — never an
/// interpolated string and never an identifier.
/// <para>
/// Why this is a static check and not a runtime test: a regression like
/// <c>routeTemplate: $"v3/{domain}/messages"</c> compiles cleanly and passes all runtime tests
/// (because tests usually run with one fake domain). It only surfaces in production as a silent
/// cardinality explosion — one histogram series per distinct domain — which then degrades
/// metrics-backend storage and queries. Catching it at PR review is the only durable defense, and
/// scanning the syntax tree with Roslyn is that defense.
/// </para>
/// </summary>
public class RouteTemplateLiteralTests
{
    [Fact]
    public void Every_http_call_passes_a_string_literal_routeTemplate()
    {
        var servicesDir = LocateServicesDirectory();
        var serviceFiles = Directory.EnumerateFiles(servicesDir, "*.cs", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(serviceFiles);

        var violations = new List<string>();
        var totalHttpCalls = 0;

        foreach (var path in serviceFiles)
        {
            var source = File.ReadAllText(path);
            var tree = CSharpSyntaxTree.ParseText(source, path: path);
            var root = tree.GetCompilationUnitRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                // Only care about `_http.<Anything>(...)` invocations.
                if (invocation.Expression is not MemberAccessExpressionSyntax member) continue;
                if (member.Expression is not IdentifierNameSyntax id) continue;
                if (id.Identifier.Text != "_http") continue;

                totalHttpCalls++;

                // Find the named argument `routeTemplate:`. The recipe enforces named-arg form, so
                // a callsite missing the name entirely is itself a violation.
                var arg = invocation.ArgumentList.Arguments.FirstOrDefault(
                    a => a.NameColon?.Name.Identifier.Text == "routeTemplate");

                if (arg is null)
                {
                    violations.Add($"{Relative(path)}:{LineOf(invocation)} — _http.{member.Name.Identifier.Text}(...) is missing the routeTemplate: named argument.");
                    continue;
                }

                // The argument expression must be a string literal — not an interpolated string,
                // not an identifier, not a member access, not a concatenation. A single literal
                // string is the only shape that guarantees low-cardinality on the metric tag.
                if (arg.Expression is not LiteralExpressionSyntax literal
                    || !literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    var actualShape = arg.Expression.GetType().Name;
                    violations.Add(
                        $"{Relative(path)}:{LineOf(arg)} — _http.{member.Name.Identifier.Text}(...) " +
                        $"passes a non-literal routeTemplate ({actualShape}: `{arg.Expression}`). " +
                        $"Use a string literal with placeholder syntax like \"v3/{{domain}}/messages\" — " +
                        $"interpolating a runtime value would explode metric cardinality.");
                }
            }
        }

        // Sanity check: if the scan finds zero call sites, the directory locator is broken and the
        // assertion above (NotEmpty serviceFiles) wouldn't catch it. Pin the count to a known floor.
        Assert.True(totalHttpCalls >= 200,
            $"Expected at least 200 _http.X(...) callsites under src/Mailgun/Services/, found {totalHttpCalls}. " +
            "The Roslyn scanner is probably looking at the wrong directory.");

        Assert.True(violations.Count == 0,
            $"Found {violations.Count} routeTemplate violation(s):{Environment.NewLine}  " +
            string.Join(Environment.NewLine + "  ", violations));
    }

    private static int LineOf(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static string Relative(string absolutePath)
    {
        var idx = absolutePath.IndexOf("src", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? absolutePath[idx..].Replace('\\', '/') : absolutePath;
    }

    /// <summary>
    /// Walks up from this source file's directory at compile time (via <see cref="CallerFilePathAttribute"/>)
    /// to find <c>src/Mailgun/Services</c>. This avoids depending on the test runner's working
    /// directory, which varies between <c>dotnet test</c> invocations and IDE test discovery.
    /// </summary>
    private static string LocateServicesDirectory([CallerFilePath] string callerPath = "")
    {
        // callerPath at compile time is the absolute path of THIS source file inside the repo.
        // Walk up until we find a `src` sibling, then descend into Mailgun/Services.
        var dir = new DirectoryInfo(Path.GetDirectoryName(callerPath)!);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Mailgun", "Services");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate src/Mailgun/Services from caller path '{callerPath}'.");
    }
}
