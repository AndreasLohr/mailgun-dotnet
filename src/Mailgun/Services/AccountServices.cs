using System.Text.Json.Serialization;
using Mailgun.Http;
using Mailgun.Internal;
using Mailgun.Pagination;

namespace Mailgun.Services;

/// <summary>Operations on <c>/v5/accounts/subaccounts</c>.</summary>
public interface ISubaccountsService
{
    Task<SubaccountListResponse> ListAsync(int? limit = null, int? skip = null, string? filter = null, CancellationToken cancellationToken = default);
    Task<Subaccount> GetAsync(string subaccountId, CancellationToken cancellationToken = default);

    /// <summary>Convenience overload that creates a subaccount with just a name.</summary>
    Task<Subaccount> CreateAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Creates a subaccount from a typed request DTO.</summary>
    Task<Subaccount> CreateAsync(CreateSubaccountRequest request, CancellationToken cancellationToken = default);

    /// <summary>Convenience overload that updates only the subaccount's name.</summary>
    Task<Subaccount> UpdateAsync(string subaccountId, string name, CancellationToken cancellationToken = default);

    /// <summary>Updates a subaccount from a typed request DTO.</summary>
    Task<Subaccount> UpdateAsync(string subaccountId, UpdateSubaccountRequest request, CancellationToken cancellationToken = default);

    Task EnableAsync(string subaccountId, CancellationToken cancellationToken = default);
    Task DisableAsync(string subaccountId, CancellationToken cancellationToken = default);
    Task<SubaccountFeatures> GetFeaturesAsync(string subaccountId, CancellationToken cancellationToken = default);
    Task<SubaccountFeatures> UpdateFeaturesAsync(string subaccountId, SubaccountFeatures features, CancellationToken cancellationToken = default);
    Task<CustomLimit> GetMonthlyCustomLimitAsync(string subaccountId, CancellationToken cancellationToken = default);
    Task SetMonthlyCustomLimitAsync(string subaccountId, long limit, CancellationToken cancellationToken = default);
}

/// <summary>
/// Parameters for <c>POST /v5/accounts/subaccounts</c>. The <see cref="Name"/> property maps to the
/// JSON field <c>name</c> via <see cref="JsonPropertyNameAttribute"/>, so refactoring the C# property
/// name (e.g. for a future <c>DisplayName</c>) cannot silently break the wire format.
/// </summary>
public sealed class CreateSubaccountRequest
{
    /// <summary>Required. Human-readable name for the subaccount.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

/// <summary>Parameters for <c>PUT /v5/accounts/subaccounts/{id}</c>.</summary>
public sealed class UpdateSubaccountRequest
{
    /// <summary>Required. New name for the subaccount.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

/// <summary>A Mailgun subaccount.</summary>
public sealed class Subaccount
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("limit")] public long? Limit { get; init; }
    [JsonPropertyName("usage")] public long? Usage { get; init; }
}

/// <summary>List response.</summary>
public sealed class SubaccountListResponse
{
    [JsonPropertyName("subaccounts")] public List<Subaccount>? Subaccounts { get; init; }
    [JsonPropertyName("total")] public long? Total { get; init; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; init; }
}

/// <summary>Per-subaccount feature toggles.</summary>
public sealed class SubaccountFeatures
{
    [JsonPropertyName("features")] public Dictionary<string, bool>? Features { get; set; }
}

internal sealed class SubaccountsService : ISubaccountsService
{
    private readonly MailgunHttpClient _http;
    public SubaccountsService(MailgunHttpClient http) => _http = http;

    public Task<SubaccountListResponse> ListAsync(int? limit = null, int? skip = null, string? filter = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder().Add("limit", limit).Add("skip", skip).Add("filter", filter).Build();
        return _http.GetJsonAsync<SubaccountListResponse>("v5/accounts/subaccounts", q, cancellationToken, routeTemplate: "v5/accounts/subaccounts");
    }

