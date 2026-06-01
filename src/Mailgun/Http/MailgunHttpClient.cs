using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Mailgun.Exceptions;
using Mailgun.Internal;
using Mailgun.Pagination;
using Mailgun.Serialization;

namespace Mailgun.Http;

/// <summary>
/// Internal HTTP client wrapper that handles auth, URL construction, JSON ser/de,
/// error mapping, and surfaces response metadata.
/// </summary>
internal sealed class MailgunHttpClient : IDisposable
{
    private static readonly string UserAgentValue = $"Mailgun-DotNet/{typeof(MailgunHttpClient).Assembly.GetName().Version}";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly Uri _baseUrl;
    private readonly string _basicAuthHeaderValue;
    private readonly string? _onBehalfOf;
    private readonly string? _userAgentSuffix;
    private readonly Action<MailgunResponseMetadata>? _onResponse;
    private readonly long _maxResponseContentBytes;

    public MailgunHttpClient(MailgunClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException("MailgunClientOptions.ApiKey is required.", nameof(options));
        }
        if (options.MaxResponseContentBytes <= 0)
        {
            throw new ArgumentException("MailgunClientOptions.MaxResponseContentBytes must be positive.", nameof(options));
        }

        // ResolveBaseUrl always returns a non-blank value because MailgunRegion is a non-nullable
        // enum and the resolver falls through to the Us/Eu defaults when BaseUrl is unset. The
        // earlier "blank ResolveBaseUrl → ArgumentException" guard was dead code with no reachable
        // execution path.
        var resolvedBase = options.ResolveBaseUrl();
        var baseUri = new Uri(resolvedBase.TrimEnd('/') + "/");
        // Refuse to attach the Basic-auth API key over plaintext. ValidatePaginationUrl already
        // enforces HTTPS for server-supplied links; the primary configured endpoint deserves the
        // same guard. Loopback is exempt (local testing has no wire), and AllowInsecureBaseUrl is
        // the explicit opt-out for a trusted self-hosted gateway.
        if (!baseUri.IsLoopback
            && !options.AllowInsecureBaseUrl
            && !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"MailgunClientOptions.BaseUrl must use HTTPS so the API key is not sent in cleartext " +
                $"(resolved scheme was '{baseUri.Scheme}'). Use a loopback host for local testing, or set " +
                "MailgunClientOptions.AllowInsecureBaseUrl = true for a trusted self-hosted gateway.",
                nameof(options));
        }
        _baseUrl = baseUri;
        _onBehalfOf = ValidateOnBehalfOf(options.OnBehalfOf);
        _userAgentSuffix = options.UserAgent;
        _onResponse = options.OnResponse;
        _maxResponseContentBytes = options.MaxResponseContentBytes;
        _basicAuthHeaderValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{options.ApiKey}"));

        if (options.HttpClient is { } provided)
        {
            _httpClient = provided;
            _ownsHttpClient = false;
        }
        else
        {
            // AllowAutoRedirect = false: the Mailgun API never issues 3xx redirects, and following
            // one on an auth-bearing client would forward custom headers (X-Mailgun-On-Behalf-Of) to
            // an attacker-influenced location. The SDK validates pagination links explicitly instead.
            HttpMessageHandler handler = new HttpClientHandler { AllowAutoRedirect = false };
            handler = new RateLimitHandler(options.MaxRetries) { InnerHandler = handler };
            _httpClient = new HttpClient(handler) { Timeout = options.Timeout };
            _ownsHttpClient = true;
        }
    }

    /// <summary>
    /// Creates a derived client that shares this client's HttpClient transport, but rewrites
    /// the <c>X-Mailgun-On-Behalf-Of</c> header to the given subaccount id. Used by
    /// <see cref="MailgunClient.ForSubaccount(string)"/>.
    /// </summary>
    internal MailgunHttpClient ForSubaccount(string subaccountId) =>
        new(this, subaccountId);

    private MailgunHttpClient(MailgunHttpClient parent, string onBehalfOf)
    {
        _httpClient = parent._httpClient;
        _ownsHttpClient = false;
        _baseUrl = parent._baseUrl;
        _userAgentSuffix = parent._userAgentSuffix;
        _basicAuthHeaderValue = parent._basicAuthHeaderValue;
        _onResponse = parent._onResponse;
        _maxResponseContentBytes = parent._maxResponseContentBytes;
        _onBehalfOf = ValidateOnBehalfOf(onBehalfOf);
    }

    /// <summary>
    /// Rejects control characters (CR, LF, NUL, …) in a subaccount id / <c>OnBehalfOf</c> value so they
    /// can never reach the <c>X-Mailgun-On-Behalf-Of</c> header, which is added with
    /// <see cref="System.Net.Http.Headers.HttpHeaders.TryAddWithoutValidation(string, string?)"/>.
    /// This closes a header-injection vector when the id is derived from untrusted input (a realistic
    /// multi-tenant pattern). Modern .NET also rejects such values at send time, but validating at the
    /// boundary fails fast with a clear error instead of an opaque transport exception.
    /// </summary>
    private static string? ValidateOnBehalfOf(string? value)
    {
        if (value is null) return null;
        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                throw new ArgumentException(
                    "Subaccount id (OnBehalfOf) must not contain control characters such as CR or LF.",
                    nameof(value));
            }
        }
        return value;
    }

    /// <summary>
    /// Most recent response metadata captured by this client. <strong>Not safe for concurrent use
    /// and effectively unusable in DI scenarios:</strong> when multiple callers issue requests against
    /// the same <see cref="MailgunClient"/> in parallel (which is the default with the DI-registered
    /// singleton in ASP.NET Core), they race to overwrite this single field — by the time you read
    /// it, another request may have already replaced its contents.
    /// <para>
    /// Prefer <see cref="MailgunClientOptions.OnResponse"/> for capturing per-request metadata: it
    /// fires synchronously on the caller's async flow and lets each request route the metadata into
    /// its own per-request storage (an <see cref="System.Threading.AsyncLocal{T}"/>, a logging scope, etc.).
    /// </para>
    /// </summary>
    public MailgunResponseMetadata? LastResponseMetadata { get; private set; }

    public Task<TResponse> GetJsonAsync<TResponse>(string path, IReadOnlyList<KeyValuePair<string, string?>>? query, CancellationToken ct, string routeTemplate = "") =>
        SendJsonAsync<TResponse>(HttpMethod.Get, path, query, content: null, ct, routeTemplate);

    public Task<TResponse> PostFormAsync<TResponse>(string path, FormBuilder form, CancellationToken ct, string routeTemplate = "") =>
        SendJsonAsync<TResponse>(HttpMethod.Post, path, query: null, content: form.ToContent(), ct, routeTemplate);

    public Task PostFormNoResponseAsync(string path, FormBuilder form, CancellationToken ct, string routeTemplate = "") =>
        SendNoBodyAsync(HttpMethod.Post, path, query: null, content: form.ToContent(), ct, routeTemplate);

    public Task<TResponse> PutFormAsync<TResponse>(string path, FormBuilder form, CancellationToken ct, string routeTemplate = "") =>
        SendJsonAsync<TResponse>(HttpMethod.Put, path, query: null, content: form.ToContent(), ct, routeTemplate);

    public Task PutFormNoResponseAsync(string path, FormBuilder form, CancellationToken ct, string routeTemplate = "") =>
        SendNoBodyAsync(HttpMethod.Put, path, query: null, content: form.ToContent(), ct, routeTemplate);

    public Task<TResponse> PostMultipartAsync<TResponse>(string path, MultipartBuilder mp, CancellationToken ct, string routeTemplate = "") =>
        SendJsonAsync<TResponse>(HttpMethod.Post, path, query: null, content: mp.Build(), ct, routeTemplate);

    public Task PostMultipartNoResponseAsync(string path, MultipartBuilder mp, CancellationToken ct, string routeTemplate = "") =>
        SendNoBodyAsync(HttpMethod.Post, path, query: null, content: mp.Build(), ct, routeTemplate);

    public Task<TResponse> PutMultipartAsync<TResponse>(string path, MultipartBuilder mp, CancellationToken ct, string routeTemplate = "") =>
        SendJsonAsync<TResponse>(HttpMethod.Put, path, query: null, content: mp.Build(), ct, routeTemplate);

    public Task PutMultipartNoResponseAsync(string path, MultipartBuilder mp, CancellationToken ct, string routeTemplate = "") =>
        SendNoBodyAsync(HttpMethod.Put, path, query: null, content: mp.Build(), ct, routeTemplate);

    /// <summary>PATCH with a multipart body — used by IP-pool editing per Mailgun's documented contract.</summary>
    public Task PatchMultipartNoResponseAsync(string path, MultipartBuilder mp, CancellationToken ct, string routeTemplate = "") =>
        SendNoBodyAsync(HttpMethod.Patch, path, query: null, content: mp.Build(), ct, routeTemplate);

    /// <summary>DELETE with a multipart body — used by IP-pool delegation revoke (subaccount in the body, not the path).</summary>
    public Task DeleteMultipartNoResponseAsync(string path, MultipartBuilder mp, CancellationToken ct, string routeTemplate = "") =>
        SendNoBodyAsync(HttpMethod.Delete, path, query: null, content: mp.Build(), ct, routeTemplate);

    public Task<TResponse> PostJsonBodyAsync<TResponse>(string path, object body, CancellationToken ct, string routeTemplate = "") =>
        SendJsonAsync<TResponse>(HttpMethod.Post, path, query: null, content: BuildJsonContent(body), ct, routeTemplate);

    public Task PostJsonBodyNoResponseAsync(string path, object body, CancellationToken ct, string routeTemplate = "") =>
        SendNoBodyAsync(HttpMethod.Post, path, query: null, content: BuildJsonContent(body), ct, routeTemplate);

    public Task<TResponse> PutJsonBodyAsync<TResponse>(string path, object body, CancellationToken ct, string routeTemplate = "") =>
        SendJsonAsync<TResponse>(HttpMethod.Put, path, query: null, content: BuildJsonContent(body), ct, routeTemplate);

    public Task PutJsonBodyNoResponseAsync(string path, object body, CancellationToken ct, string routeTemplate = "") =>
        SendNoBodyAsync(HttpMethod.Put, path, query: null, content: BuildJsonContent(body), ct, routeTemplate);

    public Task<TResponse> DeleteJsonAsync<TResponse>(string path, CancellationToken ct, string routeTemplate = "") =>
        SendJsonAsync<TResponse>(HttpMethod.Delete, path, query: null, content: null, ct, routeTemplate);

    /// <summary>DELETE with query-string parameters that returns a typed JSON response body.</summary>
    public Task<TResponse> DeleteJsonAsync<TResponse>(
        string path,
        IReadOnlyList<KeyValuePair<string, string?>>? query,
        CancellationToken ct,
        string routeTemplate = "") =>
        SendJsonAsync<TResponse>(HttpMethod.Delete, path, query, content: null, ct, routeTemplate);

    public Task DeleteNoResponseAsync(string path, CancellationToken ct, string routeTemplate = "") =>
        SendNoBodyAsync(HttpMethod.Delete, path, query: null, content: null, ct, routeTemplate);

    /// <summary>DELETE with query-string parameters and no response body.</summary>
    public Task DeleteNoResponseAsync(string path, IReadOnlyList<KeyValuePair<string, string?>>? query, CancellationToken ct, string routeTemplate = "") =>
        SendNoBodyAsync(HttpMethod.Delete, path, query, content: null, ct, routeTemplate);

    /// <summary>
    /// DELETE with a JSON request body — Mailgun uses this shape for endpoints like
    /// <c>DELETE /v1/analytics/tags</c> where the entity to delete is supplied in the body.
    /// </summary>
    public Task DeleteJsonBodyNoResponseAsync(string path, object body, CancellationToken ct, string routeTemplate = "") =>
        SendNoBodyAsync(HttpMethod.Delete, path, query: null, content: BuildJsonContent(body), ct, routeTemplate);

    /// <summary>
    /// Fetches a page from a Mailgun URL-pagination endpoint. <paramref name="absoluteUrlOrNull"/>
    /// is used verbatim when supplied (server-supplied next/previous URL); otherwise <paramref name="path"/> +
    /// <paramref name="query"/> is used.
    /// </summary>
    public async Task<SkipLimitPage<T>> GetSkipLimitPageAsync<T, TEnvelope>(
        string path,
        IReadOnlyList<KeyValuePair<string, string?>>? query,
        string? absoluteUrlOrNull,
        Func<TEnvelope, IReadOnlyList<T>?> itemsSelector,
        Func<TEnvelope, PagingLinks?> pagingSelector,
        Func<TEnvelope, long?>? totalCountSelector,
        CancellationToken ct,
        string routeTemplate = "")
        where TEnvelope : class
    {
        var envelope = absoluteUrlOrNull is null
            ? await GetJsonAsync<TEnvelope>(path, query, ct, routeTemplate).ConfigureAwait(false)
            : await GetJsonByAbsoluteUrlAsync<TEnvelope>(absoluteUrlOrNull, ct, routeTemplate).ConfigureAwait(false);

        var items = itemsSelector(envelope) ?? (IReadOnlyList<T>)Array.Empty<T>();
        var links = pagingSelector(envelope);
        var total = totalCountSelector?.Invoke(envelope);
        return new SkipLimitPage<T>(
            items,
            firstUrl: links?.First,
            previousUrl: links?.Previous,
            nextUrl: links?.Next,
            lastUrl: links?.Last,
            totalCount: total);
    }

    /// <summary>Builds an <see cref="AsyncPageable{T}"/> over a Mailgun URL-pagination endpoint.</summary>
    public AsyncPageable<T> GetSkipLimitPageable<T, TEnvelope>(
        string path,
        IReadOnlyList<KeyValuePair<string, string?>>? firstPageQuery,
        Func<TEnvelope, IReadOnlyList<T>?> itemsSelector,
        Func<TEnvelope, PagingLinks?> pagingSelector,
        Func<TEnvelope, long?>? totalCountSelector = null,
        string routeTemplate = "")
        where TEnvelope : class
    {
        // Capture routeTemplate in the closure so every page (first + follow-ups via absolute URL)
        // emits the same `http.route` metric tag.
        var capturedTemplate = routeTemplate;
        return new AsyncPageable<T>((nextUrl, ct) =>
            GetSkipLimitPageAsync(path, nextUrl is null ? firstPageQuery : null, nextUrl,
                itemsSelector, pagingSelector, totalCountSelector, ct, capturedTemplate));
    }

    private async Task<TResponse> GetJsonByAbsoluteUrlAsync<TResponse>(string absoluteUrl, CancellationToken ct, string routeTemplate = "")
    {
        var safeUri = ValidatePaginationUrl(absoluteUrl);
        using var request = new HttpRequestMessage(HttpMethod.Get, safeUri);
        var raw = await SendCoreAsync(request, ct, routeTemplate).ConfigureAwait(false);
        return Deserialize<TResponse>(raw);
    }

    /// <summary>
    /// Mailgun-region hosts an absolute pagination URL is allowed to point at. The SDK refuses to
    /// follow <c>paging.next</c> links to anything else, because <see cref="SendCoreAsync"/>
    /// unconditionally attaches the API key in Basic auth — a compromised upstream, replayed
    /// fixture, or accidentally-rewritten proxy response could otherwise turn auto-pagination
    /// into credential exfiltration.
    /// </summary>
    private static readonly string[] AllowedMailgunHosts =
    {
        "api.mailgun.net",
        "api.eu.mailgun.net",
    };

    /// <summary>
    /// Validates a server-supplied pagination URL: must be HTTPS, must be a host the SDK considers
    /// a Mailgun region (the configured base host OR a known Mailgun region host). The auth-bearing
    /// SDK must not be talked into following arbitrary absolute URLs from response data.
    /// </summary>
    private Uri ValidatePaginationUrl(string absoluteUrl)
    {
        if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri))
            throw new MailgunSerializationException(
                $"Mailgun pagination link is not a valid absolute URL: '{absoluteUrl}'.");

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            throw new MailgunSerializationException(
                $"Mailgun pagination link must be HTTPS; refusing to follow '{uri.Scheme}://{uri.Host}'.");

        var matchesBase = string.Equals(uri.Host, _baseUrl.Host, StringComparison.OrdinalIgnoreCase);
        var matchesRegion = false;
        foreach (var allowed in AllowedMailgunHosts)
        {
            if (string.Equals(uri.Host, allowed, StringComparison.OrdinalIgnoreCase))
            {
                matchesRegion = true;
                break;
            }
        }
        if (!matchesBase && !matchesRegion)
            throw new MailgunSerializationException(
                $"Refusing to follow off-origin Mailgun pagination link to host '{uri.Host}'.");

        return uri;
    }

    private async Task<TResponse> SendJsonAsync<TResponse>(
        HttpMethod method,
        string path,
        IReadOnlyList<KeyValuePair<string, string?>>? query,
        HttpContent? content,
        CancellationToken cancellationToken,
        string routeTemplate = "")
    {
        var raw = await SendStringAsync(method, path, query, content, cancellationToken, routeTemplate).ConfigureAwait(false);
        return Deserialize<TResponse>(raw);
    }

    private async Task SendNoBodyAsync(
        HttpMethod method,
        string path,
        IReadOnlyList<KeyValuePair<string, string?>>? query,
        HttpContent? content,
        CancellationToken cancellationToken,
        string routeTemplate = "")
    {
        _ = await SendStringAsync(method, path, query, content, cancellationToken, routeTemplate).ConfigureAwait(false);
    }

    private async Task<string> SendStringAsync(
        HttpMethod method,
        string path,
        IReadOnlyList<KeyValuePair<string, string?>>? query,
        HttpContent? content,
        CancellationToken cancellationToken,
        string routeTemplate = "")
    {
        var uri = BuildUri(path, query);
        using var request = new HttpRequestMessage(method, uri);
        if (content is not null)
            request.Content = content;
        return await SendCoreAsync(request, cancellationToken, routeTemplate).ConfigureAwait(false);
    }

    private async Task<string> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken, string routeTemplate = "")
    {
        // Auth is injected per-request rather than via a DelegatingHandler so that callers who supply
        // their own HttpClient (e.g. through IHttpClientFactory + AddMailgun) still authenticate
        // without needing to wire up an additional handler.
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _basicAuthHeaderValue);
        request.Headers.UserAgent.ParseAdd(UserAgentValue);
        if (!string.IsNullOrWhiteSpace(_userAgentSuffix))
        {
            request.Headers.UserAgent.ParseAdd(_userAgentSuffix);
        }
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(_onBehalfOf))
        {
            request.Headers.TryAddWithoutValidation("X-Mailgun-On-Behalf-Of", _onBehalfOf);
        }

        // Stamp the route template on the request so RateLimitHandler can read it back when emitting
        // the retries counter — there's no direct channel through the DelegatingHandler boundary.
        request.Options.Set(MailgunMeter.RouteTemplateKey, routeTemplate);

        // Pre-compute tag values we'll need on both success and exception paths. Using TagList
        // (stack-allocated struct) instead of KeyValuePair[] keeps the metric hot-path alloc-free.
        var methodName = request.Method.Method;
        // Stryker disable once all : RequestUri is set by the SDK before this point; the ?? fallback is defensive against null but unreachable on any production path.
        var hostName = request.RequestUri?.Host ?? string.Empty;
        var activeTags = new TagList
        {
            { "http.request.method", methodName },
            { "server.address", hostName },
        };
        MailgunMeter.ActiveRequests.Add(1, activeTags);
        var startTimestamp = Stopwatch.GetTimestamp();
        int? statusCode = null;

        try
        {
            using var activity = MailgunActivitySource.Instance.StartActivity(
                $"mailgun {request.Method.Method}",
                System.Diagnostics.ActivityKind.Client);
            activity?.SetTag("http.request.method", request.Method.Method);
            // url.full is REDACTED to the parameterized route template — the same low-cardinality
            // value the metrics use — so recipient email addresses that appear in suppression paths
            // (v3/{domain}/bounces/{address}, /complaints/{address}, /unsubscribes/{address}), mailing-
            // list member paths, and the validate query (?address=) never reach a tracing backend.
            // Without a template (server-supplied pagination links) we keep scheme/host/path but drop
            // the query string. See OTel HTTP semconv: sensitive values must be redacted from url.full.
            if (!string.IsNullOrEmpty(routeTemplate))
                activity?.SetTag("http.route", routeTemplate);
            activity?.SetTag("url.full", BuildRedactedUrl(request.RequestUri, routeTemplate));
            activity?.SetTag("server.address", request.RequestUri?.Host);

            try
            {
                using var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                // Assign status BEFORE any throw so the duration histogram's dimension count stays
                // stable across success / non-2xx paths (OTel collectors warn on dimension drift).
                statusCode = (int)response.StatusCode;

                var metadata = MailgunResponseMetadata.FromHttpResponse(response);
                LastResponseMetadata = metadata;

                activity?.SetTag("http.response.status_code", statusCode);
                if (metadata.RequestId is { } reqId)
                    activity?.SetTag("mailgun.request_id", reqId);
                if (metadata.RateLimitRemaining is { } remaining)
                    activity?.SetTag("mailgun.rate_limit.remaining", remaining);

                // Invoke the per-response callback exactly once per request, regardless of success
                // status. Concurrent-safe (the SDK doesn't store metadata via this callback — it's the
                // caller's responsibility to route it into their own storage). Wrapped so a thrown
                // callback can't break the request, but the exception is surfaced to Trace + the active
                // Activity so a misbehaving callback (e.g. one whose logger throws) is diagnosable.
                if (_onResponse is { } onResponse)
                {
                    try { onResponse(metadata); }
                    catch (Exception cbEx)
                    {
                        System.Diagnostics.Trace.TraceWarning(
                            "Mailgun OnResponse callback threw {0}: {1}", cbEx.GetType().FullName, cbEx.Message);
                        activity?.AddEvent(new System.Diagnostics.ActivityEvent("mailgun.on_response.exception",
                            tags: new System.Diagnostics.ActivityTagsCollection
                            {
                                ["exception.type"] = cbEx.GetType().FullName,
                                ["exception.message"] = cbEx.Message,
                            }));
                    }
                }

                var raw = await ReadBodyWithCapAsync(response, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, $"HTTP {(int)response.StatusCode}");
                    throw BuildException(response, raw, metadata);
                }
                return raw;
            }
            catch (Exception ex) when (activity is not null)
            {
                activity.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.GetType().Name);
                activity.AddTag("exception.type", ex.GetType().FullName);
                activity.AddTag("exception.message", ex.Message);
                throw;
            }
        }
        catch (Exception ex)
        {
            // The outer catch fires whether or not Activity sampling started one. The errors counter
            // captures BOTH transport-layer failures (HttpRequestException, TaskCanceledException)
            // AND 4xx/5xx mapped to MailgunException via BuildException above.
            var errorTags = new TagList
            {
                { "http.request.method", methodName },
                { "http.route", routeTemplate },
                { "error.type", ex.GetType().FullName ?? ex.GetType().Name },
                { "server.address", hostName },
            };
            MailgunMeter.RequestErrors.Add(1, errorTags);
            throw;
        }
        finally
        {
            // Increment/decrement must balance even when TagList construction in the catch would
            // throw. Decrement first, then record duration — order matters because Stopwatch is the
            // last thing we want to measure.
            MailgunMeter.ActiveRequests.Add(-1, activeTags);
            var elapsedSeconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
            var durationTags = new TagList
            {
                { "http.request.method", methodName },
                { "http.route", routeTemplate },
                { "server.address", hostName },
            };
            if (statusCode is { } sc)
                durationTags.Add("http.response.status_code", sc);
            MailgunMeter.RequestDuration.Record(elapsedSeconds, durationTags);
        }
    }

    /// <summary>
    /// Builds a PII-free URL for the <c>url.full</c> span tag. With a route template we substitute the
    /// fully-parameterized form (e.g. <c>https://api.mailgun.net/v3/{domain}/bounces/{address}</c>);
    /// without one we keep scheme/host/path but drop the query string (where <c>?address=</c> lives).
    /// </summary>
    private static string? BuildRedactedUrl(Uri? uri, string routeTemplate)
    {
        if (uri is null)
            return null;
        return string.IsNullOrEmpty(routeTemplate)
            ? $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}"
            : $"{uri.Scheme}://{uri.Authority}/{routeTemplate}";
    }

    /// <summary>
    /// Reads the response body into a string, enforcing <see cref="_maxResponseContentBytes"/> so a
    /// compromised or MITM'd endpoint cannot stream an oversized body to exhaust memory. Rejects early
    /// when the advertised <c>Content-Length</c> is over the cap, and bounds the streaming read for
    /// chunked responses that omit it. Mailgun's API is always UTF-8 JSON.
    /// </summary>
    private async Task<string> ReadBodyWithCapAsync(HttpResponseMessage response, CancellationToken ct)
    {
        // Stryker disable once all : HttpClient sets Content to EmptyContent for empty bodies; the null branch is defensive.
        if (response.Content is null)
            return string.Empty;

        var cap = _maxResponseContentBytes;
        if (response.Content.Headers.ContentLength is { } advertised && advertised > cap)
        {
            throw new MailgunSerializationException(
                $"Mailgun response body ({advertised} bytes) exceeds the configured {cap}-byte limit " +
                "(MailgunClientOptions.MaxResponseContentBytes).");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(rented.AsMemory(), ct).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > cap)
                {
                    throw new MailgunSerializationException(
                        $"Mailgun response body exceeds the configured {cap}-byte limit " +
                        "(MailgunClientOptions.MaxResponseContentBytes).");
                }
                await buffer.WriteAsync(rented.AsMemory(0, read), ct).ConfigureAwait(false);
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private Uri BuildUri(string path, IReadOnlyList<KeyValuePair<string, string?>>? query)
    {
        var relative = path.TrimStart('/');
        var baseUri = new Uri(_baseUrl, relative);
        if (query is null || query.Count == 0)
            return baseUri;

        var sb = new StringBuilder(baseUri.AbsoluteUri);
        // Pick the right separator: if the path already contains a '?', additional params append with '&'.
        var hasExistingQuery = baseUri.Query.Length > 1; // ".Query" includes the leading '?' when non-empty.
        sb.Append(hasExistingQuery ? '&' : '?');
        var first = true;
        foreach (var kv in query)
        {
            // Stryker disable once all : QueryBuilder.Add filters null values upfront; this `continue` is defensive against external IReadOnlyList callers.
            if (kv.Value is null)
                continue;
            if (!first)
                sb.Append('&');
            sb.Append(Uri.EscapeDataString(kv.Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(kv.Value));
            first = false;
        }
        return new Uri(sb.ToString());
    }

    private static StringContent BuildJsonContent(object body)
    {
        ArgumentNullException.ThrowIfNull(body);
        // Initialise upfront so any future Stryker mutation that removes the catch-block throw still
        // produces compilable code — otherwise the entire method drops out of mutation testing
        // under Stryker's safe-mode (Use of unassigned local variable 'json', CS0165).
        var json = string.Empty;
        try
        {
            json = JsonSerializer.Serialize(body, body.GetType(), MailgunJsonOptions.Default);
        }
        catch (JsonException ex)
        {
            throw new MailgunSerializationException("Failed to serialize Mailgun API request body.", ex);
        }
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static T Deserialize<T>(string rawBody)
    {
        if (string.IsNullOrEmpty(rawBody))
        {
            throw new MailgunSerializationException("Expected response body but received none.");
        }
        try
        {
            var parsed = JsonSerializer.Deserialize<T>(rawBody, MailgunJsonOptions.Default);
            if (parsed is null)
            {
                throw new MailgunSerializationException("Failed to deserialize Mailgun API response (null result).");
            }
            return parsed;
        }
        catch (JsonException ex)
        {
            throw new MailgunSerializationException("Failed to deserialize Mailgun API response.", ex);
        }
    }

    private static MailgunApiException BuildException(HttpResponseMessage response, string rawBody, MailgunResponseMetadata metadata)
    {
        string? message = null;
        var details = new List<string>();

        if (!string.IsNullOrEmpty(rawBody))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<MailgunErrorResponse>(rawBody, MailgunJsonOptions.Default);
                if (parsed is not null)
                {
                    message = parsed.Message ?? parsed.MessageCapital ?? parsed.Error;
                    AppendDetails(parsed.Details, details);
                    AppendDetails(parsed.Errors, details);
                }
            }
            catch (JsonException)
            {
                // Body wasn't JSON or didn't match the envelope; leave message null.
            }
        }

        var requestId = metadata.RequestId;
        var rateLimit = metadata.RateLimit;

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return new MailgunRateLimitException(message, details, requestId, rateLimit, rawBody);
        }
        return new MailgunApiException(response.StatusCode, message, details, requestId, rateLimit, rawBody);
    }

    private static void AppendDetails(object? raw, List<string> sink)
    {
        // `parsed.Details` / `parsed.Errors` are typed `object?` in MailgunErrorResponse, so the
        // System.Text.Json deserialiser always hands them through as JsonElement (or null). No
        // call path inside the SDK passes a raw `string` here, so we don't carry a case-string-s
        // arm — an earlier version had one but it was dead code.
        switch (raw)
        {
            case null:
                return;
            case JsonElement el:
                FlattenJson(el, sink);
                return;
        }
    }

    private static void FlattenJson(JsonElement el, List<string> sink)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    sink.Add(s!);
                break;
            case JsonValueKind.Array:
                foreach (var child in el.EnumerateArray())
                    FlattenJson(child, sink);
                break;
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var v = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(v))
                            sink.Add($"{prop.Name}: {v}");
                    }
                    else
                    {
                        FlattenJson(prop.Value, sink);
                    }
                }
                break;
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
