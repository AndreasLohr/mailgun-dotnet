using Mailgun.Models.MailingLists;
using Mailgun.Models.Templates;
using Mailgun.Services;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

/// <summary>
/// Comprehensive blank-/null-argument sweep. The original argument-validation suite covered the
/// obvious Get/Create/Update/Delete paths; this fills in the gaps Stryker surfaced — every
/// List / ListAll / DeleteAll / ImportCsv / Bulk method, plus the blank-domain side of every
/// pair of arguments where only the blank-id side was tested.
/// </summary>
public class BlankArgumentSweepTests
{
    private static (Mailgun.MailgunClient client, Mailgun.Tests.TestHelpers.MockHttpMessageHandler handler) C() =>
        TestMailgunClient.Create();

    // ─── Suppressions ───

    [Theory, InlineData(""), InlineData(" ")]
    public async Task Bounces_list_paths_require_domain(string blank)
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Bounces.ListAsync(blank));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in c.Suppressions.Bounces.ListAllAsync(blank)) { break; }
        });
        await Assert.ThrowsAsync<ArgumentException>(() =>
            c.Suppressions.Bounces.ImportCsvAsync(blank, new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Bounces.GetAsync(blank, "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Bounces.CreateAsync(blank, "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Bounces.DeleteAsync(blank, "x"));
    }

    [Fact]
    public async Task Complaints_list_paths_require_domain()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Complaints.ListAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in c.Suppressions.Complaints.ListAllAsync("")) { break; }
        });
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Complaints.ImportCsvAsync("", new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Suppressions.Complaints.ImportCsvAsync("d", null!));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Complaints.GetAsync("", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Complaints.CreateAsync("", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Complaints.DeleteAsync("", "x"));
    }

    [Fact]
    public async Task Unsubscribes_list_paths_require_domain()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Unsubscribes.ListAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in c.Suppressions.Unsubscribes.ListAllAsync("")) { break; }
        });
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Unsubscribes.ImportCsvAsync("", new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Suppressions.Unsubscribes.ImportCsvAsync("d", null!));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Unsubscribes.DeleteAsync("", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Unsubscribes.DeleteAllAsync(""));
    }

    [Fact]
    public async Task Allowlists_list_paths_require_domain()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Allowlists.ListAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in c.Suppressions.Allowlists.ListAllAsync("")) { break; }
        });
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Allowlists.ImportCsvAsync("", new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Suppressions.Allowlists.ImportCsvAsync("d", null!));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Allowlists.DeleteAsync("", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Allowlists.DeleteAllAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Suppressions.Allowlists.CreateAsync(""));
    }

    // ─── Templates ───

    [Fact]
    public async Task Templates_validate_blank_segments_on_every_path()
    {
        var (c, _) = C();
        // CreateVersionAsync: tag + template are both required.
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.CreateVersionAsync("t", new CreateTemplateVersionRequest { Tag = "", Template = "x" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Templates.CreateVersionAsync("t", new CreateTemplateVersionRequest { Tag = "v", Template = "" }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Templates.CreateAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Templates.UpdateVersionAsync("t", "v", null!));
    }

    // ─── MailingLists ───

    [Fact]
    public async Task MailingLists_validate_blank_address_on_every_member_path()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.MailingLists.AddMemberAsync("l@y", new AddMemberRequest { Address = " " }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.MailingLists.AddMemberAsync("l@y", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.MailingLists.BulkAddMembersAsync("l@y", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.MailingLists.UpdateAsync("l@y", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.MailingLists.CreateAsync(null!));
    }

    // ─── Routes ───

    [Fact]
    public async Task Routes_validate_null_request_and_blank_id()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Routes.GetAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Routes.DeleteAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Routes.MatchAsync(" "));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Routes.CreateAsync(null!));
    }

    // ─── Webhooks ───

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Webhooks_validate_blank_event_type_everywhere(string blank)
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.GetDomainAsync("d", blank));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.CreateDomainAsync("d", blank, new[] { "u" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.UpdateDomainAsync("d", blank, new[] { "u" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.DeleteDomainAsync("d", blank));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.GetAccountWebhookAsync(blank));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.CreateAccountWebhookAsync(blank, new[] { "delivered" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.UpdateAccountWebhookAsync(blank, "https://x", new[] { "delivered" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.Webhooks.DeleteAccountWebhookAsync(blank));
    }

    // ─── Keys ───

    [Fact]
    public async Task Keys_validate_null_request()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Keys.CreateAsync(null!));
    }

    // ─── DKIM Keys ───

    [Fact]
    public async Task DkimKeys_validate_every_blank_segment()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.DkimKeys.CreateAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.CreateAsync(new() { SigningDomain = "", Selector = "s" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.CreateAsync(new() { SigningDomain = "d", Selector = "" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.DeleteAsync("", "s"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.DeleteAsync("d", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.ListForAuthorityAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.CreateForAuthorityAsync("", new() { SigningDomain = "d", Selector = "s" }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.DkimKeys.CreateForAuthorityAsync("a", null!));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.CreateForAuthorityAsync("a", new() { SigningDomain = "", Selector = "s" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.CreateForAuthorityAsync("a", new() { SigningDomain = "d", Selector = "" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.ActivateForAuthorityAsync("", "s"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.ActivateForAuthorityAsync("a", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.DeactivateForAuthorityAsync("", "s"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.DeactivateForAuthorityAsync("a", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.DeleteForAuthorityAsync("", "s"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimKeys.DeleteForAuthorityAsync("a", ""));
    }

    // ─── DKIM Security ───

    [Fact]
    public async Task DkimSecurity_validate_blank_domain_everywhere()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimSecurity.RotateAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimSecurity.GetAutoRotationAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DkimSecurity.SetAutoRotationAsync("", new()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.DkimSecurity.SetAutoRotationAsync("d", null!));
    }

    // ─── IPs / Pools / Warmups ───

    [Fact]
    public async Task IPs_extended_validate_blank_ip()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.Ips.GetReputationBandAsync(""));
    }

    [Fact]
    public async Task IpPools_extended_validate_blank_segments()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.IpPools.ReplaceIpsAsync("", new[] { "1.1.1.1" }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.IpPools.ReplaceIpsAsync("p", null!));
        await Assert.ThrowsAsync<ArgumentException>(() => c.IpPools.DelegateAsync("", "a"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.IpPools.DelegateAsync("p", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.IpPools.ListDelegationsAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.IpPools.RevokeDelegationAsync("", "a"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.IpPools.RevokeDelegationAsync("p", ""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.IpPools.CreateAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.IpPools.UpdateAsync("p", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.IpPools.AddIpsAsync("p", null!));
    }

    [Fact]
    public async Task DynamicIpPools_validate_null_request()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.DynamicIpPools.CreateAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.DynamicIpPools.UpdateAsync("dp", null!));
        // Bug surfaced during Stryker triage: blank Name on the request used to slip through.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            c.DynamicIpPools.CreateAsync(new() { Name = "" }));
    }

    // ─── Inbox Placement extended ───

    [Fact]
    public async Task InboxPlacement_extended_validate_blank_segments()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.DeleteResultAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.GetResultDetailsAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.GetResultCountersAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.AddSeedAsync("", "x@y"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.AddSeedAsync("l", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.RemoveSeedAsync("", "x@y"));
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.RemoveSeedAsync("l", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.ListResultsForSeedlistAsync(""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.InboxPlacement.FilterResultsAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.InboxPlacement.CreateSeedlistAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.InboxPlacement.UpdateSeedlistAsync("n", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.InboxPlacement.CreateTestAsync(null!));
        // Bugs surfaced during Stryker triage: blank required fields used to slip through.
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.CreateSeedlistAsync(new() { Name = "" }));
        await Assert.ThrowsAsync<ArgumentException>(() => c.InboxPlacement.CreateTestAsync(new() { Seedlist = "" }));
    }

    // ─── BounceClassification extended ───

    [Fact]
    public async Task BounceClassification_extended_validate_blank_segments()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() => c.BounceClassification.ListCodesAsync(""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.BounceClassification.ClassifyAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.BounceClassification.QueryMetricsAsync(null!));
    }

    // ─── Account / Subaccounts / Users ───

    [Fact]
    public async Task Account_extended_validate_null_request()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Account.UpdateAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Subaccounts.UpdateFeaturesAsync("a", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Users.CreateAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Users.UpdateAsync("u", null!));
        // Bug surfaced during Stryker triage: blank Email used to slip through.
        await Assert.ThrowsAsync<ArgumentException>(() => c.Users.CreateAsync(new() { Email = "" }));
    }

    // ─── Analytics ───

    [Fact]
    public async Task Analytics_validate_null_request()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Analytics.QueryMetricsAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Analytics.QueryUsageMetricsAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Analytics.QueryLogsAsync(null!));
    }

    // ─── Alerts ───

    [Fact]
    public async Task Alerts_validate_null_settings()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Alerts.UpdateSettingsAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.SendAlerts.UpdateConfigAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => c.Limits.UpdateAsync(null!));
    }

    // ─── Messages: SendMime with empty recipient list ───

    [Fact]
    public async Task SendMime_rejects_empty_recipients_array()
    {
        var (c, _) = C();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            c.Messages.SendMimeAsync("d", Array.Empty<string>(), new byte[] { 1 }));
    }

    // ─── Mailgun client itself ───

    [Fact]
    public void Constructor_rejects_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => new Mailgun.MailgunClient((Mailgun.MailgunClientOptions)null!));
    }

    [Fact]
    public void Constructor_rejects_blank_api_key()
    {
        Assert.Throws<ArgumentException>(() => new Mailgun.MailgunClient(new Mailgun.MailgunClientOptions { ApiKey = " " }));
    }
}
