using System.Text.Json;
using System.Text.Json.Nodes;
using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Models.MailingLists;
using Mailgun.Pagination;
using Mailgun.Serialization;

namespace Mailgun.Services;

internal sealed class MailingListsService : IMailingListsService
{
    private readonly MailgunHttpClient _http;
    public MailingListsService(MailgunHttpClient http) => _http = http;

    public Task<SkipLimitPage<MailingList>> ListAsync(int? limit = null, int? skip = null, string? address = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder().Add("limit", limit).Add("skip", skip).Add("address", address).Build();
        return _http.GetSkipLimitPageAsync<MailingList, MailingListListEnvelope>(
            "v3/lists/pages", q, null, e => e.Items, e => e.Paging, e => e.TotalCount, cancellationToken);
    }

    public AsyncPageable<MailingList> ListAllAsync(int? limit = null)
    {
        var q = new QueryBuilder().Add("limit", limit).Build();
        return _http.GetSkipLimitPageable<MailingList, MailingListListEnvelope>(
            "v3/lists/pages", q, e => e.Items, e => e.Paging, e => e.TotalCount);
    }

    public async Task<MailingList> GetAsync(string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var env = await _http.GetJsonAsync<MailingListSingleEnvelope>($"v3/lists/{PathEscape.Segment(address)}", null, cancellationToken).ConfigureAwait(false);
        return env.List;
    }

