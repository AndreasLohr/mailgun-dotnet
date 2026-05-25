using System.Runtime.CompilerServices;

namespace Mailgun.Pagination;

/// <summary>
/// Async enumerator over all pages of a Mailgun list endpoint. Use <c>await foreach</c> for item
/// iteration, or <see cref="AsPages"/> for page-at-a-time iteration. Lazy: never fetches more pages
/// than the consumer iterates.
/// </summary>
public sealed class AsyncPageable<T> : IAsyncEnumerable<T>
{
    private readonly Func<string?, CancellationToken, Task<SkipLimitPage<T>>> _fetchPage;

    internal AsyncPageable(Func<string?, CancellationToken, Task<SkipLimitPage<T>>> fetchPage)
    {
        _fetchPage = fetchPage;
    }

    /// <inheritdoc />
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var page in AsPages(cancellationToken).ConfigureAwait(false))
        {
            foreach (var item in page.Items)
            {
                yield return item;
            }
        }
    }

    /// <summary>Iterates one page at a time, exposing page metadata.</summary>
    public async IAsyncEnumerable<SkipLimitPage<T>> AsPages(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? nextUrl = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await _fetchPage(nextUrl, cancellationToken).ConfigureAwait(false);
            yield return page;
            if (!page.HasMore || string.IsNullOrEmpty(page.NextUrl))
            {
                yield break;
            }
            nextUrl = page.NextUrl;
        }
    }
}
