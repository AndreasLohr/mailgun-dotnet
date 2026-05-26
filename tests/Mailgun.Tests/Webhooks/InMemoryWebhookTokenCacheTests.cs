using Mailgun.Webhooks;

namespace Mailgun.Tests.Webhooks;

/// <summary>
/// Coverage for the in-process replay cache, including verification that its async wrapper (the
/// default-interface <c>MarkSeenAsync</c>) really does delegate to <c>MarkSeen</c>.
/// </summary>
public class InMemoryWebhookTokenCacheTests
{
    [Fact]
    public void First_call_returns_true_second_returns_false()
    {
        var cache = new InMemoryWebhookTokenCache();

        Assert.True(cache.MarkSeen("tok-1", TimeSpan.FromMinutes(15)));
        Assert.False(cache.MarkSeen("tok-1", TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Distinct_tokens_each_return_true()
    {
        var cache = new InMemoryWebhookTokenCache();

        Assert.True(cache.MarkSeen("tok-a", TimeSpan.FromMinutes(15)));
        Assert.True(cache.MarkSeen("tok-b", TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Expired_entries_are_swept_and_re_admitted()
    {
        var cache = new InMemoryWebhookTokenCache();

        Assert.True(cache.MarkSeen("tok-x", TimeSpan.FromMilliseconds(1)));
        // Wait past the TTL so the next call's sweep removes the prior entry. Mailgun's clock-skew
        // window is minutes in practice, so this 50ms sleep is purely test-timing — well within
        // the slowest CI variance.
        Thread.Sleep(TimeSpan.FromMilliseconds(50));
        Assert.True(cache.MarkSeen("tok-x", TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void MarkSeen_rejects_blank_or_null_token()
    {
        var cache = new InMemoryWebhookTokenCache();

        Assert.Throws<ArgumentException>(() => cache.MarkSeen("", TimeSpan.FromMinutes(15)));
        Assert.Throws<ArgumentException>(() => cache.MarkSeen("   ", TimeSpan.FromMinutes(15)));
        Assert.Throws<ArgumentNullException>(() => cache.MarkSeen(null!, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public async Task Default_interface_MarkSeenAsync_delegates_to_sync_MarkSeen()
    {
        // Existing in-process implementations don't override the new async overload — they
        // inherit the default interface method that wraps MarkSeen in a completed ValueTask.
        // Verify the wrapper preserves the underlying behavior end-to-end.
        IWebhookTokenCache cache = new InMemoryWebhookTokenCache();

        Assert.True(await cache.MarkSeenAsync("tok-async", TimeSpan.FromMinutes(15)));
        Assert.False(await cache.MarkSeenAsync("tok-async", TimeSpan.FromMinutes(15)));
    }
}
