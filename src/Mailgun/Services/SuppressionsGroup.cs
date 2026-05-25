using Mailgun.Http;

namespace Mailgun.Services;

internal sealed class SuppressionsGroup : ISuppressionsGroup
{
    public SuppressionsGroup(MailgunHttpClient http)
    {
        Bounces = new BouncesService(http);
        Complaints = new ComplaintsService(http);
        Unsubscribes = new UnsubscribesService(http);
        Allowlists = new AllowlistsService(http);
    }

    public IBouncesService Bounces { get; }
    public IComplaintsService Complaints { get; }
    public IUnsubscribesService Unsubscribes { get; }
    public IAllowlistsService Allowlists { get; }
}
