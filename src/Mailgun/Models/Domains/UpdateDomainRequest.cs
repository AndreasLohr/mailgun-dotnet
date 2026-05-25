namespace Mailgun.Models.Domains;

/// <summary>Parameters for <c>PUT /v4/domains/{name}</c>.</summary>
public sealed class UpdateDomainRequest
{
    /// <summary>Spam action: <c>disabled</c> | <c>block</c> | <c>tag</c>.</summary>
    public string? SpamAction { get; set; }

    /// <summary>Wildcard domain.</summary>
    public bool? Wildcard { get; set; }

    /// <summary>Web scheme: <c>http</c> | <c>https</c>.</summary>
    public string? WebScheme { get; set; }

    /// <summary>Use automatic sender security.</summary>
    public bool? UseAutomaticSenderSecurity { get; set; }
}
