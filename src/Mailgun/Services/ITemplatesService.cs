using Mailgun.Models.Templates;
using Mailgun.Pagination;

namespace Mailgun.Services;

/// <summary>Operations on <c>/v4/templates</c> (account-level templates + versions).</summary>
public interface ITemplatesService
{
    Task<SkipLimitPage<Template>> ListAsync(int? limit = null, int? skip = null, CancellationToken cancellationToken = default);
    AsyncPageable<Template> ListAllAsync(int? limit = null);
    /// <summary><c>GET /v4/templates/{name}</c> — get a template (optionally with its active version body).</summary>
    Task<Template> GetAsync(string name, bool? active = null, CancellationToken cancellationToken = default);
    Task<Template> CreateAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default);
    /// <summary><c>PUT /v4/templates/{name}</c> — update the description.</summary>
    Task<Template> UpdateAsync(string name, string description, CancellationToken cancellationToken = default);
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
    /// <summary><c>POST /v4/templates/{name}/copy/{targetName}</c>.</summary>
    Task<Template> CopyAsync(string name, string targetName, CancellationToken cancellationToken = default);
    /// <summary><c>PUT /v4/templates/{name}/rename</c>.</summary>
    Task<Template> RenameAsync(string name, string newName, CancellationToken cancellationToken = default);

    Task<SkipLimitPage<TemplateVersion>> ListVersionsAsync(string name, int? limit = null, int? skip = null, CancellationToken cancellationToken = default);
    Task<TemplateVersion> GetVersionAsync(string name, string tag, CancellationToken cancellationToken = default);
    Task<Template> CreateVersionAsync(string name, CreateTemplateVersionRequest request, CancellationToken cancellationToken = default);
    Task<TemplateVersion> UpdateVersionAsync(string name, string tag, UpdateTemplateVersionRequest request, CancellationToken cancellationToken = default);
    Task DeleteVersionAsync(string name, string tag, CancellationToken cancellationToken = default);
}
