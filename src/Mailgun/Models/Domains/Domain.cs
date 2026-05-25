using System.Text.Json.Serialization;
using Mailgun.Serialization;

namespace Mailgun.Models.Domains;

/// <summary>A Mailgun domain (sender or inbound) as returned by <c>/v4/domains</c>.</summary>
public sealed class Domain
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("smtp_login")] public string? SmtpLogin { get; init; }
    [JsonPropertyName("smtp_password")] public string? SmtpPassword { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("is_disabled")] public bool? IsDisabled { get; init; }

    /// <summary>
    /// Disabled-state envelope when present. Mailgun's wire format here is polymorphic — see
    /// <see cref="PolymorphicDomainDisabledConverter"/>. Active domains return a bare boolean,
    /// disabled ones return the structured object; we normalize the boolean form to <c>null</c>
    /// (use <see cref="IsDisabled"/> for the boolean state).
    /// </summary>
    [JsonPropertyName("disabled")]
    [JsonConverter(typeof(PolymorphicDomainDisabledConverter))]
    public DomainDisabledInfo? Disabled { get; init; }
    [JsonPropertyName("require_tls")] public bool? RequireTls { get; init; }
    [JsonPropertyName("skip_verification")] public bool? SkipVerification { get; init; }
    [JsonPropertyName("spam_action")] public string? SpamAction { get; init; }
    [JsonPropertyName("wildcard")] public bool? Wildcard { get; init; }
    [JsonPropertyName("web_scheme")] public string? WebScheme { get; init; }
    [JsonPropertyName("web_prefix")] public string? WebPrefix { get; init; }
    [JsonPropertyName("use_automatic_sender_security")] public bool? UseAutomaticSenderSecurity { get; init; }
    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>Disabled-domain details.</summary>
public sealed class DomainDisabledInfo
{
    [JsonPropertyName("permanently")] public bool? Permanently { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
    [JsonPropertyName("note")] public string? Note { get; init; }
}
