namespace Mailgun.Pagination;

/// <summary>
/// A single page of items from a Mailgun endpoint that uses the
/// <c>paging.{first,next,previous,last}</c> URL-based pagination style
/// (suppressions, routes, mailing lists, domains, templates, IP pools).
/// </summary>
public sealed class SkipLimitPage<T>
{
    /// <summary>Items on this page.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>URL of the first page, if returned.</summary>
    public string? FirstUrl { get; }

    /// <summary>URL of the previous page, if returned.</summary>
    public string? PreviousUrl { get; }

    /// <summary>URL of the next page, if returned.</summary>
    public string? NextUrl { get; }

    /// <summary>URL of the last page, if returned.</summary>
    public string? LastUrl { get; }

    /// <summary>Total number of items across all pages, if returned by the server.</summary>
    public long? TotalCount { get; }

    /// <summary>True when this page is non-empty and Mailgun supplies a distinct <see cref="NextUrl"/>.</summary>
    public bool HasMore => Items.Count > 0 && !string.IsNullOrEmpty(NextUrl);

    /// <summary>Initializes a new page.</summary>
    public SkipLimitPage(
        IReadOnlyList<T> items,
        string? firstUrl,
        string? previousUrl,
        string? nextUrl,
        string? lastUrl,
        long? totalCount)
    {
        Items = items;
        FirstUrl = firstUrl;
        PreviousUrl = previousUrl;
        NextUrl = nextUrl;
        LastUrl = lastUrl;
        TotalCount = totalCount;
    }
}
