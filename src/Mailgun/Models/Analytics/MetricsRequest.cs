using System.Globalization;
using System.Text.Json.Serialization;

namespace Mailgun.Models.Analytics;

/// <summary>
/// Helpers for formatting timestamps in the exact shape Mailgun's analytics endpoints accept.
/// The logs endpoint specifically rejects the <c>GMT</c> suffix that <see cref="DateTimeOffset"/>'s
/// <c>ToString("r")</c> produces — it requires a numeric offset like <c>-0000</c>.
/// </summary>
public static class AnalyticsTime
{
    /// <summary>
    /// Formats a <see cref="DateTimeOffset"/> as <c>"ddd, dd MMM yyyy HH:mm:ss -0000"</c> (RFC-2822 with
    /// numeric offset). Use this when assigning <see cref="LogsRequest.Start"/> / <see cref="LogsRequest.End"/> —
    /// the metrics + usage endpoints also accept this format, so it's the safe default everywhere.
    /// </summary>
    public static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss -0000", CultureInfo.InvariantCulture);
}

/// <summary>Parameters for <c>POST /v1/analytics/metrics</c>.</summary>
public sealed class MetricsRequest
{
    /// <summary>Inclusive start (RFC-2822/ISO 8601). Required.</summary>
    [JsonPropertyName("start")] public string? Start { get; set; }
    /// <summary>Inclusive end. Required.</summary>
    [JsonPropertyName("end")] public string? End { get; set; }
    /// <summary>Aggregate by — <c>1h</c>, <c>1d</c>, <c>1m</c>, etc.</summary>
    [JsonPropertyName("resolution")] public string? Resolution { get; set; }
    /// <summary>Dimensions to group by — e.g. <c>time</c>, <c>domain</c>, <c>tag</c>, <c>provider</c>.</summary>
    [JsonPropertyName("dimensions")] public List<string>? Dimensions { get; set; }
    /// <summary>Metrics to include — e.g. <c>accepted_count</c>, <c>delivered_count</c>, <c>failed_count</c>.</summary>
    [JsonPropertyName("metrics")] public List<string>? Metrics { get; set; }
    /// <summary>Where filter (Mailgun's documented analytics filter syntax).</summary>
    [JsonPropertyName("filter")] public object? Filter { get; set; }
    /// <summary>Include/exclude subaccount data.</summary>
    [JsonPropertyName("include_subaccounts")] public bool? IncludeSubaccounts { get; set; }
    /// <summary>Include account-level data.</summary>
    [JsonPropertyName("include_aggregates")] public bool? IncludeAggregates { get; set; }
    /// <summary>Pagination.</summary>
    [JsonPropertyName("pagination")] public AnalyticsPagination? Pagination { get; set; }
}

/// <summary>Pagination params for analytics endpoints.</summary>
public sealed class AnalyticsPagination
{
    [JsonPropertyName("skip")] public int Skip { get; set; }
    [JsonPropertyName("limit")] public int Limit { get; set; } = 100;
    [JsonPropertyName("sort")] public string? Sort { get; set; }
}

/// <summary>Response from <c>POST /v1/analytics/metrics</c>.</summary>
public sealed class MetricsResponse
{
    [JsonPropertyName("items")] public List<MetricsItem>? Items { get; init; }
    [JsonPropertyName("aggregates")] public MetricsItem? Aggregates { get; init; }
    [JsonPropertyName("start")] public string? Start { get; init; }
    [JsonPropertyName("end")] public string? End { get; init; }
    [JsonPropertyName("pagination")] public AnalyticsPaginationResult? Pagination { get; init; }
}

/// <summary>Mailgun's pagination response envelope.</summary>
public sealed class AnalyticsPaginationResult
{
    [JsonPropertyName("skip")] public int Skip { get; init; }
    [JsonPropertyName("limit")] public int Limit { get; init; }
    [JsonPropertyName("total")] public long Total { get; init; }
}

/// <summary>A single grouped row from an analytics metrics query.</summary>
public sealed class MetricsItem
{
    [JsonPropertyName("dimensions")] public List<MetricsDimension>? Dimensions { get; init; }
    /// <summary>
    /// Map of metric name → value. Values are <c>double?</c> because Mailgun may emit <c>null</c>
    /// for buckets with no data (e.g. zero-traffic time slices for derived rate metrics).
    /// </summary>
    [JsonPropertyName("metrics")] public Dictionary<string, double?>? Metrics { get; init; }
}

/// <summary>The dimension cell for one row.</summary>
public sealed class MetricsDimension
{
    [JsonPropertyName("dimension")] public string? Dimension { get; init; }
    [JsonPropertyName("value")] public string? Value { get; init; }
    [JsonPropertyName("display_value")] public string? DisplayValue { get; init; }
}

/// <summary>Parameters for <c>POST /v1/analytics/logs</c>.</summary>
public sealed class LogsRequest
{
    /// <summary>
    /// Inclusive start timestamp. Must be RFC-2822 with a NUMERIC offset (e.g. <c>"Mon, 18 May 2026 17:31:27 -0000"</c>);
    /// Mailgun's logs endpoint rejects the <c>GMT</c> textual zone that <see cref="DateTimeOffset"/>'s <c>"r"</c>
    /// format produces. Use <see cref="AnalyticsTime.Format"/> to format safely.
    /// </summary>
    [JsonPropertyName("start")] public string? Start { get; set; }

