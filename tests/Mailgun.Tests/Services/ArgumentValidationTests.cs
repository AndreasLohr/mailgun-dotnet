using Mailgun.Models.Messages;
using Mailgun.Models.Routes;
using Mailgun.Models.Templates;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

/// <summary>
/// Pins down argument-validation behavior across every service. Each service guards public
/// methods with <see cref="ArgumentException.ThrowIfNullOrWhiteSpace(string?, string?)"/> on
/// path-segment-bound arguments; this class exercises the guarded paths so Stryker can't
/// remove the guard without a test failure.
/// </summary>
public class ArgumentValidationTests
{
    private static (Mailgun.MailgunClient client, Mailgun.Tests.TestHelpers.MockHttpMessageHandler handler) C() =>
        TestMailgunClient.Create();

    // ── Messages ──

    [Theory, InlineData(""), InlineData(" "), InlineData("\t")]
    public async Task Messages_SendAsync_rejects_blank_domain(string domain)
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            c.Messages.SendAsync(domain, new SendMessageRequest { From = "a@b", To = { "c@d" }, Text = "t" }));
    }

    [Fact]
    public async Task Messages_SendAsync_rejects_blank_From()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            c.Messages.SendAsync("d", new SendMessageRequest { From = "", To = { "x@y" }, Text = "t" }));
    }

    [Fact]
    public async Task Messages_SendAsync_rejects_null_request()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Messages.SendAsync("d", null!));
    }

    [Fact]
    public async Task Messages_SendMime_rejects_blank_domain_null_to_null_mime()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Messages.SendMimeAsync("", new[] { "x@y" }, new byte[] { 1 }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Messages.SendMimeAsync("d", null!, new byte[] { 1 }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Messages.SendMimeAsync("d", new[] { "x@y" }, null!));
    }

    [Theory, InlineData(""), InlineData("\t")]
    public async Task Messages_GetStored_DeleteStored_reject_blank_args(string blank)
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Messages.GetStoredAsync(blank, "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Messages.GetStoredAsync("d", blank));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Messages.DeleteStoredAsync(blank, "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Messages.DeleteStoredAsync("d", blank));
    }

    [Fact]
    public async Task Messages_GetSendingQueues_DeleteScheduledEnvelopes_reject_blank()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Messages.GetSendingQueuesAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Messages.DeleteScheduledEnvelopesAsync(""));
    }

    // ── Domains ──

    [Fact]
    public async Task Domains_path_args_must_not_be_blank()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.GetAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.UpdateAsync("", new()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.DeleteAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.VerifyAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.GetTrackingAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.UpdateOpenTrackingAsync("", true));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.UpdateClickTrackingAsync("", "yes"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.UpdateClickTrackingAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.UpdateUnsubscribeTrackingAsync("", true));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.ListSmtpCredentialsAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.CreateSmtpCredentialAsync("", "u", "p"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.CreateSmtpCredentialAsync("d", "", "p"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.CreateSmtpCredentialAsync("d", "u", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.UpdateSmtpCredentialAsync("", "u", "p"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.DeleteSmtpCredentialAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Domains.UpdateConnectionSettingsAsync(""));
    }

    [Fact]
    public async Task Domains_CreateAsync_rejects_blank_name()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            c.Domains.CreateAsync(new() { Name = "" }));
    }

    // ── Suppressions ──

    [Fact]
    public async Task Suppressions_path_args_must_not_be_blank()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Bounces.GetAsync("", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Bounces.GetAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Bounces.CreateAsync("", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Bounces.CreateAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Bounces.DeleteAsync("", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Bounces.DeleteAllAsync(""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Suppressions.Bounces.ImportCsvAsync("d", null!));

        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Complaints.GetAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Complaints.CreateAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Complaints.DeleteAllAsync(""));

        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Unsubscribes.GetAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Unsubscribes.CreateAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Unsubscribes.DeleteAsync("d", ""));

        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Allowlists.GetAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Allowlists.DeleteAsync("d", ""));
    }

    // ── Routes ──

    [Fact]
    public async Task Routes_path_args_must_not_be_blank()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Routes.GetAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Routes.UpdateAsync("", new()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Routes.DeleteAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Routes.MatchAsync(""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Routes.CreateAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Routes.UpdateAsync("r1", null!));
    }

    // ── MailingLists ──

    [Fact]
    public async Task MailingLists_path_args_must_not_be_blank()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.GetAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.UpdateAsync("", new()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.DeleteAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.ListMembersAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.GetMemberAsync("", "a@b"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.GetMemberAsync("l@y", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.AddMemberAsync("", new() { Address = "a@b" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.AddMemberAsync("l@y", new() { Address = "" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.UpdateMemberAsync("", "a", new()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.UpdateMemberAsync("l", "", new()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.DeleteMemberAsync("", "a"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.DeleteMemberAsync("l", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.BulkAddMembersAsync("", new[] { new Mailgun.Models.MailingLists.AddMemberRequest { Address = "a" } }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.BulkAddMembersCsvAsync("", new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.MailingLists.BulkAddMembersCsvAsync("l", null!));
    }

    // ── Templates ──

    [Fact]
    public async Task Templates_path_args_must_not_be_blank()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.GetAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.UpdateAsync("", "d"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.UpdateAsync("t", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.DeleteAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.CopyAsync("", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.CopyAsync("t", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.RenameAsync("", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.RenameAsync("t", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.ListVersionsAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.GetVersionAsync("", "v"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.GetVersionAsync("t", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.CreateVersionAsync("", new CreateTemplateVersionRequest { Tag = "v", Template = "x" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.UpdateVersionAsync("", "v", new()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.UpdateVersionAsync("t", "", new()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.DeleteVersionAsync("", "v"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.DeleteVersionAsync("t", ""));
    }

    // ── Webhooks ──

    [Fact]
    public async Task Webhooks_path_args_must_not_be_blank()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.ListDomainAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.GetDomainAsync("", "e"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.GetDomainAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.CreateDomainAsync("", "e", new[] { "u" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.CreateDomainAsync("d", "", new[] { "u" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.UpdateDomainAsync("", "e", new[] { "u" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.UpdateDomainAsync("d", "", new[] { "u" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.DeleteDomainAsync("", "e"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.DeleteDomainAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.GetAccountWebhookAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.CreateAccountWebhookAsync("", new[] { "delivered" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.UpdateAccountWebhookAsync("", "https://x", new[] { "delivered" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.DeleteAccountWebhookAsync(""));
    }

    // ── IpPools / Ips / DynamicIpPools / IpWarmups ──

    [Fact]
    public async Task Ip_services_path_args_must_not_be_blank()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Ips.GetAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Ips.ListDomainsAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Ips.ListByDomainAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Ips.AttachToDomainAsync("", "ip"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Ips.AttachToDomainAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Ips.DetachFromDomainAsync("", "ip"));

        await Assert.ThrowsAsync<ArgumentException>(() => c.IpPools.GetAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.IpPools.UpdateAsync("", new()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.IpPools.DeleteAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.IpPools.AddIpsAsync("", new[] { "1.1.1.1" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.IpPools.RemoveIpAsync("", "ip"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.IpPools.RemoveIpAsync("p", ""));

        await Assert.ThrowsAsync<ArgumentException>(() => c.DynamicIpPools.GetAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DynamicIpPools.UpdateAsync("", new()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DynamicIpPools.DeleteAsync(""));

        await Assert.ThrowsAsync<ArgumentException>(() => c.IpWarmups.GetAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.IpWarmups.StartAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.IpWarmups.StopAsync(""));
    }

    // ── Keys / Validate / InboxPlacement / Alerts / AnalyticsTags / BounceClassification ──

    [Fact]
    public async Task Other_services_path_args_must_not_be_blank()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Keys.DeleteAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Validate.ValidateAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Validate.CreateBulkAsync("", new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Validate.GetBulkAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Validate.DeleteBulkAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.GetSeedlistAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.UpdateSeedlistAsync("", new()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.DeleteSeedlistAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.GetResultAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Alerts.AddEmailAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Alerts.RemoveEmailAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Alerts.AddSlackChannelAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Alerts.RemoveSlackChannelAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Alerts.AddWebhookAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Alerts.RemoveWebhookAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Alerts.SubscribeEventAsync("", "c"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Alerts.SubscribeEventAsync("e", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.AnalyticsTags.DeleteAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.BounceClassification.GetAsync(""));
    }

    // ── Account / Subaccounts / Users ──

    [Fact]
    public async Task Subaccount_Account_User_path_args_must_not_be_blank()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Subaccounts.GetAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Subaccounts.CreateAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Subaccounts.UpdateAsync("", "n"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Subaccounts.UpdateAsync("a", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Subaccounts.EnableAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Subaccounts.DisableAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Subaccounts.GetFeaturesAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Subaccounts.UpdateFeaturesAsync("", new()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Subaccounts.GetMonthlyCustomLimitAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Subaccounts.SetMonthlyCustomLimitAsync("", 1000));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Account.AddSandboxAuthRecipientAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Account.RemoveSandboxAuthRecipientAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Users.GetAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Users.UpdateAsync("", new()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Users.DeleteAsync(""));
    }
}
