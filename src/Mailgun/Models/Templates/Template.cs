using System.Text.Json.Serialization;
using Mailgun.Pagination;
using Mailgun.Serialization;

namespace Mailgun.Models.Templates;

/// <summary>A Mailgun template (<c>/v4/templates</c>).</summary>
public sealed class Template
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("createdAt")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("createdBy")] public string? CreatedBy { get; init; }
    [JsonPropertyName("version")] public TemplateVersion? Version { get; init; }
    [JsonPropertyName("versions")] public List<TemplateVersion>? Versions { get; init; }
}

/// <summary>A single template version.</summary>
public sealed class TemplateVersion
{
    [JsonPropertyName("tag")] public string Tag { get; init; } = string.Empty;
    [JsonPropertyName("template")] public string? TemplateBody { get; init; }
    [JsonPropertyName("engine")] public string? Engine { get; init; }
    [JsonPropertyName("comment")] public string? Comment { get; init; }
    [JsonPropertyName("active")] public bool? Active { get; init; }
    [JsonPropertyName("mjml")] public string? Mjml { get; init; }
    [JsonPropertyName("headers")] public Dictionary<string, string>? Headers { get; init; }
    [JsonPropertyName("createdAt")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>Parameters for <c>POST /v4/templates</c>.</summary>
public sealed class CreateTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Optional initial active version body.</summary>
    public string? Template { get; set; }
    /// <summary>Optional version tag for the initial version (default <c>initial</c>).</summary>
    public string? Tag { get; set; }
    /// <summary>Template engine — <c>handlebars</c> (default).</summary>
    public string? Engine { get; set; }
    public string? Comment { get; set; }
    public string? Mjml { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}

/// <summary>Parameters for <c>POST /v4/templates/{name}/versions</c>.</summary>
public sealed class CreateTemplateVersionRequest
{
    public string Tag { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string? Engine { get; set; }
    public string? Comment { get; set; }
    public bool? Active { get; set; }
    public string? Mjml { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}

/// <summary>Parameters for <c>PUT /v4/templates/{name}/versions/{tag}</c>.</summary>
public sealed class UpdateTemplateVersionRequest
{
    public string? Template { get; set; }
    public string? Comment { get; set; }
    public bool? Active { get; set; }
    public string? Mjml { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}

/// <summary>Wrapper around a single template returned by Mailgun.</summary>
public sealed class TemplateResponse
{
    [JsonPropertyName("template")] public Template Template { get; init; } = new();
    [JsonPropertyName("message")] public string? Message { get; init; }
}

internal sealed class TemplateListEnvelope
{
    [JsonPropertyName("items")] public List<Template>? Items { get; set; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; set; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; set; }
}

internal sealed class TemplateVersionListEnvelope
{
    [JsonPropertyName("template")] public Template? Template { get; set; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; set; }
}
