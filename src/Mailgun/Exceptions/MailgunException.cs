namespace Mailgun.Exceptions;

/// <summary>
/// Base type for all exceptions thrown by the Mailgun SDK.
/// </summary>
public abstract class MailgunException : Exception
{
    /// <inheritdoc />
    protected MailgunException(string message) : base(message) { }

    /// <inheritdoc />
    protected MailgunException(string message, Exception? innerException) : base(message, innerException) { }
}
