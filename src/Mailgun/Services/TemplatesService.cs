using System.Text.Json;
using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Models.Templates;
using Mailgun.Pagination;
using Mailgun.Serialization;

namespace Mailgun.Services;

internal sealed class TemplatesService : ITemplatesService
{
    private readonly MailgunHttpClient _http;
    public TemplatesService(MailgunHttpClient http) => _http = http;

    public Task<SkipLimitPage<Template>> ListAsync(int? limit = null, int? skip = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder().Add("limit", limit).Add("skip", skip).Build();
        return _http.GetSkipLimitPageAsync<Template, TemplateListEnvelope>(
            "v4/templates", q, null, e => e.Items, e => e.Paging, e => e.TotalCount, cancellationToken);
    }

    public AsyncPageable<Template> ListAllAsync(int? limit = null)
    {
        var q = new QueryBuilder().Add("limit", limit).Build();
        return _http.GetSkipLimitPageable<Template, TemplateListEnvelope>(
            "v4/templates", q, e => e.Items, e => e.Paging, e => e.TotalCount);
    }

    public async Task<Template> GetAsync(string name, bool? active = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var q = new QueryBuilder().Add("active", active).Build();
        var env = await _http.GetJsonAsync<TemplateResponse>($"v4/templates/{PathEscape.Segment(name)}", q, cancellationToken).ConfigureAwait(false);
        return env.Template;
    }

    public async Task<Template> CreateAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.", nameof(request));
        var fb = new FormBuilder()
            .Add("name", request.Name)
            .Add("description", request.Description)
            .Add("template", request.Template)
            .Add("tag", request.Tag)
            .Add("engine", request.Engine)
            .Add("comment", request.Comment)
            .Add("mjml", request.Mjml);
        if (request.Headers is not null)
            fb.Add("headers", JsonSerializer.Serialize(request.Headers, MailgunJsonOptions.Default));
        var env = await _http.PostFormAsync<TemplateResponse>("v4/templates", fb, cancellationToken).ConfigureAwait(false);
        return env.Template;
    }

    public async Task<Template> UpdateAsync(string name, string description, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        var fb = new FormBuilder().Add("description", description);
        var env = await _http.PutFormAsync<TemplateResponse>($"v4/templates/{PathEscape.Segment(name)}", fb, cancellationToken).ConfigureAwait(false);
        return env.Template;
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _http.DeleteNoResponseAsync($"v4/templates/{PathEscape.Segment(name)}", cancellationToken);
    }

    public async Task<Template> CopyAsync(string name, string targetName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        var env = await _http.PostFormAsync<TemplateResponse>(
            $"v4/templates/{PathEscape.Segment(name)}/copy/{PathEscape.Segment(targetName)}",
            new FormBuilder(), cancellationToken).ConfigureAwait(false);
        return env.Template;
    }

    public async Task<Template> RenameAsync(string name, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        var fb = new FormBuilder().Add("name", newName);
        var env = await _http.PutFormAsync<TemplateResponse>(
            $"v4/templates/{PathEscape.Segment(name)}/rename", fb, cancellationToken).ConfigureAwait(false);
        return env.Template;
    }

    public async Task<SkipLimitPage<TemplateVersion>> ListVersionsAsync(string name, int? limit = null, int? skip = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var q = new QueryBuilder().Add("limit", limit).Add("skip", skip).Build();
        var env = await _http.GetJsonAsync<TemplateVersionListEnvelope>(
            $"v4/templates/{PathEscape.Segment(name)}/versions", q, cancellationToken).ConfigureAwait(false);
        var items = env.Template?.Versions ?? new List<TemplateVersion>();
        return new SkipLimitPage<TemplateVersion>(items, env.Paging?.First, env.Paging?.Previous, env.Paging?.Next, env.Paging?.Last, totalCount: null);
    }

    public async Task<TemplateVersion> GetVersionAsync(string name, string tag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        var env = await _http.GetJsonAsync<TemplateResponse>(
            $"v4/templates/{PathEscape.Segment(name)}/versions/{PathEscape.Segment(tag)}", null, cancellationToken).ConfigureAwait(false);
        return env.Template.Version ?? throw new InvalidOperationException("Mailgun did not return a version object.");
    }

    public async Task<Template> CreateVersionAsync(string name, CreateTemplateVersionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Tag);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Template);
        var fb = new FormBuilder()
            .Add("tag", request.Tag)
            .Add("template", request.Template)
            .Add("engine", request.Engine)
            .Add("comment", request.Comment)
            .Add("active", request.Active)
            .Add("mjml", request.Mjml);
        if (request.Headers is not null)
            fb.Add("headers", JsonSerializer.Serialize(request.Headers, MailgunJsonOptions.Default));
        var env = await _http.PostFormAsync<TemplateResponse>(
            $"v4/templates/{PathEscape.Segment(name)}/versions", fb, cancellationToken).ConfigureAwait(false);
        return env.Template;
    }

    public async Task<TemplateVersion> UpdateVersionAsync(string name, string tag, UpdateTemplateVersionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(request);
        var fb = new FormBuilder()
            .Add("template", request.Template)
            .Add("comment", request.Comment)
            .Add("active", request.Active)
            .Add("mjml", request.Mjml);
        if (request.Headers is not null)
            fb.Add("headers", JsonSerializer.Serialize(request.Headers, MailgunJsonOptions.Default));
        var env = await _http.PutFormAsync<TemplateResponse>(
            $"v4/templates/{PathEscape.Segment(name)}/versions/{PathEscape.Segment(tag)}", fb, cancellationToken).ConfigureAwait(false);
        return env.Template.Version ?? throw new InvalidOperationException("Mailgun did not return a version object.");
    }

    public Task DeleteVersionAsync(string name, string tag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return _http.DeleteNoResponseAsync(
            $"v4/templates/{PathEscape.Segment(name)}/versions/{PathEscape.Segment(tag)}", cancellationToken);
    }
}
