using Mailgun;
using Mailgun.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Mailgun.Extensions.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for wiring <see cref="IMailgunClient"/>
/// using <see cref="IHttpClientFactory"/>.
/// </summary>
public static class MailgunServiceCollectionExtensions
{
    /// <summary>The name of the named <see cref="HttpClient"/> registered for Mailgun API requests.</summary>
    public const string HttpClientName = "Mailgun";

    /// <summary>The default configuration section name bound by <see cref="AddMailgun(IServiceCollection, IConfiguration)"/>.</summary>
    public const string DefaultConfigSectionName = "Mailgun";

    /// <summary>
    /// Registers <see cref="IMailgunClient"/> with options bound from the supplied <see cref="IConfigurationSection"/>.
    /// </summary>
    /// <remarks>
    /// The resolved <see cref="IMailgunClient"/> is a singleton and reads its options snapshot once at
    /// construction. Mutations to the underlying configuration source after the first resolve (e.g.
    /// rotating the API key in <c>appsettings.json</c>) do NOT propagate to the running client — restart
    /// the process or rebuild the service provider for rotation to take effect.
    /// </remarks>
    public static IHttpClientBuilder AddMailgun(
        this IServiceCollection services,
        IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);
        services.Configure<MailgunClientOptions>(section);
        return services.AddMailgun(configureOptions: null);
    }

    /// <summary>
    /// Registers <see cref="IMailgunClient"/> with options bound from the <c>"Mailgun"</c> section of the
    /// supplied <see cref="IConfiguration"/>. Shorthand for
    /// <c>services.AddMailgun(configuration.GetSection(MailgunServiceCollectionExtensions.DefaultConfigSectionName))</c>.
    /// </summary>
    public static IHttpClientBuilder AddMailgun(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddMailgun(configuration.GetSection(DefaultConfigSectionName));
    }

    /// <summary>
    /// Registers <see cref="IMailgunClient"/> as a singleton, configured via the supplied options action.
    /// The underlying <see cref="HttpClient"/> is managed by <see cref="IHttpClientFactory"/>; a
    /// <see cref="RateLimitHandler"/> is wired into its pipeline so retries on 429 and idempotent
    /// 5xx work identically to the SDK-owned <see cref="HttpClient"/> path.
    /// </summary>
    /// <returns>The <see cref="IHttpClientBuilder"/> for the registered HttpClient — chain additional handlers if needed.</returns>
    public static IHttpClientBuilder AddMailgun(
        this IServiceCollection services,
        Action<MailgunClientOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }
        services.AddOptions<MailgunClientOptions>().Validate(
            o => !string.IsNullOrWhiteSpace(o.ApiKey),
            "MailgunClientOptions.ApiKey is required.");

        // Register the rate-limit handler so the named HttpClient pipeline can use it. This is
        // the piece that was missing previously: without it, every request from a DI-resolved
        // MailgunClient went through the IHttpClientFactory client untouched by RateLimitHandler,
        // silently bypassing MaxRetries (the SDK's own retry path lives in MailgunHttpClient.ctor's
        // owned-HttpClient branch, which DI doesn't take).
        services.TryAddTransient<RateLimitHandler>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<MailgunClientOptions>>().Value;
            return new RateLimitHandler(opts.MaxRetries);
        });

        var builder = services.AddHttpClient(HttpClientName, (sp, http) =>
            {
                // BaseAddress is intentionally not set: MailgunHttpClient.BuildUri composes absolute
                // URIs from its own resolved base URL. Setting HttpClient.BaseAddress here would be
                // dead config, and worse, would diverge from the SDK's source of truth if a caller
                // ever updated one but not the other.
                var opts = sp.GetRequiredService<IOptions<MailgunClientOptions>>().Value;
                http.Timeout = opts.Timeout;
            })
            .AddHttpMessageHandler<RateLimitHandler>();

        services.TryAddSingleton<MailgunClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<MailgunClientOptions>>().Value;
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(HttpClientName);

            // Clone ALL configured options and override only the transport with the named factory
            // client. This used to be a hand-maintained field-by-field projection that silently
            // dropped options as they were added (OnResponse, then AllowInsecureBaseUrl and
            // MaxResponseContentBytes). CloneWithHttpClient uses MemberwiseClone, so no option can
            // ever be forgotten here again. See OptionsParityTests for the guard.
            return new MailgunClient(opts.CloneWithHttpClient(httpClient));
        });

        services.TryAddSingleton<IMailgunClient>(sp => sp.GetRequiredService<MailgunClient>());

        return builder;
    }
}
