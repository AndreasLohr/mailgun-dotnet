using System.Collections.Concurrent;

namespace Mailgun.Webhooks;

/// <summary>
/// Anti-replay token cache. Mailgun rotates the per-webhook <c>token</c> on every request; storing
/// recently-seen tokens prevents a same-payload replay attack within the validity window.
/// </summary>
public interface IWebhookTokenCache
{
    /// <summary>
    /// Atomically: return <c>false</c> if the token was already seen (replay), otherwise record it
    /// for the next <paramref name="ttl"/> and return <c>true</c>.
    /// </summary>
    bool MarkSeen(string token, TimeSpan ttl);

    /// <summary>
    /// Async counterpart of <see cref="MarkSeen"/>. The endpoint helper calls this overload so
    /// that distributed implementations (Redis, SQL, Cosmos) can do I/O without blocking the
    /// request thread on <c>.GetAwaiter().GetResult()</c>. Default implementation delegates to the
    /// synchronous overload so existing in-process implementations (e.g.
    /// <see cref="InMemoryWebhookTokenCache"/>) keep working unchanged — distributed adapters
    /// override this and leave <see cref="MarkSeen"/> as a not-supported throw.
    /// </summary>
    ValueTask<bool> MarkSeenAsync(string token, TimeSpan ttl, CancellationToken cancellationToken = default) =>
        new(MarkSeen(token, ttl));
}

/// <summary>
/// In-memory <see cref="IWebhookTokenCache"/> suitable for single-process deployments.
/// Distributed deployments should plug in a Redis-backed implementation.
/// </summary>
/// <remarks>
/// Each <see cref="MarkSeen"/> call sweeps expired entries synchronously (O(n) in the current
/// cache size, on the request path). This is intentional — Mailgun's webhook volume is small
/// (typically a few per second per domain) and the per-request overhead is dominated by the
/// HMAC verification itself. If you expect sustained high throughput, swap to a TTL-aware
/// distributed cache (Redis EXPIRE, MemoryCache with absolute expiry) and skip the per-call sweep.
/// </remarks>
public sealed class InMemoryWebhookTokenCache : IWebhookTokenCache
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new();

    /// <inheritdoc />
    public bool MarkSeen(string token, TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        Sweep();
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        return _seen.TryAdd(token, expiresAt);
    }

    private void Sweep()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _seen)
        {
            if (kv.Value < now)
            {
                _seen.TryRemove(kv);
            }
        }
    }
}
