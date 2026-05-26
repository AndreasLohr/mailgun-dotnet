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
    /// <c>PUT /v1/dkim_management/domains/{name}/rotation</c> — set the auto-rotation policy
    /// (enabled / disabled + cadence). Mailgun requires multipart/form-data with
    /// <c>rotation_enabled=true|false</c> (required) and optional <c>rotation_interval</c>.
    /// </summary>
    /// <param name="domain">The signing domain whose rotation policy is being set.</param>
    /// <param name="rotationEnabled">Whether Mailgun should auto-rotate. Required field on the wire.</param>
    /// <param name="rotationInterval">Optional cadence, e.g. <c>5d</c>, <c>30d</c>. Minimum 5 days.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAutoRotationAsync(string domain, bool rotationEnabled, string? rotationInterval = null, CancellationToken cancellationToken = default);
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
            new FormBuilder(), cancellationToken, routeTemplate: "v1/dkim_management/domains/{domain}/rotate");
    }

    public async Task SetAutoRotationAsync(string domain, bool rotationEnabled, string? rotationInterval = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        // Mailgun's PUT /v1/dkim_management/domains/{name}/rotation requires multipart/form-data with
        // a literal "true"/"false" value for rotation_enabled. The SDK's MultipartBuilder.AddText(bool?)
        // overload emits Mailgun's general convention ("yes"/"no") which this endpoint rejects, so we
        // write the string form explicitly.
        using var mp = new MultipartBuilder()
            .AddText("rotation_enabled", rotationEnabled ? "true" : "false")
            .AddText("rotation_interval", rotationInterval);
        await _http.PutMultipartNoResponseAsync(
            $"v1/dkim_management/domains/{PathEscape.Segment(domain)}/rotation", mp, cancellationToken, routeTemplate: "v1/dkim_management/domains/{domain}/rotation").ConfigureAwait(false);
    }
}
