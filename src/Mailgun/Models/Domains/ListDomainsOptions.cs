namespace Mailgun.Models.Domains;

/// <summary>Query parameters for <c>GET /v4/domains</c>.</summary>
public sealed class ListDomainsOptions
{
    /// <summary>Page size (max 1000).</summary>
    public int? Limit { get; set; }

    /// <summary>Number of items to skip.</summary>
    public int? Skip { get; set; }

    /// <summary>Optional search-by-name (substring).</summary>
    public string? Filter { get; set; }

    /// <summary>Optional state filter (<c>active</c> | <c>unverified</c> | <c>disabled</c>).</summary>
    public string? State { get; set; }
}
