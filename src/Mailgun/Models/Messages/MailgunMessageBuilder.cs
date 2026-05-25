using Mailgun.Services;

namespace Mailgun.Models.Messages;

/// <summary>
/// Fluent builder for <see cref="SendMessageRequest"/>. Each setter mutates the underlying
/// request and returns the same builder instance so calls can be chained. Terminate the chain
/// with <see cref="SendAsync"/> to dispatch or <see cref="Build"/> to extract the request.
/// </summary>
/// <remarks>
/// Not thread-safe. Use one builder per send. Methods are pass-through to
/// <see cref="SendMessageRequest"/> properties and perform no validation — the server is the
/// source of truth and rejects malformed requests with <see cref="Exceptions.MailgunApiException"/>.
/// </remarks>
public sealed class MailgunMessageBuilder
{
    private readonly IMessagesService _messages;
    private readonly SendMessageRequest _req = new();

    internal MailgunMessageBuilder(IMessagesService messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        _messages = messages;
    }

    // ---------- Scalars ----------

    /// <summary>Sets <see cref="SendMessageRequest.From"/>.</summary>
    public MailgunMessageBuilder From(string address)
    {
        _req.From = address;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.Subject"/>.</summary>
    public MailgunMessageBuilder Subject(string subject)
    {
        _req.Subject = subject;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.Text"/>.</summary>
    public MailgunMessageBuilder Text(string text)
    {
        _req.Text = text;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.Html"/>.</summary>
    public MailgunMessageBuilder Html(string html)
    {
        _req.Html = html;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.AmpHtml"/> (the <c>amp-html</c> alternative body).</summary>
    public MailgunMessageBuilder AmpHtml(string ampHtml)
    {
        _req.AmpHtml = ampHtml;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.Template"/>.</summary>
    public MailgunMessageBuilder Template(string templateName)
    {
        _req.Template = templateName;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.TemplateVersion"/>.</summary>
    public MailgunMessageBuilder TemplateVersion(string version)
    {
        _req.TemplateVersion = version;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.TemplateText"/>.</summary>
    public MailgunMessageBuilder TemplateText(bool enabled = true)
    {
        _req.TemplateText = enabled;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.RecipientVariables"/> (raw JSON string).</summary>
    public MailgunMessageBuilder RecipientVariables(string json)
    {
        _req.RecipientVariables = json;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.TestMode"/>. Defaults to <c>true</c> for one-liner <c>.TestMode()</c>.</summary>
    public MailgunMessageBuilder TestMode(bool enabled = true)
    {
        _req.TestMode = enabled;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.Dkim"/>.</summary>
    public MailgunMessageBuilder Dkim(bool enabled = true)
    {
        _req.Dkim = enabled;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.DeliveryTime"/>.</summary>
    public MailgunMessageBuilder DeliverAt(DateTimeOffset when)
    {
        _req.DeliveryTime = when;
        return this;
    }

    /// <summary>Alias for <see cref="DeliverAt"/>; matches the <see cref="SendMessageRequest.DeliveryTime"/> property name 1:1.</summary>
    public MailgunMessageBuilder DeliveryTime(DateTimeOffset when) => DeliverAt(when);

    /// <summary>Sets <see cref="SendMessageRequest.DeliveryTimeOptimizePeriod"/>, e.g. <c>"24h"</c>.</summary>
    public MailgunMessageBuilder DeliveryTimeOptimizePeriod(string period)
    {
        _req.DeliveryTimeOptimizePeriod = period;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.TimeZoneLocalize"/>.</summary>
    public MailgunMessageBuilder TimeZoneLocalize(string period)
    {
        _req.TimeZoneLocalize = period;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.Tracking"/> (<c>true</c>/<c>false</c>/<c>htmlonly</c>).</summary>
    public MailgunMessageBuilder Tracking(string mode)
    {
        _req.Tracking = mode;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.TrackingClicks"/> (<c>true</c>/<c>false</c>/<c>htmlonly</c>).</summary>
    public MailgunMessageBuilder TrackingClicks(string mode)
    {
        _req.TrackingClicks = mode;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.TrackingOpens"/>.</summary>
    public MailgunMessageBuilder TrackingOpens(bool enabled = true)
    {
        _req.TrackingOpens = enabled;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.RequireTls"/>.</summary>
    public MailgunMessageBuilder RequireTls(bool enabled = true)
    {
        _req.RequireTls = enabled;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.SkipVerification"/>.</summary>
    public MailgunMessageBuilder SkipVerification(bool enabled = true)
    {
        _req.SkipVerification = enabled;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.SendingIp"/>.</summary>
    public MailgunMessageBuilder SendingIp(string ip)
    {
        _req.SendingIp = ip;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.SendingIpPool"/>.</summary>
    public MailgunMessageBuilder SendingIpPool(string pool)
    {
        _req.SendingIpPool = pool;
        return this;
    }

    /// <summary>Sets <see cref="SendMessageRequest.TrackingPixelLocationTop"/>.</summary>
    public MailgunMessageBuilder TrackingPixelLocationTop(bool enabled = true)
    {
        _req.TrackingPixelLocationTop = enabled;
        return this;
    }

    // ---------- Collections ----------

    /// <summary>Adds one or more recipients to <see cref="SendMessageRequest.To"/>.</summary>
    public MailgunMessageBuilder To(params string[] addresses) => AddRange(_req.To, addresses);

    /// <summary>Adds one or more recipients to <see cref="SendMessageRequest.Cc"/>.</summary>
    public MailgunMessageBuilder Cc(params string[] addresses) => AddRange(_req.Cc, addresses);

    /// <summary>Adds one or more recipients to <see cref="SendMessageRequest.Bcc"/>.</summary>
    public MailgunMessageBuilder Bcc(params string[] addresses) => AddRange(_req.Bcc, addresses);

    /// <summary>Adds one or more tags to <see cref="SendMessageRequest.Tags"/>.</summary>
    public MailgunMessageBuilder Tag(params string[] tags) => AddRange(_req.Tags, tags);

    /// <summary>Adds one or more campaign ids to <see cref="SendMessageRequest.Campaigns"/>.</summary>
    public MailgunMessageBuilder Campaign(params string[] campaignIds) => AddRange(_req.Campaigns, campaignIds);

    /// <summary>Adds an attachment.</summary>
    public MailgunMessageBuilder Attach(MessageAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _req.Attachments.Add(attachment);
        return this;
    }

    /// <summary>Adds an attachment from raw bytes.</summary>
    public MailgunMessageBuilder Attach(string fileName, byte[] content, string? contentType = null) =>
        Attach(new MessageAttachment(fileName, content, contentType));

    /// <summary>Adds an inline asset (HTML references it via <c>cid:&lt;FileName&gt;</c>).</summary>
    public MailgunMessageBuilder Inline(MessageAttachment asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        _req.Inline.Add(asset);
        return this;
    }

    /// <summary>Adds an inline asset from raw bytes.</summary>
    public MailgunMessageBuilder Inline(string fileName, byte[] content, string? contentType = null) =>
        Inline(new MessageAttachment(fileName, content, contentType));

    // ---------- Dictionaries ----------

    /// <summary>Sets one entry in <see cref="SendMessageRequest.TemplateVariables"/>. Overwrites on duplicate key.</summary>
    public MailgunMessageBuilder TemplateVariable(string name, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _req.TemplateVariables[name] = value;
        return this;
    }

    /// <summary>Sets one entry in <see cref="SendMessageRequest.CustomHeaders"/>. Overwrites on duplicate key.</summary>
    public MailgunMessageBuilder Header(string name, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _req.CustomHeaders[name] = value;
        return this;
    }

    /// <summary>Alias for <see cref="Header"/>; matches the <see cref="SendMessageRequest.CustomHeaders"/> property name 1:1.</summary>
    public MailgunMessageBuilder CustomHeader(string name, string value) => Header(name, value);

    /// <summary>Sets one entry in <see cref="SendMessageRequest.CustomVariables"/>. Overwrites on duplicate key.</summary>
    public MailgunMessageBuilder CustomVariable(string name, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _req.CustomVariables[name] = value;
        return this;
    }

    /// <summary>Sets one entry in <see cref="SendMessageRequest.AdditionalOptions"/> (raw Mailgun <c>o:</c> passthrough). Overwrites on duplicate key.</summary>
    public MailgunMessageBuilder Option(string name, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _req.AdditionalOptions[name] = value;
        return this;
    }

    // ---------- Terminals ----------

    /// <summary>Returns the underlying <see cref="SendMessageRequest"/>. Subsequent builder calls continue to mutate it.</summary>
    public SendMessageRequest Build() => _req;

    /// <summary>Dispatches the built request via <see cref="IMessagesService.SendAsync"/>.</summary>
    public Task<SendMessageResponse> SendAsync(string domain, CancellationToken cancellationToken = default) =>
        _messages.SendAsync(domain, _req, cancellationToken);

    // ---------- Helpers ----------

    private MailgunMessageBuilder AddRange(List<string> target, string[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            target.Add(item);
        }
        return this;
    }
}

/// <summary>
/// Fluent-builder entry points for <see cref="IMessagesService"/>.
/// </summary>
public static class MailgunMessageBuilderExtensions
{
    /// <summary>
    /// Starts a fluent <see cref="MailgunMessageBuilder"/> chain.
    /// Terminate with <c>SendAsync(domain)</c> to dispatch or <c>Build()</c> to extract the request.
    /// </summary>
    public static MailgunMessageBuilder NewMessage(this IMessagesService messages) => new(messages);
}
