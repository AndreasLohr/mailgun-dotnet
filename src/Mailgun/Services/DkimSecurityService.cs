using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;

namespace Mailgun.Services;

/// <summary>
/// Operations on <c>/v1/dkim_management/...</c> — manual rotation and the auto-rotation policy
/// for a domain's DKIM key. Separate from <see cref="IDkimKeysService"/> because Mailgun
/// gates these behind a different RBAC scope (sender-security).
/// </summary>
public interface IDkimSecurityService
{
    /// <summary>
    /// <c>PUT /v1/dkim_management/{domain}/rotate-dkim-key</c> — manually rotate the DKIM key
    /// for a domain. Use sparingly; Mailgun rate-limits rotation operations.
    /// </summary>
    Task RotateAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v1/dkim_management/{domain}/auto-rotation</c> — current auto-rotation policy.
    /// </summary>
    Task<DkimAutoRotationPolicy> GetAutoRotationAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PUT /v1/dkim_management/{domain}/auto-rotation</c> — set the auto-rotation policy
    /// (enabled / disabled + cadence).
    /// </summary>
    Task<DkimAutoRotationPolicy> SetAutoRotationAsync(string domain, DkimAutoRotationPolicy policy, CancellationToken cancellationToken = default);
}

/// <summary>The auto-rotation policy attached to a domain's DKIM key.</summary>
public sealed class DkimAutoRotationPolicy
{
    /// <summary>True when Mailgun should automatically rotate the key on the configured cadence.</summary>
    [JsonPropertyName("enabled")] public bool? Enabled { get; set; }

    /// <summary>Rotation cadence — Mailgun-documented values include <c>90d</c>, <c>180d</c>, <c>1y</c>.</summary>
    [JsonPropertyName("rotation_interval")] public string? RotationInterval { get; set; }

    /// <summary>DKIM key size in bits to use on the next rotation (1024 or 2048).</summary>
    [JsonPropertyName("bits")] public int? Bits { get; set; }
}

internal sealed class DkimSecurityService : IDkimSecurityService
{
    private readonly MailgunHttpClient _http;
    public DkimSecurityService(MailgunHttpClient http) => _http = http;

    public Task RotateAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.PutFormNoResponseAsync(
            $"v1/dkim_management/{PathEscape.Segment(domain)}/rotate-dkim-key",
            new FormBuilder(), cancellationToken);
    }

    public Task<DkimAutoRotationPolicy> GetAutoRotationAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return _http.GetJsonAsync<DkimAutoRotationPolicy>(
            $"v1/dkim_management/{PathEscape.Segment(domain)}/auto-rotation", null, cancellationToken);
    }

    public Task<DkimAutoRotationPolicy> SetAutoRotationAsync(string domain, DkimAutoRotationPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(policy);
        return _http.PutJsonBodyAsync<DkimAutoRotationPolicy>(
            $"v1/dkim_management/{PathEscape.Segment(domain)}/auto-rotation",
            policy, cancellationToken);
    }
}
