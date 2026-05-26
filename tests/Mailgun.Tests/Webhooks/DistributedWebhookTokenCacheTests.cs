using Mailgun.Webhooks;
using Mailgun.Webhooks.DistributedCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Mailgun.Tests.Webhooks;

/// <summary>
/// Covers the <see cref="DistributedWebhookTokenCache"/> adapter against a real
/// <see cref="IDistributedCache"/> (the in-process <see cref="MemoryDistributedCache"/>, which
/// implements the same contract used by Redis / SQL Server / Cosmos adapters).
/// </summary>
public class DistributedWebhookTokenCacheTests
{
    private static IDistributedCache NewCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    [Fact]
    public async Task First_call_with_token_returns_true_and_writes_with_ttl()
    {
        var backing = NewCache();
        var sut = new DistributedWebhookTokenCache(backing);

        var fresh = await sut.MarkSeenAsync("tok-1", TimeSpan.FromMinutes(15));

        Assert.True(fresh);
        Assert.NotNull(await backing.GetAsync("mailgun-webhook-token:tok-1"));
    }

    [Fact]
    public async Task Second_call_with_same_token_returns_false_within_ttl()
    {
        var sut = new DistributedWebhookTokenCache(NewCache());

        var first = await sut.MarkSeenAsync("tok-replay", TimeSpan.FromMinutes(15));
        var second = await sut.MarkSeenAsync("tok-replay", TimeSpan.FromMinutes(15));

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task Distinct_tokens_each_register_as_fresh()
    {
        var sut = new DistributedWebhookTokenCache(NewCache());

        Assert.True(await sut.MarkSeenAsync("tok-a", TimeSpan.FromMinutes(15)));
        Assert.True(await sut.MarkSeenAsync("tok-b", TimeSpan.FromMinutes(15)));
        Assert.True(await sut.MarkSeenAsync("tok-c", TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public async Task Custom_key_prefix_is_applied_to_underlying_cache_key()
    {
        var backing = NewCache();
        var sut = new DistributedWebhookTokenCache(backing, keyPrefix: "tenant-42:");

        await sut.MarkSeenAsync("tok-x", TimeSpan.FromMinutes(15));

        Assert.NotNull(await backing.GetAsync("tenant-42:tok-x"));
        Assert.Null(await backing.GetAsync("mailgun-webhook-token:tok-x"));
    }

    [Fact]
    public void Sync_MarkSeen_throws_not_supported()
    {
        // Sync-over-async on IDistributedCache is a deadlock / starvation hazard. The adapter
        // intentionally throws so a caller who somehow gets here gets a loud, accurate error
        // rather than a hard-to-diagnose hang.
        var sut = new DistributedWebhookTokenCache(NewCache());

        var ex = Assert.Throws<NotSupportedException>(() => sut.MarkSeen("tok-z", TimeSpan.FromMinutes(15)));
        Assert.Contains("MarkSeenAsync", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Blank_or_null_token_throws_argument_exception()
    {
        var sut = new DistributedWebhookTokenCache(NewCache());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.MarkSeenAsync("", TimeSpan.FromMinutes(15)).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.MarkSeenAsync("   ", TimeSpan.FromMinutes(15)).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.MarkSeenAsync(null!, TimeSpan.FromMinutes(15)).AsTask());
    }

    [Fact]
    public void Constructor_rejects_null_cache()
    {
        Assert.Throws<ArgumentNullException>(() => new DistributedWebhookTokenCache(null!));
    }

    [Fact]
    public void Di_extension_registers_singleton_resolvable_via_IWebhookTokenCache()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddMailgunWebhookDistributedTokenCache();

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IWebhookTokenCache>();

        Assert.IsType<DistributedWebhookTokenCache>(resolved);
    }

    [Fact]
    public async Task Di_resolved_cache_behaves_as_distributed_replay_filter()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddMailgunWebhookDistributedTokenCache();

        using var sp = services.BuildServiceProvider();
        var cache = sp.GetRequiredService<IWebhookTokenCache>();

        Assert.True(await cache.MarkSeenAsync("tok-di", TimeSpan.FromMinutes(15)));
        Assert.False(await cache.MarkSeenAsync("tok-di", TimeSpan.FromMinutes(15)));
    }
}