    public Task<Subaccount> GetAsync(string subaccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subaccountId);
        return _http.GetJsonAsync<Subaccount>($"v5/accounts/subaccounts/{PathEscape.Segment(subaccountId)}", null, cancellationToken, routeTemplate: "v5/accounts/subaccounts/{subaccount_id}");
    }

    public Task<Subaccount> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return CreateAsync(new CreateSubaccountRequest { Name = name }, cancellationToken);
    }

    public Task<Subaccount> CreateAsync(CreateSubaccountRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        return _http.PostJsonBodyAsync<Subaccount>("v5/accounts/subaccounts", request, cancellationToken, routeTemplate: "v5/accounts/subaccounts");
    }

    public Task<Subaccount> UpdateAsync(string subaccountId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subaccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return UpdateAsync(subaccountId, new UpdateSubaccountRequest { Name = name }, cancellationToken);
    }

    public Task<Subaccount> UpdateAsync(string subaccountId, UpdateSubaccountRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subaccountId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        return _http.PutJsonBodyAsync<Subaccount>($"v5/accounts/subaccounts/{PathEscape.Segment(subaccountId)}", request, cancellationToken, routeTemplate: "v5/accounts/subaccounts/{subaccount_id}");
    }

    public Task EnableAsync(string subaccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subaccountId);
        return _http.PostJsonBodyNoResponseAsync($"v5/accounts/subaccounts/{PathEscape.Segment(subaccountId)}/enable", new { }, cancellationToken, routeTemplate: "v5/accounts/subaccounts/{subaccount_id}/enable");
    }

    public Task DisableAsync(string subaccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subaccountId);
        return _http.PostJsonBodyNoResponseAsync($"v5/accounts/subaccounts/{PathEscape.Segment(subaccountId)}/disable", new { }, cancellationToken, routeTemplate: "v5/accounts/subaccounts/{subaccount_id}/disable");
    }

    public Task<SubaccountFeatures> GetFeaturesAsync(string subaccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subaccountId);
        return _http.GetJsonAsync<SubaccountFeatures>($"v5/accounts/subaccounts/{PathEscape.Segment(subaccountId)}/features", null, cancellationToken, routeTemplate: "v5/accounts/subaccounts/{subaccount_id}/features");
    }

    public Task<SubaccountFeatures> UpdateFeaturesAsync(string subaccountId, SubaccountFeatures features, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subaccountId);
        ArgumentNullException.ThrowIfNull(features);
        return _http.PutJsonBodyAsync<SubaccountFeatures>($"v5/accounts/subaccounts/{PathEscape.Segment(subaccountId)}/features", features, cancellationToken, routeTemplate: "v5/accounts/subaccounts/{subaccount_id}/features");
    }

    public Task<CustomLimit> GetMonthlyCustomLimitAsync(string subaccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subaccountId);
        return _http.GetJsonAsync<CustomLimit>($"v5/accounts/subaccounts/{PathEscape.Segment(subaccountId)}/limit/custom/monthly", null, cancellationToken, routeTemplate: "v5/accounts/subaccounts/{subaccount_id}/limit/custom/monthly");
    }

    public Task SetMonthlyCustomLimitAsync(string subaccountId, long limit, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subaccountId);
        return _http.PutJsonBodyNoResponseAsync($"v5/accounts/subaccounts/{PathEscape.Segment(subaccountId)}/limit/custom/monthly", new { limit }, cancellationToken, routeTemplate: "v5/accounts/subaccounts/{subaccount_id}/limit/custom/monthly");
    }
}

/// <summary>Operations on <c>/v5/accounts/limit/custom/monthly</c>.</summary>
public interface ICustomMessageLimitService
{
    Task<CustomLimit> GetAsync(CancellationToken cancellationToken = default);
    Task SetAsync(long limit, CancellationToken cancellationToken = default);
    Task EnableAsync(CancellationToken cancellationToken = default);
    Task DisableAsync(CancellationToken cancellationToken = default);
}

