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
    /// <summary>
    /// <c>DELETE /v4/templates</c> — delete every account-level template in one call. There is no
    /// confirmation step and no undo on the wire; if you need a safety net, snapshot the list first.
    /// </summary>
    Task DeleteAllAsync(CancellationToken cancellationToken = default);
    /// <summary><c>POST /v4/templates/{name}/copy/{targetName}</c>.</summary>
    Task<Template> CopyAsync(string name, string targetName, CancellationToken cancellationToken = default);
    /// <summary><c>PUT /v4/templates/{name}/rename</c>.</summary>
    Task<Template> RenameAsync(string name, string newName, CancellationToken cancellationToken = default);

    Task<SkipLimitPage<TemplateVersion>> ListVersionsAsync(string name, int? limit = null, int? skip = null, CancellationToken cancellationToken = default);
    Task<TemplateVersion> GetVersionAsync(string name, string tag, CancellationToken cancellationToken = default);
    Task<Template> CreateVersionAsync(string name, CreateTemplateVersionRequest request, CancellationToken cancellationToken = default);
    Task<TemplateVersion> UpdateVersionAsync(string name, string tag, UpdateTemplateVersionRequest request, CancellationToken cancellationToken = default);
    Task DeleteVersionAsync(string name, string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v4/templates/{name}/copy</c> — batch-copy a template to multiple targets in one
    /// call. <paramref name="targets"/> is the destination-template-name list; an optional
    /// <paramref name="sourceVersions"/> restricts which versions are copied (omit to copy all).
    /// </summary>
    Task<TemplateBatchCopyResponse> BatchCopyAsync(
        string name,
        IReadOnlyList<TemplateCopyRequest> targets,
        IReadOnlyList<string>? sourceVersions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v4/templates/{name}/rename/{newName}</c> — alternate rename shape where the new
    /// name is a path segment (distinct from <see cref="RenameAsync"/> which passes the new name
    /// in the form body).
    /// </summary>
    Task<Template> RenameByPathAsync(string name, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v4/templates/{name}/versions/{version}/copy/{newVersion}</c> — copy a template
    /// version under a new tag, optionally with a comment describing why.
    /// </summary>
    Task<TemplateVersion> CopyVersionAsync(
        string name,
        string sourceVersion,
        string newVersion,
        string? comment = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single destination in a <c>PUT /v4/templates/{name}/copy</c> batch request. Mailgun's
/// schema expects each entry to name the target template and optionally describe it.
/// </summary>
public sealed class TemplateCopyRequest
{
    /// <summary>Required. The new template name to copy into.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("target_template_name")]
    public string TargetTemplateName { get; set; } = string.Empty;

    /// <summary>Optional description applied to the new template.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>Response from the batch-copy template endpoint.</summary>
public sealed class TemplateBatchCopyResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("message")] public string? Message { get; init; }
    /// <summary>Per-destination failure list — empty means every copy succeeded.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("failed_copies")] public List<object>? FailedCopies { get; init; }
    [System.Text.Json.Serialization.JsonPropertyName("error")] public string? Error { get; init; }
}