    /// <summary>Inclusive end timestamp; same format requirements as <see cref="Start"/>.</summary>
    [JsonPropertyName("end")] public string? End { get; set; }

    [JsonPropertyName("events")] public List<string>? Events { get; set; }
    [JsonPropertyName("filter")] public object? Filter { get; set; }
    [JsonPropertyName("include_subaccounts")] public bool? IncludeSubaccounts { get; set; }
    [JsonPropertyName("pagination")] public AnalyticsPagination? Pagination { get; set; }
}

/// <summary>Response from <c>POST /v1/analytics/logs</c>.</summary>
public sealed class LogsResponse
{
    [JsonPropertyName("items")] public List<LogEvent>? Items { get; init; }
    [JsonPropertyName("pagination")] public AnalyticsPaginationResult? Pagination { get; init; }
}

/// <summary>A single delivery/event log row from <c>POST /v1/analytics/logs</c>.</summary>
public sealed class LogEvent
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("event")] public string? Event { get; init; }
    [JsonPropertyName("timestamp")] public double? Timestamp { get; init; }
    [JsonPropertyName("message_id")] public string? MessageId { get; init; }
    [JsonPropertyName("recipient")] public string? Recipient { get; init; }
    [JsonPropertyName("domain")] public string? Domain { get; init; }
    [JsonPropertyName("subject")] public string? Subject { get; init; }
    [JsonPropertyName("tags")] public List<string>? Tags { get; init; }
    /// <summary>Mailgun <c>v:</c> custom variables — left as a dictionary because users put
    /// arbitrary JSON-shaped data here.</summary>
    [JsonPropertyName("variables")] public Dictionary<string, object>? Variables { get; init; }
    [JsonPropertyName("delivery_status")] public LogDeliveryStatus? DeliveryStatus { get; init; }
    [JsonPropertyName("envelope")] public LogEnvelope? Envelope { get; init; }
    [JsonPropertyName("flags")] public LogFlags? Flags { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
}

/// <summary>The remote MTA's delivery response captured by Mailgun for a single delivery attempt.</summary>
public sealed class LogDeliveryStatus
{
    /// <summary>SMTP code returned by the remote MTA (or Mailgun's internal classification code).</summary>
    [JsonPropertyName("code")] public int? Code { get; init; }
    /// <summary>Human-readable description of the delivery outcome.</summary>
    [JsonPropertyName("description")] public string? Description { get; init; }
    /// <summary>Verbatim SMTP message returned by the remote MTA.</summary>
    [JsonPropertyName("message")] public string? Message { get; init; }
    /// <summary>Mailgun's classification of the failure (e.g. <c>bounce</c>, <c>spam</c>).</summary>
    [JsonPropertyName("session-seconds")] public double? SessionSeconds { get; init; }
    /// <summary>Mailgun MTA that handled the delivery.</summary>
    [JsonPropertyName("mx-host")] public string? MxHost { get; init; }
    /// <summary>Number of delivery attempts so far for this recipient.</summary>
    [JsonPropertyName("attempt-no")] public int? AttemptNumber { get; init; }
    /// <summary>Mailgun-assigned bounce-classification code, if any.</summary>
    [JsonPropertyName("bounce_classification")] public string? BounceClassification { get; init; }
}

/// <summary>Envelope of the message as Mailgun saw it (sender, transport, etc.).</summary>
public sealed class LogEnvelope
{
    /// <summary>Envelope sender (the <c>MAIL FROM</c> address; may differ from the header <c>From</c>).</summary>
    [JsonPropertyName("sender")] public string? Sender { get; init; }
    /// <summary>Envelope <c>From</c> address.</summary>
    [JsonPropertyName("mail-from")] public string? MailFrom { get; init; }
    /// <summary>SMTP transport class (<c>smtp</c>, <c>local</c>, …).</summary>
    [JsonPropertyName("transport")] public string? Transport { get; init; }
    /// <summary>Target SMTP host.</summary>
    [JsonPropertyName("targets")] public string? Targets { get; init; }
}

/// <summary>Boolean flags describing the source message (Mailgun emits these on every log event).</summary>
public sealed class LogFlags
{
    [JsonPropertyName("is-routed")] public bool? IsRouted { get; init; }
    [JsonPropertyName("is-authenticated")] public bool? IsAuthenticated { get; init; }
    [JsonPropertyName("is-system-test")] public bool? IsSystemTest { get; init; }
    [JsonPropertyName("is-test-mode")] public bool? IsTestMode { get; init; }
    [JsonPropertyName("is-delayed-bounce")] public bool? IsDelayedBounce { get; init; }
    [JsonPropertyName("is-callback")] public bool? IsCallback { get; init; }
    [JsonPropertyName("is-encrypted")] public bool? IsEncrypted { get; init; }
}

/// <summary>Parameters for <c>POST /v1/analytics/usage/metrics</c>.</summary>
public sealed class UsageMetricsRequest
{
    [JsonPropertyName("start")] public string? Start { get; set; }
    [JsonPropertyName("end")] public string? End { get; set; }
    [JsonPropertyName("resolution")] public string? Resolution { get; set; }
    [JsonPropertyName("metrics")] public List<string>? Metrics { get; set; }
    [JsonPropertyName("include_subaccounts")] public bool? IncludeSubaccounts { get; set; }
    [JsonPropertyName("pagination")] public AnalyticsPagination? Pagination { get; set; }
}