/// <summary>Custom monthly message limit.</summary>
public sealed class CustomLimit
{
    [JsonPropertyName("limit")] public long? Limit { get; init; }
    [JsonPropertyName("enabled")] public bool? Enabled { get; init; }
    [JsonPropertyName("usage")] public long? Usage { get; init; }
}

internal sealed class CustomMessageLimitService : ICustomMessageLimitService
{
    private readonly MailgunHttpClient _http;
    public CustomMessageLimitService(MailgunHttpClient http) => _http = http;

    public Task<CustomLimit> GetAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<CustomLimit>("v5/accounts/limit/custom/monthly", null, cancellationToken, routeTemplate: "v5/accounts/limit/custom/monthly");

    public Task SetAsync(long limit, CancellationToken cancellationToken = default) =>
        _http.PutJsonBodyNoResponseAsync("v5/accounts/limit/custom/monthly", new { limit }, cancellationToken, routeTemplate: "v5/accounts/limit/custom/monthly");

    public Task EnableAsync(CancellationToken cancellationToken = default) =>
        _http.PostJsonBodyNoResponseAsync("v5/accounts/limit/custom/monthly/enable", new { }, cancellationToken, routeTemplate: "v5/accounts/limit/custom/monthly/enable");

    public Task DisableAsync(CancellationToken cancellationToken = default) =>
        _http.PostJsonBodyNoResponseAsync("v5/accounts/limit/custom/monthly/disable", new { }, cancellationToken, routeTemplate: "v5/accounts/limit/custom/monthly/disable");
}

/// <summary>Operations on <c>/v5/accounts</c>.</summary>
public interface IAccountService
{
    /// <summary><c>PUT /v5/accounts</c> — update the current account.</summary>
    Task<Account> UpdateAsync(Account updates, CancellationToken cancellationToken = default);

    /// <summary><c>GET /v5/accounts/http_signing_key</c> — fetch the HTTP webhook signing key.</summary>
    Task<HttpSigningKey> GetHttpSigningKeyAsync(CancellationToken cancellationToken = default);

    /// <summary><c>POST /v5/accounts/http_signing_key</c> — rotate the HTTP webhook signing key.</summary>
    Task<HttpSigningKey> RotateHttpSigningKeyAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /v5/accounts/features</c> — list account feature flags.</summary>
    Task<Dictionary<string, bool>> GetFeaturesAsync(CancellationToken cancellationToken = default);

    /// <summary><c>POST /v5/accounts/resend_activation_email</c> — re-send the activation email.</summary>
    Task ResendActivationEmailAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /v5/sandbox/auth_recipients</c> — list sandbox authorized recipients.</summary>
    Task<SandboxAuthRecipientsList> ListSandboxAuthRecipientsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>POST /v5/sandbox/auth_recipients</c> — add a sandbox authorized recipient.</summary>
    Task AddSandboxAuthRecipientAsync(string email, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /v5/sandbox/auth_recipients/{email}</c> — remove a sandbox authorized recipient.</summary>
    Task RemoveSandboxAuthRecipientAsync(string email, CancellationToken cancellationToken = default);
}

/// <summary>The current Mailgun account.</summary>
public sealed class Account
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("plan")] public string? Plan { get; set; }
}

/// <summary>HTTP webhook signing key.</summary>
public sealed class HttpSigningKey
{
    [JsonPropertyName("http_signing_key")] public string Key { get; init; } = string.Empty;
    [JsonPropertyName("created_at")] public string? CreatedAt { get; init; }
}

/// <summary>List of sandbox authorized recipients.</summary>
public sealed class SandboxAuthRecipientsList
{
    [JsonPropertyName("items")] public List<Dictionary<string, object>>? Items { get; init; }
}

internal sealed class AccountService : IAccountService
{
    private readonly MailgunHttpClient _http;
    public AccountService(MailgunHttpClient http) => _http = http;

    public Task<Account> UpdateAsync(Account updates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        return _http.PutJsonBodyAsync<Account>("v5/accounts", updates, cancellationToken, routeTemplate: "v5/accounts");
    }

