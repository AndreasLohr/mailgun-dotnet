using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Models.Messages;

namespace Mailgun.Services;

internal sealed class MessagesService : IMessagesService
{
    private readonly MailgunHttpClient _http;

    public MessagesService(MailgunHttpClient http) => _http = http;

    public async Task<SendMessageResponse> SendAsync(string domain, SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.From))
            throw new ArgumentException("SendMessageRequest.From is required.", nameof(request));
        if (request.To.Count == 0 && request.Cc.Count == 0 && request.Bcc.Count == 0)
            throw new ArgumentException("SendMessageRequest requires at least one To/Cc/Bcc recipient.", nameof(request));

        var path = $"v3/{PathEscape.Segment(domain)}/messages";

        if (request.RequiresMultipart)
        {
            // Must be async+await so the `using` scope outlives the HTTP body read. A non-async
            // method returning the Task would dispose the MultipartBuilder before the handler
            // gets to the request body, clearing the multipart parts.
            using var mp = BuildMultipart(request);
            return await _http.PostMultipartAsync<SendMessageResponse>(path, mp, cancellationToken).ConfigureAwait(false);
        }
        return await _http.PostFormAsync<SendMessageResponse>(path, BuildForm(request), cancellationToken).ConfigureAwait(false);
    }

    public async Task<SendMessageResponse> SendMimeAsync(
        string domain,
        IReadOnlyList<string> to,
        byte[] mimeMessage,
        bool? testMode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(mimeMessage);
        if (to.Count == 0)
            throw new ArgumentException("At least one recipient is required.", nameof(to));

        using var mp = new MultipartBuilder();
        foreach (var t in to)
            mp.AddText("to", t);
        if (testMode is not null)
            mp.AddText("o:testmode", testMode);
        mp.AddFile("message", "message.mime", mimeMessage, "message/rfc822");

        var path = $"v3/{PathEscape.Segment(domain)}/messages.mime";
        return await _http.PostMultipartAsync<SendMessageResponse>(path, mp, cancellationToken).ConfigureAwait(false);
    }

    public Task<StoredMessage> GetStoredAsync(string domain, string storageKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        return _http.GetJsonAsync<StoredMessage>(
            $"v3/domains/{PathEscape.Segment(domain)}/messages/{PathEscape.Segment(storageKey)}",
            query: null,
            cancellationToken);
    }

    public Task DeleteStoredAsync(string domain, string storageKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        return _http.DeleteNoResponseAsync(
            $"v3/domains/{PathEscape.Segment(domain)}/messages/{PathEscape.Segment(storageKey)}",
            cancellationToken);
    }

    public Task<SendingQueueStatus> GetSendingQueuesAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.GetJsonAsync<SendingQueueStatus>(
            $"v3/domains/{PathEscape.Segment(domain)}/sending_queues",
            query: null,
            cancellationToken);
    }

    public Task DeleteScheduledEnvelopesAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.DeleteNoResponseAsync(
            $"v3/domains/{PathEscape.Segment(domain)}/envelopes",
            cancellationToken);
    }

    private static FormBuilder BuildForm(SendMessageRequest r)
    {
        var fb = new FormBuilder().Add("from", r.From);
        foreach (var t in r.To) fb.Add("to", t);
        foreach (var c in r.Cc) fb.Add("cc", c);
        foreach (var b in r.Bcc) fb.Add("bcc", b);
        fb.Add("subject", r.Subject)
          .Add("text", r.Text)
          .Add("html", r.Html)
          .Add("amp-html", r.AmpHtml);
        ApplyOptions(fb, r);
        ApplyTemplate(fb, r);
        ApplyHeaders(fb, r);
        return fb;
    }

    private static MultipartBuilder BuildMultipart(SendMessageRequest r)
    {
        var mp = new MultipartBuilder().AddText("from", r.From);
        foreach (var t in r.To) mp.AddText("to", t);
        foreach (var c in r.Cc) mp.AddText("cc", c);
        foreach (var b in r.Bcc) mp.AddText("bcc", b);
        mp.AddText("subject", r.Subject)
          .AddText("text", r.Text)
          .AddText("html", r.Html)
          .AddText("amp-html", r.AmpHtml);

        foreach (var tag in r.Tags) mp.AddText("o:tag", tag);
        foreach (var camp in r.Campaigns) mp.AddText("o:campaign", camp);
        mp.AddText("o:testmode", r.TestMode);
        mp.AddText("o:dkim", r.Dkim);
        mp.AddText("o:deliverytime", r.DeliveryTime);
        mp.AddText("o:deliverytime-optimize-period", r.DeliveryTimeOptimizePeriod);
        mp.AddText("o:time-zone-localize", r.TimeZoneLocalize);
        mp.AddText("o:tracking", r.Tracking);
        mp.AddText("o:tracking-clicks", r.TrackingClicks);
        mp.AddText("o:tracking-opens", r.TrackingOpens);
        mp.AddText("o:require-tls", r.RequireTls);
        mp.AddText("o:skip-verification", r.SkipVerification);
        mp.AddText("o:sending-ip", r.SendingIp);
        mp.AddText("o:sending-ip-pool", r.SendingIpPool);
        mp.AddText("o:tracking-pixel-location-top", r.TrackingPixelLocationTop);
        mp.AddPrefixed("o:", r.AdditionalOptions);

        if (r.Template is not null) mp.AddText("template", r.Template);
        if (r.TemplateVersion is not null) mp.AddText("t:version", r.TemplateVersion);
        if (r.TemplateText is not null) mp.AddText("t:text", r.TemplateText);
        mp.AddPrefixed("v:", r.TemplateVariables);
        if (r.RecipientVariables is not null) mp.AddText("recipient-variables", r.RecipientVariables);

        mp.AddPrefixed("h:", r.CustomHeaders);
        mp.AddPrefixed("v:", r.CustomVariables);

        foreach (var att in r.Attachments)
            mp.AddFile("attachment", att.FileName, att.Content, att.ContentType);
        foreach (var inl in r.Inline)
            mp.AddFile("inline", inl.FileName, inl.Content, inl.ContentType);

        return mp;
    }

    private static void ApplyOptions(FormBuilder fb, SendMessageRequest r)
    {
        foreach (var tag in r.Tags) fb.Add("o:tag", tag);
        foreach (var camp in r.Campaigns) fb.Add("o:campaign", camp);
        fb.Add("o:testmode", r.TestMode);
        fb.Add("o:dkim", r.Dkim);
        fb.Add("o:deliverytime", r.DeliveryTime);
        fb.Add("o:deliverytime-optimize-period", r.DeliveryTimeOptimizePeriod);
        fb.Add("o:time-zone-localize", r.TimeZoneLocalize);
        fb.Add("o:tracking", r.Tracking);
        fb.Add("o:tracking-clicks", r.TrackingClicks);
        fb.Add("o:tracking-opens", r.TrackingOpens);
        fb.Add("o:require-tls", r.RequireTls);
        fb.Add("o:skip-verification", r.SkipVerification);
        fb.Add("o:sending-ip", r.SendingIp);
        fb.Add("o:sending-ip-pool", r.SendingIpPool);
        fb.Add("o:tracking-pixel-location-top", r.TrackingPixelLocationTop);
        fb.AddPrefixed("o:", r.AdditionalOptions);
    }

    private static void ApplyTemplate(FormBuilder fb, SendMessageRequest r)
    {
        if (r.Template is not null) fb.Add("template", r.Template);
        if (r.TemplateVersion is not null) fb.Add("t:version", r.TemplateVersion);
        if (r.TemplateText is not null) fb.Add("t:text", r.TemplateText);
        fb.AddPrefixed("v:", r.TemplateVariables);
        if (r.RecipientVariables is not null) fb.Add("recipient-variables", r.RecipientVariables);
    }

    private static void ApplyHeaders(FormBuilder fb, SendMessageRequest r)
    {
        fb.AddPrefixed("h:", r.CustomHeaders);
        fb.AddPrefixed("v:", r.CustomVariables);
    }
}