    public async Task<MailingList> CreateAsync(CreateMailingListRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Address))
            throw new ArgumentException("Address is required.", nameof(request));
        var fb = new FormBuilder()
            .Add("address", request.Address)
            .Add("name", request.Name)
            .Add("description", request.Description)
            .Add("access_level", request.AccessLevel)
            .Add("reply_preference", request.ReplyPreference);
        var env = await _http.PostFormAsync<MailingListSingleEnvelope>("v3/lists", fb, cancellationToken).ConfigureAwait(false);
        return env.List;
    }

    public async Task<MailingList> UpdateAsync(string address, UpdateMailingListRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentNullException.ThrowIfNull(request);
        var fb = new FormBuilder()
            .Add("address", request.Address)
            .Add("name", request.Name)
            .Add("description", request.Description)
            .Add("access_level", request.AccessLevel)
            .Add("reply_preference", request.ReplyPreference);
        var env = await _http.PutFormAsync<MailingListSingleEnvelope>($"v3/lists/{PathEscape.Segment(address)}", fb, cancellationToken).ConfigureAwait(false);
        return env.List;
    }

    public Task DeleteAsync(string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return _http.DeleteNoResponseAsync($"v3/lists/{PathEscape.Segment(address)}", cancellationToken);
    }

    public Task<SkipLimitPage<MailingListMember>> ListMembersAsync(string listAddress, int? limit = null, int? skip = null, bool? subscribed = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listAddress);
        var q = new QueryBuilder().Add("limit", limit).Add("skip", skip).Add("subscribed", subscribed).Build();
        return _http.GetSkipLimitPageAsync<MailingListMember, MailingListMembersEnvelope>(
            $"v3/lists/{PathEscape.Segment(listAddress)}/members/pages",
            q, null, e => e.Items, e => e.Paging, e => e.TotalCount, cancellationToken);
    }

    public AsyncPageable<MailingListMember> ListAllMembersAsync(string listAddress, int? limit = null, bool? subscribed = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listAddress);
        var q = new QueryBuilder().Add("limit", limit).Add("subscribed", subscribed).Build();
        return _http.GetSkipLimitPageable<MailingListMember, MailingListMembersEnvelope>(
            $"v3/lists/{PathEscape.Segment(listAddress)}/members/pages",
            q, e => e.Items, e => e.Paging, e => e.TotalCount);
    }

    public async Task<MailingListMember> GetMemberAsync(string listAddress, string memberAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberAddress);
        var env = await _http.GetJsonAsync<MailingListMemberSingleEnvelope>(
            $"v3/lists/{PathEscape.Segment(listAddress)}/members/{PathEscape.Segment(memberAddress)}",
            null, cancellationToken).ConfigureAwait(false);
        return env.Member;
    }

    public async Task<MailingListMember> AddMemberAsync(string listAddress, AddMemberRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listAddress);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Address))
            throw new ArgumentException("Address is required.", nameof(request));
        var fb = MemberToForm(request);
        var env = await _http.PostFormAsync<MailingListMemberSingleEnvelope>(
            $"v3/lists/{PathEscape.Segment(listAddress)}/members", fb, cancellationToken).ConfigureAwait(false);
        return env.Member;
    }

    public async Task<MailingListMember> UpdateMemberAsync(string listAddress, string memberAddress, AddMemberRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberAddress);
        ArgumentNullException.ThrowIfNull(request);
        var fb = MemberToForm(request);
        var env = await _http.PutFormAsync<MailingListMemberSingleEnvelope>(
            $"v3/lists/{PathEscape.Segment(listAddress)}/members/{PathEscape.Segment(memberAddress)}",
            fb, cancellationToken).ConfigureAwait(false);
        return env.Member;
    }

    public Task DeleteMemberAsync(string listAddress, string memberAddress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberAddress);
        return _http.DeleteNoResponseAsync(
            $"v3/lists/{PathEscape.Segment(listAddress)}/members/{PathEscape.Segment(memberAddress)}",
            cancellationToken);
    }

    public Task BulkAddMembersAsync(string listAddress, IReadOnlyList<AddMemberRequest> members, bool upsert = true, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listAddress);
        ArgumentNullException.ThrowIfNull(members);
        if (members.Count == 0)
            throw new ArgumentException("At least one member is required.", nameof(members));
        if (members.Count > 1000)
            throw new ArgumentException("Mailgun bulk add accepts at most 1000 members per call.", nameof(members));

        // Mailgun's POST /v3/lists/{list}/members.json expects each member's `vars` as a NESTED JSON
        // OBJECT, not a JSON-encoded string. AddMemberRequest.Vars is a string for compatibility with
        // the single-member form path (where Mailgun accepts a JSON-string-typed form field), so we
        // parse it into a JsonNode here before embedding. Invalid JSON for any member raises a
        // clear ArgumentException that pinpoints the offending address.
        var dtos = new List<BulkMemberDto>(members.Count);
        foreach (var m in members)
        {
            JsonNode? vars = null;
            if (!string.IsNullOrEmpty(m.Vars))
            {
                try { vars = JsonNode.Parse(m.Vars); }
                catch (JsonException ex)
                {
                    throw new ArgumentException(
                        $"Member '{m.Address}' has Vars that is not valid JSON: {ex.Message}", nameof(members), ex);
                }
            }
            dtos.Add(new BulkMemberDto(m.Address, m.Name, m.Subscribed, vars));
        }
        var json = JsonSerializer.Serialize(dtos, MailgunJsonOptions.Default);
        var fb = new FormBuilder().Add("members", json).Add("upsert", upsert);
        return _http.PostFormNoResponseAsync($"v3/lists/{PathEscape.Segment(listAddress)}/members.json", fb, cancellationToken);
    }

    public async Task BulkAddMembersCsvAsync(string listAddress, Stream csvStream, string fileName = "members.csv", bool upsert = true, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listAddress);
        ArgumentNullException.ThrowIfNull(csvStream);
        using var mp = new MultipartBuilder()
            .AddText("upsert", upsert)
            .AddFile("file", fileName, csvStream, "text/csv");
        await _http.PostMultipartNoResponseAsync($"v3/lists/{PathEscape.Segment(listAddress)}/members.csv", mp, cancellationToken).ConfigureAwait(false);
    }

    private static FormBuilder MemberToForm(AddMemberRequest m)
    {
        var fb = new FormBuilder()
            .Add("address", m.Address)
            .Add("name", m.Name)
            .Add("subscribed", m.Subscribed)
            .Add("upsert", m.Upsert)
            .Add("vars", m.Vars);
        return fb;
    }

    private sealed record BulkMemberDto(string Address, string? Name, bool? Subscribed, JsonNode? Vars);
}
