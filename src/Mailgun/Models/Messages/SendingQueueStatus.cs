using System.Text.Json.Serialization;

namespace Mailgun.Models.Messages;

/// <summary>Response from <c>GET /v3/domains/{domain}/sending_queues</c>.</summary>
public sealed class SendingQueueStatus
{
    [JsonPropertyName("regular")] public QueueInfo? Regular { get; init; }
    [JsonPropertyName("scheduled")] public QueueInfo? Scheduled { get; init; }
}

/// <summary>Mailgun sending queue statistics for one of {regular, scheduled}.</summary>
public sealed class QueueInfo
{
    [JsonPropertyName("is_disabled")] public bool? IsDisabled { get; init; }
    [JsonPropertyName("disabled")] public QueueDisabledInfo? Disabled { get; init; }
}

/// <summary>Mailgun disabled-queue details.</summary>
public sealed class QueueDisabledInfo
{
    [JsonPropertyName("until")] public string? Until { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
}
