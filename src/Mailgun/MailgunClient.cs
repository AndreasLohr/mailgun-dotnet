using System.Diagnostics.CodeAnalysis;
using Mailgun.Http;
using Mailgun.Services;

namespace Mailgun;

/// <summary>
/// Top-level Mailgun API client. Thread-safe and intended as a singleton.
/// Resource services are exposed as lazy properties (e.g. <see cref="Messages"/>, <see cref="Domains"/>).
/// </summary>
/// <remarks>
/// <para><strong>AOT / trimming.</strong> The SDK serializes and deserializes its DTOs through
/// <see cref="System.Text.Json.JsonSerializer"/>'s reflection-based path. Apps published with
/// <c>PublishAot=true</c> or <c>PublishTrimmed=true</c> + <c>TrimMode=full</c> will see trim
/// warnings and may fail at runtime. A future release will migrate to a
/// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> source generator; until
/// then either disable trimming for this assembly or hold off on AOT publish.</para>
/// </remarks>
[RequiresUnreferencedCode("MailgunClient uses reflection-based System.Text.Json serialization. Suppressed at the public boundary; the SDK does not currently ship source-generated DTOs.")]
[RequiresDynamicCode("MailgunClient uses reflection-based System.Text.Json serialization which requires dynamic code generation under NativeAOT.")]
public sealed class MailgunClient : IMailgunClient, IDisposable, IAsyncDisposable
{
    private readonly MailgunHttpClient _httpClient;

    private readonly Lazy<IMessagesService> _messages;
    private readonly Lazy<IDomainsService> _domains;
    private readonly Lazy<IIpsService> _ips;
    private readonly Lazy<IIpPoolsService> _ipPools;
    private readonly Lazy<IDynamicIpPoolsService> _dynamicIpPools;
    private readonly Lazy<IIpWarmupsService> _ipWarmups;
    private readonly Lazy<IWebhooksService> _webhooks;
    private readonly Lazy<ISuppressionsGroup> _suppressions;
    private readonly Lazy<IRoutesService> _routes;
    private readonly Lazy<IMailingListsService> _mailingLists;
    private readonly Lazy<ITemplatesService> _templates;
    private readonly Lazy<IAnalyticsService> _analytics;
    private readonly Lazy<IAnalyticsTagsService> _analyticsTags;
    private readonly Lazy<IBounceClassificationService> _bounceClassification;
    private readonly Lazy<IValidateService> _validate;
    private readonly Lazy<IInboxPlacementService> _inboxPlacement;
    private readonly Lazy<IAlertsService> _alerts;
    private readonly Lazy<ISendAlertsService> _sendAlerts;
    private readonly Lazy<ILimitsService> _limits;
    private readonly Lazy<ISubaccountsService> _subaccounts;
    private readonly Lazy<ICustomMessageLimitService> _customMessageLimit;
    private readonly Lazy<IAccountService> _account;
    private readonly Lazy<IUsersService> _users;
    private readonly Lazy<IKeysService> _keys;
    private readonly Lazy<IDkimKeysService> _dkimKeys;
    private readonly Lazy<IDkimSecurityService> _dkimSecurity;
    private readonly Lazy<IIpAllowlistService> _ipAllowlist;

    /// <summary>Initializes a new client with the given API key, defaults for everything else.</summary>
    public MailgunClient(string apiKey)
        : this(new MailgunClientOptions { ApiKey = apiKey }) { }

    /// <summary>Initializes a new client with the supplied options.</summary>
    public MailgunClient(MailgunClientOptions options)
        : this(new MailgunHttpClient(options ?? throw new ArgumentNullException(nameof(options))), ownsHttp: true) { }

