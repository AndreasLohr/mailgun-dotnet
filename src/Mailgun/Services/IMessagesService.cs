using Mailgun.Models.Messages;

namespace Mailgun.Services;

/// <summary>
/// Endpoints under <c>/v3/{domain}/messages</c> and related message operations.
/// </summary>
public interface IMessagesService
{
    /// <summary>
    /// <c>POST /v3/{domain}/messages</c> — send a message. Encoding is <c>multipart/form-data</c> when
    /// the request has attachments or inline assets, otherwise <c>application/x-www-form-urlencoded</c>.
    /// </summary>
    Task<SendMessageResponse> SendAsync(string domain, SendMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v3/{domain}/messages.mime</c> — send a pre-built MIME message.
    /// </summary>
    Task<SendMessageResponse> SendMimeAsync(
        string domain,
        IReadOnlyList<string> to,
        byte[] mimeMessage,
        bool? testMode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v3/domains/{domain}/messages/{storageKey}</c> — retrieve a stored message.
    /// </summary>
    Task<StoredMessage> GetStoredAsync(string domain, string storageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v3/domains/{domain}/messages/{storageKey}</c> — delete a stored message.
    /// </summary>
    Task DeleteStoredAsync(string domain, string storageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /v3/domains/{domain}/sending_queues</c> — sending queue status for the domain.
    /// </summary>
    Task<SendingQueueStatus> GetSendingQueuesAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DELETE /v3/domains/{domain}/envelopes</c> — purge the scheduled envelope queue.
    /// </summary>
    Task DeleteScheduledEnvelopesAsync(string domain, CancellationToken cancellationToken = default);
}
