namespace Mailgun.Models.Domains;

/// <summary>Parameters for <c>POST /v4/domains</c>.</summary>
public sealed class CreateDomainRequest
{
    /// <summary>Fully-qualified domain name (e.g. <c>mg.example.com</c>). Required.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>SMTP authentication password (4-32 chars). Optional; Mailgun generates one if omitted.</summary>
    public string? SmtpPassword { get; set; }

    /// <summary>Spam action: <c>disabled</c> | <c>block</c> | <c>tag</c>.</summary>
    public string? SpamAction { get; set; }

    /// <summary>Wildcard domain (<c>true</c> = handle all subdomains).</summary>
    public bool? Wildcard { get; set; }

    /// <summary>Require TLS on outbound sends.</summary>
    public bool? ForceDkimAuthority { get; set; }

    /// <summary>DKIM key size (1024 or 2048).</summary>
    public int? DkimKeySize { get; set; }

    /// <summary>IPs to assign to this domain (comma-separated when supplied directly).</summary>
    public List<string> Ips { get; } = new();

    /// <summary>Pool id to assign this domain to.</summary>
    public string? PoolId { get; set; }

    /// <summary>Web scheme: <c>http</c> | <c>https</c>.</summary>
    public string? WebScheme { get; set; }

    /// <summary>Use automatic sender security (BIMI, MTA-STS, DMARC).</summary>
    public bool? UseAutomaticSenderSecurity { get; set; }
}
