using Mailgun.Http;
using Mailgun.Services;

namespace Mailgun;

/// <summary>
/// Public entry point to the Mailgun API. Implementations are thread-safe and intended as singletons.
/// </summary>
public interface IMailgunClient
{
    /// <summary>
    /// Metadata about the most recent HTTP response from Mailgun. Not safe for concurrent use
    /// against a shared <see cref="MailgunClient"/> — parallel callers race to overwrite this
    /// field. Use <see cref="MailgunClientOptions.OnResponse"/> for per-request metadata in
    /// concurrent scenarios.
    /// </summary>
    MailgunResponseMetadata? LastResponseMetadata { get; }

    /// <summary>Returns a derived client that sends every request with the supplied subaccount id
    /// in <c>X-Mailgun-On-Behalf-Of</c>.</summary>
    IMailgunClient ForSubaccount(string subaccountId);

    /// <summary>Operations on <c>POST /v3/{domain}/messages</c> and friends.</summary>
    IMessagesService Messages { get; }

    /// <summary>Operations on <c>/v4/domains</c> (CRUD, verify, tracking, SMTP credentials, DKIM, keys).</summary>
    IDomainsService Domains { get; }

    /// <summary>Operations on <c>/v3/ips</c>.</summary>
    IIpsService Ips { get; }

    /// <summary>Operations on <c>/v3/ip_pools</c>.</summary>
    IIpPoolsService IpPools { get; }

    /// <summary>Operations on <c>/v1/dynamic_pools</c>.</summary>
    IDynamicIpPoolsService DynamicIpPools { get; }

    /// <summary>Operations on <c>/v3/ip_warmups</c>.</summary>
    IIpWarmupsService IpWarmups { get; }

    /// <summary>Operations on <c>/v1/webhooks</c> (account) and <c>/v4/domains/{domain}/webhooks</c> (domain).</summary>
    IWebhooksService Webhooks { get; }

    /// <summary>Mailgun suppression lists — bounces, complaints, unsubscribes, allowlists.</summary>
    ISuppressionsGroup Suppressions { get; }

    /// <summary>Operations on <c>/v3/routes</c>.</summary>
    IRoutesService Routes { get; }

    /// <summary>Operations on <c>/v3/lists</c> + members.</summary>
    IMailingListsService MailingLists { get; }

    /// <summary>Operations on <c>/v4/templates</c> + versions.</summary>
    ITemplatesService Templates { get; }

    /// <summary>Operations on <c>/v1/analytics/{metrics,usage/metrics,logs}</c>.</summary>
    IAnalyticsService Analytics { get; }

    /// <summary>Operations on <c>/v1/analytics/tags</c>.</summary>
    IAnalyticsTagsService AnalyticsTags { get; }

    /// <summary>Operations on <c>/v1/bounce-classification</c> and <c>/v2/bounce-classification/metrics</c>.</summary>
    IBounceClassificationService BounceClassification { get; }

    /// <summary>Operations on <c>/v4/address/validate</c> + bulk + bulk preview.</summary>
    IValidateService Validate { get; }

    /// <summary>Operations on <c>/v4/inbox/*</c> (Inbox Placement).</summary>
    IInboxPlacementService InboxPlacement { get; }

    /// <summary>Operations on <c>/v1/alerts</c>.</summary>
    IAlertsService Alerts { get; }

    /// <summary>Operations on <c>/v1/thresholds/alerts/send</c>.</summary>
    ISendAlertsService SendAlerts { get; }

    /// <summary>Operations on <c>/v1/thresholds/limits</c>.</summary>
    ILimitsService Limits { get; }

    /// <summary>Operations on <c>/v5/accounts/subaccounts</c>.</summary>
    ISubaccountsService Subaccounts { get; }

    /// <summary>Operations on <c>/v5/accounts/limit/custom/monthly</c>.</summary>
    ICustomMessageLimitService CustomMessageLimit { get; }

    /// <summary>Operations on <c>/v5/accounts</c>.</summary>
    IAccountService Account { get; }

    /// <summary>Operations on <c>/v5/users</c> (RBAC users).</summary>
    IUsersService Users { get; }

    /// <summary>Operations on <c>/v1/keys</c>.</summary>
    IKeysService Keys { get; }

    /// <summary>Operations on <c>/v1/dkim/keys</c> and <c>/v4/domains/{authority}/keys</c>.</summary>
    IDkimKeysService DkimKeys { get; }

    /// <summary>Operations on <c>/v1/dkim_management/{domain}</c> (rotation + auto-rotation).</summary>
    IDkimSecurityService DkimSecurity { get; }

    /// <summary>Operations on <c>/v2/ip_whitelist</c> (account IP allowlist).</summary>
    IIpAllowlistService IpAllowlist { get; }
}
