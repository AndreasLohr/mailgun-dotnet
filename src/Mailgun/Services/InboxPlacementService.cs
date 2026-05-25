using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;

namespace Mailgun.Services;

/// <summary>
/// Operations on <c>/v4/inbox/*</c>: seedlists, results, tests, providers. (Inbox Placement testing.)
/// </summary>
public interface IInboxPlacementService
{
    /// <summary><c>GET /v4/inbox/seedlists</c> — list seedlists.</summary>
    Task<SeedlistListResponse> ListSeedlistsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/inbox/seedlists/{name}</c> — get a seedlist.</summary>
    Task<Seedlist> GetSeedlistAsync(string name, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v4/inbox/seedlists</c> — create a seedlist.</summary>
    Task<Seedlist> CreateSeedlistAsync(CreateSeedlistRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /v4/inbox/seedlists/{name}</c> — update a seedlist.</summary>
    Task<Seedlist> UpdateSeedlistAsync(string name, UpdateSeedlistRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v4/inbox/seedlists/{name}</c> — delete a seedlist.</summary>
    Task DeleteSeedlistAsync(string name, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/inbox/results</c> — list inbox-placement results.</summary>
    Task<InboxPlacementResultList> ListResultsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/inbox/results/{resultId}</c> — get a specific placement result.</summary>
    Task<InboxPlacementResult> GetResultAsync(string resultId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v4/inbox/tests</c> — start a new placement test.</summary>
    Task<InboxPlacementResult> CreateTestAsync(CreateInboxPlacementTestRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/inbox/providers</c> — list supported providers.</summary>
    Task<InboxPlacementProviderList> ListProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v4/inbox/results/{resultId}</c> — delete a placement result.</summary>
    Task DeleteResultAsync(string resultId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/inbox/results/{resultId}/details</c> — full per-provider breakdown for a placement result.</summary>
    Task<InboxPlacementResultDetails> GetResultDetailsAsync(string resultId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/inbox/results/{resultId}/counters</c> — bulk count of inbox/spam/missing for a result.</summary>
    Task<InboxPlacementCounters> GetResultCountersAsync(string resultId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v4/inbox/seedlists/{name}/seeds</c> — add a seed address to an existing seedlist.</summary>
    Task AddSeedAsync(string seedlistName, string email, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v4/inbox/seedlists/{name}/seeds/{email}</c> — remove a seed.</summary>
    Task RemoveSeedAsync(string seedlistName, string email, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/inbox/seedlists/{name}/results</c> — list placement results scoped to one seedlist.</summary>
    Task<InboxPlacementResultList> ListResultsForSeedlistAsync(string seedlistName, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/inbox/results/filter</c> — list placement results filtered by domain / from-address / subject.</summary>
    Task<InboxPlacementResultList> FilterResultsAsync(InboxPlacementResultsFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>Per-provider breakdown for a single placement result.</summary>
public sealed class InboxPlacementResultDetails
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("providers")] public List<InboxPlacementProviderResult>? Providers { get; init; }
}

/// <summary>Single-provider placement breakdown.</summary>
public sealed class InboxPlacementProviderResult
{
    [JsonPropertyName("provider")] public string? Provider { get; init; }
    [JsonPropertyName("inbox")] public int? Inbox { get; init; }
    [JsonPropertyName("spam")] public int? Spam { get; init; }
    [JsonPropertyName("missing")] public int? Missing { get; init; }
}

/// <summary>Total inbox / spam / missing counts for a placement result.</summary>
public sealed class InboxPlacementCounters
{
    [JsonPropertyName("inbox")] public int? Inbox { get; init; }
    [JsonPropertyName("spam")] public int? Spam { get; init; }
    [JsonPropertyName("missing")] public int? Missing { get; init; }
    [JsonPropertyName("total")] public int? Total { get; init; }
}

/// <summary>Filter parameters for <see cref="IInboxPlacementService.FilterResultsAsync(InboxPlacementResultsFilter, CancellationToken)"/>.</summary>
public sealed class InboxPlacementResultsFilter
{
    public string? Subject { get; set; }
    public string? FromAddress { get; set; }
    public string? FromDomain { get; set; }
    public string? Seedlist { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public int? Limit { get; set; }
}

/// <summary>An inbox placement seedlist.</summary>
public sealed class Seedlist
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("seeds")] public List<string>? Seeds { get; init; }
    [JsonPropertyName("filter")] public Dictionary<string, object>? Filter { get; init; }
}

/// <summary>List response.</summary>
public sealed class SeedlistListResponse
{
    [JsonPropertyName("items")] public List<Seedlist>? Items { get; init; }
}

/// <summary>Create seedlist request.</summary>
public sealed class CreateSeedlistRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("filter")] public Dictionary<string, object>? Filter { get; set; }
}

/// <summary>Update seedlist request.</summary>
public sealed class UpdateSeedlistRequest
{
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("filter")] public Dictionary<string, object>? Filter { get; set; }
}

/// <summary>An inbox placement test result.</summary>
public sealed class InboxPlacementResult
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("subject")] public string? Subject { get; init; }
    [JsonPropertyName("from")] public string? From { get; init; }
    [JsonPropertyName("placement")] public Dictionary<string, double>? Placement { get; init; }
    [JsonPropertyName("created_at")] public string? CreatedAt { get; init; }
    [JsonPropertyName("seedlist")] public string? Seedlist { get; init; }
}

/// <summary>List of results.</summary>
public sealed class InboxPlacementResultList
{
    [JsonPropertyName("items")] public List<InboxPlacementResult>? Items { get; init; }
}

/// <summary>Request to start an inbox placement test.</summary>
public sealed class CreateInboxPlacementTestRequest
{
    [JsonPropertyName("seedlist")] public string Seedlist { get; set; } = string.Empty;
    [JsonPropertyName("subject")] public string? Subject { get; set; }
    [JsonPropertyName("from_address")] public string? FromAddress { get; set; }
    [JsonPropertyName("from_name")] public string? FromName { get; set; }
}

/// <summary>List of placement providers.</summary>
public sealed class InboxPlacementProviderList
{
    [JsonPropertyName("items")] public List<Dictionary<string, object>>? Items { get; init; }
}

internal sealed class InboxPlacementService : IInboxPlacementService
{
    private readonly MailgunHttpClient _http;
    public InboxPlacementService(MailgunHttpClient http) => _http = http;

    public Task<SeedlistListResponse> ListSeedlistsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<SeedlistListResponse>("v4/inbox/seedlists", null, cancellationToken);

    public Task<Seedlist> GetSeedlistAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _http.GetJsonAsync<Seedlist>($"v4/inbox/seedlists/{PathEscape.Segment(name)}", null, cancellationToken);
    }

    public Task<Seedlist> CreateSeedlistAsync(CreateSeedlistRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        return _http.PostJsonBodyAsync<Seedlist>("v4/inbox/seedlists", request, cancellationToken);
    }

    public Task<Seedlist> UpdateSeedlistAsync(string name, UpdateSeedlistRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(request);
        return _http.PutJsonBodyAsync<Seedlist>($"v4/inbox/seedlists/{PathEscape.Segment(name)}", request, cancellationToken);
    }

    public Task DeleteSeedlistAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _http.DeleteNoResponseAsync($"v4/inbox/seedlists/{PathEscape.Segment(name)}", cancellationToken);
    }

    public Task<InboxPlacementResultList> ListResultsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<InboxPlacementResultList>("v4/inbox/results", null, cancellationToken);

    public Task<InboxPlacementResult> GetResultAsync(string resultId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultId);
        return _http.GetJsonAsync<InboxPlacementResult>($"v4/inbox/results/{PathEscape.Segment(resultId)}", null, cancellationToken);
    }

    public Task<InboxPlacementResult> CreateTestAsync(CreateInboxPlacementTestRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Seedlist);
        return _http.PostJsonBodyAsync<InboxPlacementResult>("v4/inbox/tests", request, cancellationToken);
    }

    public Task<InboxPlacementProviderList> ListProvidersAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<InboxPlacementProviderList>("v4/inbox/providers", null, cancellationToken);

    public Task DeleteResultAsync(string resultId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultId);
        return _http.DeleteNoResponseAsync($"v4/inbox/results/{PathEscape.Segment(resultId)}", cancellationToken);
    }

    public Task<InboxPlacementResultDetails> GetResultDetailsAsync(string resultId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultId);
        return _http.GetJsonAsync<InboxPlacementResultDetails>(
            $"v4/inbox/results/{PathEscape.Segment(resultId)}/details", null, cancellationToken);
    }

