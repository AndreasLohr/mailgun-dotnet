using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mailgun.Webhooks.DistributedCache;

/// <summary>
/// DI helpers for registering <see cref="DistributedWebhookTokenCache"/> as the application's
/// <see cref="IWebhookTokenCache"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DistributedWebhookTokenCache"/> as a singleton <see cref="IWebhookTokenCache"/>.
    /// Requires that <c>IDistributedCache</c> is already registered in DI — typically via
    /// <c>services.AddStackExchangeRedisCache(...)</c>, <c>services.AddDistributedSqlServerCache(...)</c>,
    /// or any other documented <c>AddDistributed*Cache(...)</c> extension. <c>AddDistributedMemoryCache()</c>
    /// gives you a single-process fallback that's API-compatible but not actually distributed —
    /// useful for tests but no different from <see cref="InMemoryWebhookTokenCache"/> in production.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="keyPrefix">
    /// Cache-key namespace. Override only when you share one <c>IDistributedCache</c> across multiple
    /// services and want to avoid key collisions. Defaults to <c>mailgun-webhook-token:</c>.
    /// </param>
    public static IServiceCollection AddMailgunWebhookDistributedTokenCache(
        this IServiceCollection services,
        string keyPrefix = "mailgun-webhook-token:")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(keyPrefix);

        services.TryAddSingleton<IWebhookTokenCache>(sp =>
            new DistributedWebhookTokenCache(
                sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(),
                keyPrefix));
        return services;
    }
}