    private MailgunClient(MailgunHttpClient httpClient, bool ownsHttp)
    {
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttp;

        const LazyThreadSafetyMode Mode = LazyThreadSafetyMode.ExecutionAndPublication;

        _messages = new(() => new MessagesService(_httpClient), Mode);
        _domains = new(() => new DomainsService(_httpClient), Mode);
        _ips = new(() => new IpsService(_httpClient), Mode);
        _ipPools = new(() => new IpPoolsService(_httpClient), Mode);
        _dynamicIpPools = new(() => new DynamicIpPoolsService(_httpClient), Mode);
        _ipWarmups = new(() => new IpWarmupsService(_httpClient), Mode);
        _webhooks = new(() => new WebhooksService(_httpClient), Mode);
        _suppressions = new(() => new SuppressionsGroup(_httpClient), Mode);
        _routes = new(() => new RoutesService(_httpClient), Mode);
        _mailingLists = new(() => new MailingListsService(_httpClient), Mode);
        _templates = new(() => new TemplatesService(_httpClient), Mode);
        _analytics = new(() => new AnalyticsService(_httpClient), Mode);
        _analyticsTags = new(() => new AnalyticsTagsService(_httpClient), Mode);
        _bounceClassification = new(() => new BounceClassificationService(_httpClient), Mode);
        _validate = new(() => new ValidateService(_httpClient), Mode);
        _inboxPlacement = new(() => new InboxPlacementService(_httpClient), Mode);
        _alerts = new(() => new AlertsService(_httpClient), Mode);
        _sendAlerts = new(() => new SendAlertsService(_httpClient), Mode);
        _limits = new(() => new LimitsService(_httpClient), Mode);
        _subaccounts = new(() => new SubaccountsService(_httpClient), Mode);
        _customMessageLimit = new(() => new CustomMessageLimitService(_httpClient), Mode);
        _account = new(() => new AccountService(_httpClient), Mode);
        _users = new(() => new UsersService(_httpClient), Mode);
        _keys = new(() => new KeysService(_httpClient), Mode);
        _dkimKeys = new(() => new DkimKeysService(_httpClient), Mode);
        _dkimSecurity = new(() => new DkimSecurityService(_httpClient), Mode);
        _ipAllowlist = new(() => new IpAllowlistService(_httpClient), Mode);
    }

    private readonly bool _ownsHttpClient;

    /// <inheritdoc />
    public MailgunResponseMetadata? LastResponseMetadata => _httpClient.LastResponseMetadata;

    /// <inheritdoc />
    public IMailgunClient ForSubaccount(string subaccountId) =>
        new MailgunClient(_httpClient.ForSubaccount(subaccountId), ownsHttp: false);

    /// <inheritdoc />
    public IMessagesService Messages => _messages.Value;
    /// <inheritdoc />
    public IDomainsService Domains => _domains.Value;
    /// <inheritdoc />
    public IIpsService Ips => _ips.Value;
    /// <inheritdoc />
    public IIpPoolsService IpPools => _ipPools.Value;
    /// <inheritdoc />
    public IDynamicIpPoolsService DynamicIpPools => _dynamicIpPools.Value;
    /// <inheritdoc />
    public IIpWarmupsService IpWarmups => _ipWarmups.Value;
    /// <inheritdoc />
    public IWebhooksService Webhooks => _webhooks.Value;
    /// <inheritdoc />
    public ISuppressionsGroup Suppressions => _suppressions.Value;
    /// <inheritdoc />
    public IRoutesService Routes => _routes.Value;
    /// <inheritdoc />
    public IMailingListsService MailingLists => _mailingLists.Value;
    /// <inheritdoc />
    public ITemplatesService Templates => _templates.Value;
    /// <inheritdoc />
    public IAnalyticsService Analytics => _analytics.Value;
    /// <inheritdoc />
    public IAnalyticsTagsService AnalyticsTags => _analyticsTags.Value;
    /// <inheritdoc />
    public IBounceClassificationService BounceClassification => _bounceClassification.Value;
    /// <inheritdoc />
    public IValidateService Validate => _validate.Value;
    /// <inheritdoc />
    public IInboxPlacementService InboxPlacement => _inboxPlacement.Value;
    /// <inheritdoc />
    public IAlertsService Alerts => _alerts.Value;
    /// <inheritdoc />
    public ISendAlertsService SendAlerts => _sendAlerts.Value;
    /// <inheritdoc />
    public ILimitsService Limits => _limits.Value;
    /// <inheritdoc />
    public ISubaccountsService Subaccounts => _subaccounts.Value;
    /// <inheritdoc />
    public ICustomMessageLimitService CustomMessageLimit => _customMessageLimit.Value;
    /// <inheritdoc />
    public IAccountService Account => _account.Value;
    /// <inheritdoc />
    public IUsersService Users => _users.Value;
    /// <inheritdoc />
    public IKeysService Keys => _keys.Value;
    /// <inheritdoc />
    public IDkimKeysService DkimKeys => _dkimKeys.Value;
    /// <inheritdoc />
    public IDkimSecurityService DkimSecurity => _dkimSecurity.Value;
    /// <inheritdoc />
    public IIpAllowlistService IpAllowlist => _ipAllowlist.Value;

    internal MailgunHttpClient HttpClient => _httpClient;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    /// <summary>
    /// Releases the underlying <see cref="System.Net.Http.HttpClient"/> if the SDK created it.
    /// Unlocks the <c>await using var client = new MailgunClient(...)</c> pattern.
    /// </summary>
    /// <remarks>
    /// The actual disposal work is synchronous (<c>HttpClient.Dispose()</c> is sync), so this
    /// returns a completed <see cref="ValueTask"/> after delegating to the synchronous
    /// <c>Dispose</c>.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
