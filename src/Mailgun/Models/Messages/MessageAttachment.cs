namespace Mailgun.Models.Messages;

/// <summary>
/// A binary attachment or inline asset for a Mailgun send.
/// </summary>
public sealed class MessageAttachment
{
    /// <summary>File name as it should appear in the email.</summary>
    public string FileName { get; }

    /// <summary>Raw bytes of the file.</summary>
    public byte[] Content { get; }

    /// <summary>Optional MIME type. When null Mailgun infers from the extension.</summary>
    public string? ContentType { get; }

    /// <summary>Initializes a new attachment.</summary>
    public MessageAttachment(string fileName, byte[] content, string? contentType = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));
        ArgumentNullException.ThrowIfNull(content);
        FileName = fileName;
        Content = content;
        ContentType = contentType;
    }
}
