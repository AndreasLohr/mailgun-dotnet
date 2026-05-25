namespace Mailgun.Models.Messages;

/// <summary>
/// Parameters for <c>POST /v3/{domain}/messages</c>. Maps directly to Mailgun's documented
/// message-send form fields. Setting any of <see cref="Attachments"/>, <see cref="Inline"/>,
/// or supplying multi-line headers triggers <c>multipart/form-data</c> encoding automatically.
/// </summary>
public sealed class SendMessageRequest
{
    /// <summary>Email address of the sender (required). Example: <c>"Excited User &lt;mailgun@example.com&gt;"</c>.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>Recipients (one or more required). RFC-822 addresses; display names allowed.</summary>
    public List<string> To { get; } = new();

    /// <summary>Cc recipients.</summary>
    public List<string> Cc { get; } = new();

    /// <summary>Bcc recipients.</summary>
    public List<string> Bcc { get; } = new();

    /// <summary>Message subject.</summary>
    public string? Subject { get; set; }

    /// <summary>Plain-text body.</summary>
    public string? Text { get; set; }

    /// <summary>HTML body.</summary>
    public string? Html { get; set; }

    /// <summary>
    /// AMP-HTML body (Mailgun form field <c>amp-html</c>). Clients that support AMP for Email
    /// render this alternative instead of <see cref="Html"/>; clients that don't fall back to
    /// the regular HTML/text bodies.
    /// </summary>
    public string? AmpHtml { get; set; }

    /// <summary>Tag(s) to attach to the message (Mailgun <c>o:tag</c>). Multiple allowed.</summary>
    public List<string> Tags { get; } = new();

    /// <summary>Mailgun campaign ids (<c>o:campaign</c>).</summary>
    public List<string> Campaigns { get; } = new();

    /// <summary>Mailgun template name (uses <c>template</c> form field; pair with <see cref="TemplateVariables"/>).</summary>
    public string? Template { get; set; }

    /// <summary>Template version (Mailgun <c>t:version</c>).</summary>
    public string? TemplateVersion { get; set; }

    /// <summary>Whether to render the template body in test mode (Mailgun <c>t:text</c>).</summary>
    public bool? TemplateText { get; set; }

    /// <summary>Template variables sent as <c>v:my-var</c> form fields. Values are typically JSON-encoded strings.</summary>
    public Dictionary<string, string> TemplateVariables { get; } = new();

    /// <summary>Per-recipient variables sent as <c>recipient-variables</c> (JSON-encoded by caller or via SDK helper).</summary>
    public string? RecipientVariables { get; set; }

    /// <summary>Custom MIME headers (Mailgun <c>h:Header-Name</c>).</summary>
    public Dictionary<string, string> CustomHeaders { get; } = new();

    /// <summary>Custom variables (Mailgun <c>v:variable-name</c>); separate from template variables — these reach event payloads.</summary>
    public Dictionary<string, string> CustomVariables { get; } = new();

    /// <summary>Sets <c>o:testmode</c> = yes; messages are accepted but never delivered.</summary>
    public bool? TestMode { get; set; }

    /// <summary>Sets <c>o:dkim</c> (true/false).</summary>
    public bool? Dkim { get; set; }

    /// <summary>Scheduled delivery time (Mailgun <c>o:deliverytime</c>).</summary>
    public DateTimeOffset? DeliveryTime { get; set; }

    /// <summary>Send-Time Optimization (STO) period — Mailgun <c>o:deliverytime-optimize-period</c>, e.g. <c>"24h"</c>.</summary>
    public string? DeliveryTimeOptimizePeriod { get; set; }

    /// <summary>Time-Zone Optimization period — Mailgun <c>o:time-zone-localize</c>.</summary>
    public string? TimeZoneLocalize { get; set; }

    /// <summary>Sets <c>o:tracking</c> (true/false/htmlonly).</summary>
    public string? Tracking { get; set; }

    /// <summary>Sets <c>o:tracking-clicks</c> (true/false/htmlonly).</summary>
    public string? TrackingClicks { get; set; }

    /// <summary>Sets <c>o:tracking-opens</c> (true/false).</summary>
    public bool? TrackingOpens { get; set; }

    /// <summary>Sets <c>o:require-tls</c>.</summary>
    public bool? RequireTls { get; set; }

    /// <summary>Sets <c>o:skip-verification</c>.</summary>
    public bool? SkipVerification { get; set; }

    /// <summary>Sets <c>o:sending-ip</c>.</summary>
    public string? SendingIp { get; set; }

    /// <summary>Sets <c>o:sending-ip-pool</c>.</summary>
    public string? SendingIpPool { get; set; }

    /// <summary>Sets <c>o:tracking-pixel-location-top</c>.</summary>
    public bool? TrackingPixelLocationTop { get; set; }

    /// <summary>Free-form additional Mailgun <c>o:</c> options not covered by typed properties above.</summary>
    public Dictionary<string, string> AdditionalOptions { get; } = new();

    /// <summary>File attachments. Setting this triggers <c>multipart/form-data</c> encoding.</summary>
    public List<MessageAttachment> Attachments { get; } = new();

    /// <summary>Inline assets (HTML can reference these via <c>cid:&lt;FileName&gt;</c>).</summary>
    public List<MessageAttachment> Inline { get; } = new();

    /// <summary>True when this request requires <c>multipart/form-data</c> encoding.</summary>
    public bool RequiresMultipart => Attachments.Count > 0 || Inline.Count > 0;
}
