namespace Mailgun.Exceptions;

/// <summary>
/// Thrown when the SDK cannot serialize a request or deserialize a response body.
/// </summary>
public sealed class MailgunSerializationException : MailgunException
{
    /// <summary>Initializes a new <see cref="MailgunSerializationException"/>.</summary>
    public MailgunSerializationException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
