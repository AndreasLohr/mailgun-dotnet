namespace Mailgun.Services;

/// <summary>
/// Container for the four Mailgun suppression-list services: bounces, complaints, unsubscribes,
/// and allowlists (aka "whitelists" in the underlying API).
/// </summary>
public interface ISuppressionsGroup
{
    /// <summary>Operations on <c>/v3/{domain}/bounces</c>.</summary>
    IBouncesService Bounces { get; }

    /// <summary>Operations on <c>/v3/{domain}/complaints</c>.</summary>
    IComplaintsService Complaints { get; }

    /// <summary>Operations on <c>/v3/{domain}/unsubscribes</c>.</summary>
    IUnsubscribesService Unsubscribes { get; }

    /// <summary>Operations on <c>/v3/{domain}/whitelists</c> (allowlists).</summary>
    IAllowlistsService Allowlists { get; }
}
