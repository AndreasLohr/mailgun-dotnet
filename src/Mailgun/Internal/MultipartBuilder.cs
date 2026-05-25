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
        var bc = new ByteArrayContent(content);
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
    /// non-rewindable <c>StreamContent</c> would not survive. The caller's stream is then
    /// disposed alongside this builder.
    /// </summary>
    public MultipartBuilder AddFile(string name, string fileName, Stream stream, string? mediaType = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Buffer the stream so retries don't see an exhausted reader. For very small streams
        // this is essentially free; for bigger uploads (Mailgun caps bulk-validation CSV at
        // 25 MB) it costs a transient byte[] allocation, which is the right trade-off for a
        // robust retry path.
        byte[] buffer;
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var seg) && seg.Offset == 0)
        {
            buffer = seg.Count == seg.Array!.Length ? seg.Array : ms.ToArray();
        }
        else
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            buffer = copy.ToArray();
        }
        // Take ownership of the caller's stream so callers can use a single `using` for the
        // builder (matches the byte[] overload's lifetime expectations).
        _disposables.Add(stream);

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
