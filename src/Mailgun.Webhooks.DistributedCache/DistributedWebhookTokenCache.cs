using Microsoft.Extensions.Caching.Distributed;

namespace Mailgun.Webhooks.DistributedCache;

/// <summary>
/// <see cref="IWebhookTokenCache"/> backed by <see cref="IDistributedCache"/>. Use this when the
/// webhook receiver runs in more than one process / pod / instance — the in-process
/// <see cref="InMemoryWebhookTokenCache"/> degrades silently in those topologies because each
/// instance's <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/> sees
/// only its own traffic, so a replayed token routed to a different instance is accepted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Atomicity caveat.</b> <see cref="IDistributedCache"/> deliberately does not expose
/// set-if-not-exists semantics — it has separate <c>GetAsync</c> and <c>SetAsync</c> primitives.
/// So this adapter implements replay-check as get-then-set, which contains a small race window:
/// two concurrent webhooks carrying the SAME token can both observe "absent" on <c>GetAsync</c>
/// before either <c>SetAsync</c> commits, and both will be treated as fresh.
/// </para>
/// <para>
/// For Mailgun webhook replay protection this trade-off is acceptable:
/// (1) Mailgun's tokens are large random strings — collisions are vanishingly unlikely,
/// (2) replay attacks are typically not concurrent (the attacker captures and re-sends later),
/// (3) the second replay onward is reliably blocked once the first <c>SetAsync</c> lands.
/// Stripe's official webhook samples accept this same trade-off.
/// </para>
/// <para>
/// Callers needing strict atomic check-and-set should implement <see cref="IWebhookTokenCache"/>
/// directly against their store's primitive (e.g. StackExchange.Redis's
/// <c>StringSetAsync(..., when: When.NotExists)</c>) — this adapter is the right answer for the
/// 95% case where <c>IDistributedCache</c> is already in DI.
/// </para>
/// </remarks>
public sealed class DistributedWebhookTokenCache : IWebhookTokenCache
{
    private const string DefaultKeyPrefix = "mailgun-webhook-token:";

    private readonly IDistributedCache _cache;
    private readonly string _keyPrefix;

    // A single shared sentinel byte avoids per-call byte[] allocation. The cached VALUE is
    // semantically a presence-flag — we only care whether GetAsync returns null or non-null.
    private static readonly byte[] PresenceMarker = { 1 };

    /// <summary>
    /// Create the adapter. <paramref name="keyPrefix"/> defaults to <c>mailgun-webhook-token:</c>
    /// — override only when you're sharing one <see cref="IDistributedCache"/> across multiple
    /// services and want a different namespace.
    /// </summary>
    public DistributedWebhookTokenCache(IDistributedCache cache, string keyPrefix = DefaultKeyPrefix)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentException.ThrowIfNullOrEmpty(keyPrefix);
        _cache = cache;
        _keyPrefix = keyPrefix;
    }

    /// <summary>
    /// Synchronous overload is not supported on this adapter — <see cref="IDistributedCache"/> is
    /// async-only, and a sync-over-async wrapper would risk thread-pool starvation under load.
    /// The endpoint helper calls <see cref="MarkSeenAsync"/> instead.
    /// </summary>
    public bool MarkSeen(string token, TimeSpan ttl) =>
        throw new NotSupportedException(
            $"{nameof(DistributedWebhookTokenCache)} is async-only — call {nameof(MarkSeenAsync)} instead. " +
            "The endpoint helper does this automatically.");

    /// <inheritdoc />
    public async ValueTask<bool> MarkSeenAsync(string token, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var key = _keyPrefix + token;
        // Step 1: is this token already in the cache?
        var existing = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return false;

        // Step 2: mark it seen. AbsoluteExpirationRelativeToNow lets the store evict the entry
        // automatically once the replay window closes — same TTL semantics as the in-memory cache.
        await _cache.SetAsync(
            key,
            PresenceMarker,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            cancellationToken).ConfigureAwait(false);
        return true;
    }
}