    public Task<InboxPlacementCounters> GetResultCountersAsync(string resultId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultId);
        return _http.GetJsonAsync<InboxPlacementCounters>(
            $"v4/inbox/results/{PathEscape.Segment(resultId)}/counters", null, cancellationToken);
    }

    public Task AddSeedAsync(string seedlistName, string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedlistName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return _http.PostJsonBodyNoResponseAsync(
            $"v4/inbox/seedlists/{PathEscape.Segment(seedlistName)}/seeds",
            new { email },
            cancellationToken);
    }

    public Task RemoveSeedAsync(string seedlistName, string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedlistName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return _http.DeleteNoResponseAsync(
            $"v4/inbox/seedlists/{PathEscape.Segment(seedlistName)}/seeds/{PathEscape.Segment(email)}",
            cancellationToken);
    }

    public Task<InboxPlacementResultList> ListResultsForSeedlistAsync(string seedlistName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedlistName);
        return _http.GetJsonAsync<InboxPlacementResultList>(
            $"v4/inbox/seedlists/{PathEscape.Segment(seedlistName)}/results", null, cancellationToken);
    }

    public Task<InboxPlacementResultList> FilterResultsAsync(InboxPlacementResultsFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var q = new QueryBuilder()
            .Add("subject", filter.Subject)
            .Add("from_address", filter.FromAddress)
            .Add("from_domain", filter.FromDomain)
            .Add("seedlist", filter.Seedlist)
            .Add("start_time", filter.StartTime)
            .Add("end_time", filter.EndTime)
            .Add("limit", filter.Limit)
            .Build();
        return _http.GetJsonAsync<InboxPlacementResultList>("v4/inbox/results/filter", q, cancellationToken);
    }
}
