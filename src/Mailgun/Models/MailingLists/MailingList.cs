using System.Text.Json.Serialization;
using Mailgun.Pagination;
using Mailgun.Serialization;

namespace Mailgun.Models.MailingLists;

/// <summary>A Mailgun mailing list (<c>/v3/lists</c>).</summary>
public sealed class MailingList
{
    [JsonPropertyName("address")] public string Address { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("access_level")] public string? AccessLevel { get; init; }
    [JsonPropertyName("reply_preference")] public string? ReplyPreference { get; init; }
    [JsonPropertyName("members_count")] public long? MembersCount { get; init; }
    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>A member of a Mailgun mailing list.</summary>
public sealed class MailingListMember
{
    [JsonPropertyName("address")] public string Address { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("subscribed")] public bool? Subscribed { get; init; }
    [JsonPropertyName("vars")] public Dictionary<string, object>? Vars { get; init; }
}

/// <summary>Parameters for <c>POST /v3/lists</c>.</summary>
public sealed class CreateMailingListRequest
{
    public string Address { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    /// <summary><c>readonly</c> | <c>members</c> | <c>everyone</c>.</summary>
    public string? AccessLevel { get; set; }
    /// <summary><c>list</c> | <c>sender</c>.</summary>
    public string? ReplyPreference { get; set; }
}

/// <summary>Parameters for <c>PUT /v3/lists/{address}</c>.</summary>
public sealed class UpdateMailingListRequest
{
    public string? Address { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? AccessLevel { get; set; }
    public string? ReplyPreference { get; set; }
}

/// <summary>Parameters for adding a single mailing-list member.</summary>
public sealed class AddMemberRequest
{
    public string Address { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool? Subscribed { get; set; }
    /// <summary>If <c>true</c>, upsert; otherwise fail when the address already exists.</summary>
    public bool? Upsert { get; set; }
    /// <summary>JSON-encoded variable map (Mailgun expects a JSON string).</summary>
    public string? Vars { get; set; }
}

internal sealed class MailingListListEnvelope
{
    [JsonPropertyName("items")] public List<MailingList>? Items { get; set; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; set; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; set; }
}

internal sealed class MailingListSingleEnvelope
{
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("list")] public MailingList List { get; set; } = new();
}

internal sealed class MailingListMembersEnvelope
{
    [JsonPropertyName("items")] public List<MailingListMember>? Items { get; set; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; set; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; set; }
}

internal sealed class MailingListMemberSingleEnvelope
{
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("member")] public MailingListMember Member { get; set; } = new();
}
