using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;

namespace Mailgun.Services;

/// <summary>
/// Operations on <c>/v4/address/validate</c>, <c>/v4/address/validate/bulk/{listId}</c>, and
/// <c>/v4/address/validate/bulk/preview</c>.
/// </summary>
public interface IValidateService
{
    /// <summary><c>GET /v4/address/validate</c> — validate a single address.</summary>
    Task<EmailValidationResult> ValidateAsync(string address, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v4/address/validate/bulk/{listId}</c> — upload a CSV for bulk validation (max 25 MB; 5 concurrent jobs).</summary>
    Task<BulkValidationJob> CreateBulkAsync(string listId, Stream csvStream, string fileName = "addresses.csv", CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/address/validate/bulk/{listId}</c> — get bulk job status.</summary>
    Task<BulkValidationJob> GetBulkAsync(string listId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/address/validate/bulk</c> — list bulk jobs.</summary>
    Task<BulkValidationJobList> ListBulkAsync(CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v4/address/validate/bulk/{listId}</c> — cancel/delete a bulk job.</summary>
    Task DeleteBulkAsync(string listId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /v4/address/validate/bulk/preview/{listId}</c> — generate a bulk preview.</summary>
    Task<BulkPreview> CreateBulkPreviewAsync(string listId, Stream csvStream, string fileName = "addresses.csv", CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/address/validate/bulk/preview/{listId}</c> — get bulk preview.</summary>
    Task<BulkPreview> GetBulkPreviewAsync(string listId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v4/address/validate/bulk/preview</c> — list previews.</summary>
    Task<BulkPreviewList> ListBulkPreviewsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v4/address/validate/bulk/preview/{listId}</c> — delete a preview.</summary>
    Task DeleteBulkPreviewAsync(string listId, CancellationToken cancellationToken = default);
}

/// <summary>Mailgun's single-address validation result.</summary>
public sealed class EmailValidationResult
{
    [JsonPropertyName("address")] public string? Address { get; init; }
    [JsonPropertyName("is_valid")] public bool? IsValid { get; init; }
    [JsonPropertyName("is_disposable_address")] public bool? IsDisposableAddress { get; init; }
    [JsonPropertyName("is_role_address")] public bool? IsRoleAddress { get; init; }
    [JsonPropertyName("reason")] public List<string>? Reason { get; init; }
    [JsonPropertyName("result")] public string? Result { get; init; }
    [JsonPropertyName("risk")] public string? Risk { get; init; }
    [JsonPropertyName("did_you_mean")] public string? DidYouMean { get; init; }
    [JsonPropertyName("engagement")] public Dictionary<string, object>? Engagement { get; init; }
    [JsonPropertyName("envelope")] public Dictionary<string, object>? Envelope { get; init; }
    [JsonPropertyName("root_address")] public string? RootAddress { get; init; }
}

/// <summary>Bulk validation job status.</summary>
public sealed class BulkValidationJob
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("quantity")] public long? Quantity { get; init; }
    [JsonPropertyName("valid_count")] public long? ValidCount { get; init; }
    [JsonPropertyName("risky_count")] public long? RiskyCount { get; init; }
    [JsonPropertyName("invalid_count")] public long? InvalidCount { get; init; }
    [JsonPropertyName("unknown_count")] public long? UnknownCount { get; init; }
    [JsonPropertyName("download_url")] public Dictionary<string, string>? DownloadUrl { get; init; }
    [JsonPropertyName("created_at")] public string? CreatedAt { get; init; }
}

/// <summary>List of bulk jobs.</summary>
public sealed class BulkValidationJobList
{
    [JsonPropertyName("jobs")] public List<BulkValidationJob>? Jobs { get; init; }
    [JsonPropertyName("paging")] public Pagination.PagingLinks? Paging { get; init; }
}

/// <summary>Bulk preview result.</summary>
public sealed class BulkPreview
{
    [JsonPropertyName("preview")] public Dictionary<string, object>? Preview { get; init; }
}

/// <summary>List of previews.</summary>
public sealed class BulkPreviewList
{
    [JsonPropertyName("previews")] public List<BulkPreview>? Previews { get; init; }
}

internal sealed class ValidateService : IValidateService
{
    private readonly MailgunHttpClient _http;
    public ValidateService(MailgunHttpClient http) => _http = http;

    public Task<EmailValidationResult> ValidateAsync(string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var q = new QueryBuilder().Add("address", address).Build();
        return _http.GetJsonAsync<EmailValidationResult>("v4/address/validate", q, cancellationToken,
            routeTemplate: "v4/address/validate");
    }

    public async Task<BulkValidationJob> CreateBulkAsync(string listId, Stream csvStream, string fileName = "addresses.csv", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);
        ArgumentNullException.ThrowIfNull(csvStream);
        using var mp = new MultipartBuilder().AddFile("file", fileName, csvStream, "text/csv");
        return await _http.PostMultipartAsync<BulkValidationJob>($"v4/address/validate/bulk/{PathEscape.Segment(listId)}", mp, cancellationToken,
            routeTemplate: "v4/address/validate/bulk/{list_id}").ConfigureAwait(false);
    }

    public Task<BulkValidationJob> GetBulkAsync(string listId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);
        return _http.GetJsonAsync<BulkValidationJob>($"v4/address/validate/bulk/{PathEscape.Segment(listId)}", null, cancellationToken,
            routeTemplate: "v4/address/validate/bulk/{list_id}");
    }

    public Task<BulkValidationJobList> ListBulkAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<BulkValidationJobList>("v4/address/validate/bulk", null, cancellationToken,
            routeTemplate: "v4/address/validate/bulk");

    public Task DeleteBulkAsync(string listId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);
        return _http.DeleteNoResponseAsync($"v4/address/validate/bulk/{PathEscape.Segment(listId)}", cancellationToken,
            routeTemplate: "v4/address/validate/bulk/{list_id}");
    }

    public async Task<BulkPreview> CreateBulkPreviewAsync(string listId, Stream csvStream, string fileName = "addresses.csv", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);
        ArgumentNullException.ThrowIfNull(csvStream);
        using var mp = new MultipartBuilder().AddFile("file", fileName, csvStream, "text/csv");
        return await _http.PostMultipartAsync<BulkPreview>($"v4/address/validate/bulk/preview/{PathEscape.Segment(listId)}", mp, cancellationToken,
            routeTemplate: "v4/address/validate/bulk/preview/{list_id}").ConfigureAwait(false);
    }

    public Task<BulkPreview> GetBulkPreviewAsync(string listId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);
        return _http.GetJsonAsync<BulkPreview>($"v4/address/validate/bulk/preview/{PathEscape.Segment(listId)}", null, cancellationToken,
            routeTemplate: "v4/address/validate/bulk/preview/{list_id}");
    }

    public Task<BulkPreviewList> ListBulkPreviewsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<BulkPreviewList>("v4/address/validate/bulk/preview", null, cancellationToken,
            routeTemplate: "v4/address/validate/bulk/preview");

    public Task DeleteBulkPreviewAsync(string listId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);
        return _http.DeleteNoResponseAsync($"v4/address/validate/bulk/preview/{PathEscape.Segment(listId)}", cancellationToken,
            routeTemplate: "v4/address/validate/bulk/preview/{list_id}");
    }
}
