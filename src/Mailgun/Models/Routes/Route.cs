using System.Text.Json.Serialization;
using Mailgun.Pagination;
using Mailgun.Serialization;

namespace Mailgun.Models.Routes;

/// <summary>A Mailgun routing rule (<c>/v3/routes</c>).</summary>
public sealed class Route
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("priority")] public int? Priority { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("expression")] public string? Expression { get; init; }
    [JsonPropertyName("actions")] public List<string>? Actions { get; init; }
    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(Rfc2822DateTimeOffsetConverter))]
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>Parameters for <c>POST /v3/routes</c>.</summary>
public sealed class CreateRouteRequest
{
    public int? Priority { get; set; }
    public string? Description { get; set; }
    public string Expression { get; set; } = string.Empty;
    public List<string> Actions { get; } = new();
}

/// <summary>Parameters for <c>PUT /v3/routes/{id}</c>.</summary>
public sealed class UpdateRouteRequest
{
    public int? Priority { get; set; }
    public string? Description { get; set; }
    public string? Expression { get; set; }
    public List<string> Actions { get; } = new();
}

/// <summary>Result of <c>POST /v3/routes/match</c>.</summary>
public sealed class RouteMatchResult
{
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("matched")] public List<Route>? Matched { get; init; }
}

internal sealed class RouteListEnvelope
{
    [JsonPropertyName("items")] public List<Route>? Items { get; set; }
    [JsonPropertyName("paging")] public PagingLinks? Paging { get; set; }
    [JsonPropertyName("total_count")] public long? TotalCount { get; set; }
}

internal sealed class RouteSingleEnvelope
{
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("route")] public Route Route { get; set; } = new();
}
