using Mailgun.Models.MailingLists;
using Mailgun.Pagination;

namespace Mailgun.Services;

/// <summary>Operations on <c>/v3/lists</c> + list members.</summary>
public interface IMailingListsService
{
    Task<SkipLimitPage<MailingList>> ListAsync(int? limit = null, int? skip = null, string? address = null, CancellationToken cancellationToken = default);
    AsyncPageable<MailingList> ListAllAsync(int? limit = null);
    Task<MailingList> GetAsync(string address, CancellationToken cancellationToken = default);
    Task<MailingList> CreateAsync(CreateMailingListRequest request, CancellationToken cancellationToken = default);
    Task<MailingList> UpdateAsync(string address, UpdateMailingListRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string address, CancellationToken cancellationToken = default);

    Task<SkipLimitPage<MailingListMember>> ListMembersAsync(string listAddress, int? limit = null, int? skip = null, bool? subscribed = null, CancellationToken cancellationToken = default);
    AsyncPageable<MailingListMember> ListAllMembersAsync(string listAddress, int? limit = null, bool? subscribed = null);
    Task<MailingListMember> GetMemberAsync(string listAddress, string memberAddress, CancellationToken cancellationToken = default);
    Task<MailingListMember> AddMemberAsync(string listAddress, AddMemberRequest request, CancellationToken cancellationToken = default);
    Task<MailingListMember> UpdateMemberAsync(string listAddress, string memberAddress, AddMemberRequest request, CancellationToken cancellationToken = default);
    Task DeleteMemberAsync(string listAddress, string memberAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v3/lists/{address}/members.json</c> — bulk add up to 1000 members at once.
    /// </summary>
    Task BulkAddMembersAsync(string listAddress, IReadOnlyList<AddMemberRequest> members, bool upsert = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>POST /v3/lists/{address}/members.csv</c> — bulk add members from a CSV stream.
    /// </summary>
    Task BulkAddMembersCsvAsync(string listAddress, Stream csvStream, string fileName = "members.csv", bool upsert = true, CancellationToken cancellationToken = default);
}
