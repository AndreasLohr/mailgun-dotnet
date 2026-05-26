namespace Mailgun.Models.Messages;

/// <summary>
/// A binary attachment or inline asset for a Mailgun send.
/// </summary>
public sealed class MessageAttachment
{
    /// <summary>File name as it should appear in the email.</summary>
    public string FileName { get; }

    /// <summary>Raw bytes of the file. A defensive copy of the caller-supplied array — see ctor docs.</summary>
    public byte[] Content { get; }

    /// <summary>Optional MIME type. When null Mailgun infers from the extension.</summary>
    public string? ContentType { get; }

    /// <summary>
    /// Initializes a new attachment.
    /// </summary>
    /// <remarks>
    /// <paramref name="content"/> is copied at construction time. The attachment retains no
    /// reference to the caller's array, so callers are free to recycle it (return to
    /// <see cref="System.Buffers.ArrayPool{T}"/>, reuse a staging buffer in a batch loop, etc.)
    /// without affecting any in-flight HTTP body. This mirrors the SDK-internal
    /// <c>MultipartBuilder.AddFile</c> contract — once you hand the bytes to the SDK, mutations to
    /// the original array are invisible to the wire. The copy is bounded by Mailgun's documented
    /// attachment-size limits (25 MB) and dominated by the HTTP round-trip cost.
    /// </remarks>
    public MessageAttachment(string fileName, byte[] content, string? contentType = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));
        ArgumentNullException.ThrowIfNull(content);
        FileName = fileName;
        // Defensive copy on intake. Without this, a caller who mutates `content` between
        // constructing the attachment and the corresponding SendAsync would leak the mutation onto
        // the wire — a silent data-corruption bug class identical to the one MultipartBuilder.AddFile
        // already defends against at the multipart-serialisation layer.
        var copy = new byte[content.Length];
        Buffer.BlockCopy(content, 0, copy, 0, content.Length);
        Content = copy;
        ContentType = contentType;
    }
}
