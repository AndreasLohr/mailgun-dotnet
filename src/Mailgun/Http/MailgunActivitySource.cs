using System.Diagnostics;
using System.Reflection;

namespace Mailgun.Http;

/// <summary>
/// Single <see cref="System.Diagnostics.ActivitySource"/> the SDK emits HTTP-request spans on.
/// OpenTelemetry consumers subscribe by name:
/// <code>
/// builder.Services.AddOpenTelemetry().WithTracing(t =&gt; t.AddSource(MailgunActivitySource.Name));
/// </code>
/// </summary>
public static class MailgunActivitySource
{
    /// <summary>The well-known source name. Consumers pass this to <c>AddSource(…)</c>.</summary>
    public const string Name = "Mailgun";

    private static readonly string Version =
        typeof(MailgunActivitySource).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(MailgunActivitySource).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>The shared <see cref="ActivitySource"/> instance.</summary>
    public static readonly ActivitySource Instance = new(Name, Version);
}
