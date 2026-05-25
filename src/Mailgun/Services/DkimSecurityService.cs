using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;

namespace Mailgun.Services;

/// <summary>
/// Operations on <c>/v1/dkim_management/domains/{name}/...</c> — manual rotation and the
/// auto-rotation policy for a domain's DKIM key. Separate from <see cref="IDkimKeysService"/>
/// because Mailgun gates these behind a different RBAC scope (sender-security).
/// </summary>
public interface IDkimSecurityService
{
    /// <summary>
    /// <c>POST /v1/dkim_management/domains/{name}/rotate</c> — manually rotate the DKIM key
    /// for a domain. Use sparingly; Mailgun rate-limits rotation operations.
    /// </summary>
    Task RotateAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v1/dkim_management/domains/{name}/rotation</c> — current auto-rotation policy.
    /// </summary>
    Task<DkimAutoRotationPolicy> GetAutoRotationAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v1/dkim_management/domains/{name}/rotation</c> — set the auto-rotation policy
    /// (enabled / disabled + cadence). Mailgun requires multipart/form-data with form fields
    /// <c>rotation_enabled</c> and optional <c>rotation_interval</c>.
    /// </summary>
    Task SetAutoRotationAsync(string domain, DkimAutoRotationPolicy policy, CancellationToken cancellationToken = default);
}

/// <summary>
/// The auto-rotation policy attached to a domain's DKIM key.
/// </summary>
/// <remarks>
/// Mailgun's wire field for the on/off flag is <c>rotation_enabled</c> (not <c>enabled</c>).
/// The DTO uses that as the canonical name so it round-trips through both GET and SET. There is
/// no documented <c>bits</c> field on this endpoint — DKIM key size is set when the key is created
/// via <see cref="IDkimKeysService.CreateForAuthorityAsync"/>, not on the rotation policy.
/// </remarks>
public sealed class DkimAutoRotationPolicy
{
    /// <summary>True when Mailgun should automatically rotate the key on the configured cadence.</summary>
    [JsonPropertyName("rotation_enabled")] public bool? RotationEnabled { get; set; }

    /// <summary>Rotation cadence — minimum allowed interval is <c>5d</c>; common values <c>5d</c>, <c>30d</c>.</summary>
    [JsonPropertyName("rotation_interval")] public string? RotationInterval { get; set; }
}

internal sealed class DkimSecurityService : IDkimSecurityService
{
    private readonly MailgunHttpClient _http;
    public DkimSecurityService(MailgunHttpClient http) => _http = http;

    public Task RotateAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        // POST with no body — Mailgun's documented contract for this endpoint.
        return _http.PostFormNoResponseAsync(
            $"v1/dkim_management/domains/{PathEscape.Segment(domain)}/rotate",
            new FormBuilder(), cancellationToken);
    }

    public Task<DkimAutoRotationPolicy> GetAutoRotationAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.GetJsonAsync<DkimAutoRotationPolicy>(
            $"v1/dkim_management/domains/{PathEscape.Segment(domain)}/rotation", null, cancellationToken);
    }

    public async Task SetAutoRotationAsync(string domain, DkimAutoRotationPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(policy);
        // Mailgun documents the endpoint as PUT + multipart/form-data with rotation_enabled (required)
        // and optional rotation_interval. JSON body is rejected.
        using var mp = new MultipartBuilder()
            .AddText("rotation_enabled", policy.RotationEnabled)
            .AddText("rotation_interval", policy.RotationInterval);
        await _http.PutMultipartNoResponseAsync(
            $"v1/dkim_management/domains/{PathEscape.Segment(domain)}/rotation", mp, cancellationToken).ConfigureAwait(false);
    }
}