    public Task<HttpSigningKey> GetHttpSigningKeyAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<HttpSigningKey>("v5/accounts/http_signing_key", null, cancellationToken, routeTemplate: "v5/accounts/http_signing_key");

    public Task<HttpSigningKey> RotateHttpSigningKeyAsync(CancellationToken cancellationToken = default) =>
        _http.PostJsonBodyAsync<HttpSigningKey>("v5/accounts/http_signing_key", new { }, cancellationToken, routeTemplate: "v5/accounts/http_signing_key");

    public Task<Dictionary<string, bool>> GetFeaturesAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<Dictionary<string, bool>>("v5/accounts/features", null, cancellationToken, routeTemplate: "v5/accounts/features");

    public Task ResendActivationEmailAsync(CancellationToken cancellationToken = default) =>
        _http.PostJsonBodyNoResponseAsync("v5/accounts/resend_activation_email", new { }, cancellationToken, routeTemplate: "v5/accounts/resend_activation_email");

    public Task<SandboxAuthRecipientsList> ListSandboxAuthRecipientsAsync(CancellationToken cancellationToken = default) =>
        _http.GetJsonAsync<SandboxAuthRecipientsList>("v5/sandbox/auth_recipients", null, cancellationToken, routeTemplate: "v5/sandbox/auth_recipients");

    public Task AddSandboxAuthRecipientAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return _http.PostJsonBodyNoResponseAsync("v5/sandbox/auth_recipients", new { email }, cancellationToken, routeTemplate: "v5/sandbox/auth_recipients");
    }

    public Task RemoveSandboxAuthRecipientAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return _http.DeleteNoResponseAsync($"v5/sandbox/auth_recipients/{PathEscape.Segment(email)}", cancellationToken, routeTemplate: "v5/sandbox/auth_recipients/{email}");
    }
}

/// <summary>Operations on <c>/v5/users</c>.</summary>
public interface IUsersService
{
    Task<UserList> ListAsync(int? limit = null, int? skip = null, CancellationToken cancellationToken = default);
    Task<MailgunUser> GetAsync(string userId, CancellationToken cancellationToken = default);
    Task<MailgunUser> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<MailgunUser> UpdateAsync(string userId, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>A Mailgun RBAC user.</summary>
public sealed class MailgunUser
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("role")] public string? Role { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
}

/// <summary>List response.</summary>
public sealed class UserList
{
    [JsonPropertyName("items")] public List<MailgunUser>? Items { get; init; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; init; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; init; }
}

/// <summary>Create-user request.</summary>
public sealed class CreateUserRequest
{
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

/// <summary>Update-user request.</summary>
public sealed class UpdateUserRequest
{
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
}

internal sealed class UsersService : IUsersService
{
    private readonly MailgunHttpClient _http;
    public UsersService(MailgunHttpClient http) => _http = http;

    public Task<UserList> ListAsync(int? limit = null, int? skip = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryBuilder().Add("limit", limit).Add("skip", skip).Build();
        return _http.GetJsonAsync<UserList>("v5/users", q, cancellationToken, routeTemplate: "v5/users");
    }

    public Task<MailgunUser> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _http.GetJsonAsync<MailgunUser>($"v5/users/{PathEscape.Segment(userId)}", null, cancellationToken, routeTemplate: "v5/users/{user_id}");
    }

    public Task<MailgunUser> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        return _http.PostJsonBodyAsync<MailgunUser>("v5/users", request, cancellationToken, routeTemplate: "v5/users");
    }

    public Task<MailgunUser> UpdateAsync(string userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(request);
        return _http.PutJsonBodyAsync<MailgunUser>($"v5/users/{PathEscape.Segment(userId)}", request, cancellationToken, routeTemplate: "v5/users/{user_id}");
    }

    public Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _http.DeleteNoResponseAsync($"v5/users/{PathEscape.Segment(userId)}", cancellationToken, routeTemplate: "v5/users/{user_id}");
    }
}
