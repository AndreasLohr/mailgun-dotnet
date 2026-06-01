using Mailgun.Http;

namespace Mailgun;

/// <summary>
/// Configuration options for <see cref="MailgunClient"/>.
/// </summary>
public sealed class MailgunClientOptions
{
    /// <summary>Base URL for <see cref="MailgunRegion.Us"/>.</summary>
    public const string UsBaseUrl = "https://api.mailgun.net";

    /// <summary>Base URL for <see cref="MailgunRegion.Eu"/>.</summary>
    public const string EuBaseUrl = "https://api.eu.mailgun.net";

    /// <summary>
    /// The Mailgun API key. May be an account/primary API key or a domain-scoped sending key
    /// (sending keys only authorize <c>POST /v3/{domain}/messages</c> and <c>.mime</c>).
    /// Required.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Mailgun deployment region. Defaults to <see cref="MailgunRegion.Us"/>.
    /// Ignored when <see cref="BaseUrl"/> is explicitly set.
    /// </summary>
    public MailgunRegion Region { get; set; } = MailgunRegion.Us;

    /// <summary>
    /// Optional explicit base URL override. When set, takes precedence over <see cref="Region"/>.
    /// Trailing slash is normalized. Useful for testing or for self-hosted gateways.
    /// </summary>
    /// <remarks>
    /// Must use HTTPS unless the host is loopback (<c>localhost</c> / <c>127.0.0.1</c> / <c>::1</c>)
    /// or <see cref="AllowInsecureBaseUrl"/> is set — otherwise the SDK throws at construction time
    /// because the Basic-auth API key would be transmitted in cleartext.
    /// </remarks>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Opt-in escape hatch that permits a non-HTTPS, non-loopback <see cref="BaseUrl"/>. Set this to
    /// <c>true</c> only for a trusted self-hosted gateway you fully control on a private network —
    /// otherwise the account API key would be sent over plaintext HTTP. Loopback hosts are always
    /// allowed without this flag. Defaults to <c>false</c>.
    /// </summary>
    public bool AllowInsecureBaseUrl { get; set; }

    /// <summary>
    /// Hard cap, in bytes, on the size of an API response body the SDK will buffer into memory.
    /// Guards against a compromised or MITM'd endpoint streaming an oversized body to exhaust memory.
    /// Mailgun's real responses are small JSON payloads; the 64 MiB default leaves generous headroom.
    /// Enforced whether or not the SDK owns the <see cref="HttpClient"/>. Must be positive.
    /// </summary>
    public long MaxResponseContentBytes { get; set; } = 64L * 1024 * 1024;

    /// <summary>
    /// HTTP request timeout. Defaults to 100 seconds. Ignored when <see cref="HttpClient"/> is supplied.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Optional caller-owned <see cref="System.Net.Http.HttpClient"/>. When supplied the SDK does not
    /// dispose it. Otherwise the SDK constructs and owns an internal <see cref="System.Net.Http.HttpClient"/>.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>
    /// Maximum number of retries for transient failures (HTTP 429 and idempotent 5xx). Defaults to 3.
    /// Honored only when the SDK constructs its own <see cref="System.Net.Http.HttpClient"/>.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Optional additional fragment appended to the <c>User-Agent</c> header (e.g. <c>"myapp/1.0"</c>).
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Optional subaccount id. When set, every request includes the
    /// <c>X-Mailgun-On-Behalf-Of</c> header so the account-level API key acts on behalf of the named subaccount.
    /// Usually set indirectly via <see cref="MailgunClient.ForSubaccount(string)"/>.
    /// </summary>
    public string? OnBehalfOf { get; set; }

    /// <summary>
    /// Optional callback invoked after every API call (success or failure) with the parsed
    /// response metadata — status code, request id, rate-limit headers.
    /// </summary>
    /// <remarks>
    /// Use this in concurrent scenarios where <see cref="MailgunClient.LastResponseMetadata"/>
    /// is unsafe: that property is a single field overwritten on every request, so parallel
    /// callers race to read it. A callback runs synchronously on the caller's async flow, so
    /// each caller can route the metadata into their own per-request storage (e.g. an
    /// <see cref="System.Threading.AsyncLocal{T}"/> they own, or a logging scope).
    /// </remarks>
    public Action<MailgunResponseMetadata>? OnResponse { get; set; }

    /// <summary>Resolves the effective base URL: <see cref="BaseUrl"/> if set, otherwise the URL for <see cref="Region"/>.</summary>
    public string ResolveBaseUrl() =>
        !string.IsNullOrWhiteSpace(BaseUrl)
            ? BaseUrl
            : Region == MailgunRegion.Eu ? EuBaseUrl : UsBaseUrl;
}
