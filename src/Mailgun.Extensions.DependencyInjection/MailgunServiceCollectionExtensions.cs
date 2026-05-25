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
    /// Uses <see cref="OptionsConfigurationServiceCollectionExtensions.Configure{TOptions}(IServiceCollection, IConfiguration)"/>,
    /// so reload-on-change is honored when the configuration provider supports it (e.g. <c>appsettings.json</c>
    /// with <c>reloadOnChange: true</c>).
    /// </summary>
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
                var opts = sp.GetRequiredService<IOptions<MailgunClientOptions>>().Value;
                http.BaseAddress = new Uri(opts.ResolveBaseUrl().TrimEnd('/') + "/");
                http.Timeout = opts.Timeout;
            })
            .AddHttpMessageHandler<RateLimitHandler>();

        services.TryAddSingleton<MailgunClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<MailgunClientOptions>>().Value;
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(HttpClientName);

            // Build the inner client by COPYING from the configured options, then overriding the
            // HttpClient with the named factory client. Every field on MailgunClientOptions must
            // be copied — most importantly OnResponse, which an earlier version of this extension
            // silently dropped (the callback was registered via configureOptions but never reached
            // MailgunHttpClient.ctor because this projection didn't propagate it).
            var clientOptions = new MailgunClientOptions
            {
                ApiKey = opts.ApiKey,
                Region = opts.Region,
                BaseUrl = opts.BaseUrl,
                Timeout = opts.Timeout,
                HttpClient = httpClient,
                MaxRetries = opts.MaxRetries,
                UserAgent = opts.UserAgent,
                OnBehalfOf = opts.OnBehalfOf,
                OnResponse = opts.OnResponse,
            };
            return new MailgunClient(clientOptions);
        });

        services.TryAddSingleton<IMailgunClient>(sp => sp.GetRequiredService<MailgunClient>());

        return builder;
    }
}
