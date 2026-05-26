using System.Globalization;
using System.Net.Http.Headers;

namespace Mailgun.Internal;

/// <summary>
/// Builds <c>multipart/form-data</c> request bodies for Mailgun endpoints that accept files
/// (attachments on <c>POST /v3/{domain}/messages</c>, <c>.mime</c> sends, suppression CSV imports,
/// mailing-list bulk CSV adds, validation bulk CSV uploads).
/// </summary>
internal sealed class MultipartBuilder : IDisposable
{
    private readonly MultipartFormDataContent _content;
    private readonly List<IDisposable> _disposables = new();

    public MultipartBuilder()
    {
        // Avoid the default `boundary="..."` quoting which some servers reject; mirror what Mailgun's
        // own examples produce.
        _content = new MultipartFormDataContent("----MailgunDotNetBoundary" + Guid.NewGuid().ToString("N"));
    }

    public MultipartBuilder AddText(string name, string? value)
    {
        if (value is null)
            return this;
        var c = new StringContent(value);
        _disposables.Add(c);
        _content.Add(c, name);
        return this;
    }

    public MultipartBuilder AddText(string name, int? value) =>
        AddText(name, value?.ToString(CultureInfo.InvariantCulture));

    public MultipartBuilder AddText(string name, long? value) =>
        AddText(name, value?.ToString(CultureInfo.InvariantCulture));

    public MultipartBuilder AddText(string name, bool? value) =>
        AddText(name, value is null ? null : (value.Value ? "yes" : "no"));

    public MultipartBuilder AddText(string name, DateTimeOffset? value) =>
        AddText(name, value is null ? null : MailgunDate.FormatRfc2822(value.Value));

    public MultipartBuilder AddTextArray(string name, IEnumerable<string>? values)
    {
        if (values is null)
            return this;
        foreach (var v in values)
        {
            if (v is not null)
                AddText(name, v);
        }
        return this;
    }

    public MultipartBuilder AddPrefixed(string prefix, IReadOnlyDictionary<string, string>? values)
    {
        if (values is null)
            return this;
        foreach (var kv in values)
        {
            if (kv.Value is not null)
                AddText(prefix + kv.Key, kv.Value);
        }
        return this;
    }

    /// <summary>
    /// Adds a file part (e.g. <c>attachment</c>, <c>inline</c>, <c>message</c>, <c>file</c>).
    /// Caller owns <paramref name="content"/> lifetime; the builder copies the bytes.
    /// </summary>
    public MultipartBuilder AddFile(string name, string fileName, byte[] content, string? mediaType = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        // ByteArrayContent(byte[]) stores the reference — it does NOT copy. So a caller that
        // recycles the array (ArrayPool rental returned after AddFile, a reused staging buffer in
        // a batch send loop, anything async) would leak post-attach mutations onto the wire.
        // The Stream overload below has a multi-line comment explaining exactly this aliasing hazard
        // and was rewritten to defeat it; the byte[] overload's docstring already promises the
        // builder copies, so honour that here with an explicit clone.
        var copy = new byte[content.Length];
        Buffer.BlockCopy(content, 0, copy, 0, content.Length);

        var bc = new ByteArrayContent(copy);
        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            bc.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        }
        _disposables.Add(bc);
        _content.Add(bc, name, fileName);
        return this;
    }

    /// <summary>
    /// Adds a file part from a caller-supplied stream. The stream is read fully into memory
    /// before being attached so the request is retry-safe — the SDK's <c>RateLimitHandler</c>
    /// re-sends the same <see cref="HttpRequestMessage"/> on 429 / idempotent-5xx, which a
    /// non-rewindable <c>StreamContent</c> would not survive. Caller retains ownership of
    /// <paramref name="stream"/>; this method does NOT dispose it — the bytes are already
    /// copied so the caller is free to rewind, reuse, retry at a higher level, or leave the
    /// stream open as part of a longer-lived pipeline.
    /// </summary>
    public MultipartBuilder AddFile(string name, string fileName, Stream stream, string? mediaType = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Buffer the stream into a fresh byte[] so retries don't see an exhausted reader.
        //
        // The previous fast path for publicly-visible MemoryStream had two correctness bugs:
        //  (1) Stream.Position was ignored — TryGetBuffer returns the array bounds, not the unread
        //      region, so pre-position bytes (e.g. a header line the caller already consumed)
        //      leaked into the upload.
        //  (2) When the segment covered the whole array, the SDK aliased the caller's raw array
        //      directly into ByteArrayContent — later mutations to the caller's buffer would
        //      mutate the HTTP body in flight.
        // CopyTo always reads from the current Position and produces an independent copy, so both
        // bugs go away by removing the fast path entirely. The allocation cost is bounded by
        // Mailgun's own request-size limits (25 MB for bulk CSVs).
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        var buffer = copy.ToArray();

        var bc = new ByteArrayContent(buffer);
        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            bc.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        }
        _disposables.Add(bc);
        _content.Add(bc, name, fileName);
        return this;
    }

    public MultipartFormDataContent Build() => _content;

    public void Dispose()
    {
        _content.Dispose();
        foreach (var d in _disposables)
        {
            d.Dispose();
        }
    }
}
