using System.Text.Json.Serialization;

namespace Mailgun.Internal;

/// <summary>
/// Mailgun's error response envelope. The dominant shape is <c>{"message":"..."}</c> but some
/// endpoints add <c>details</c> or <c>errors</c>. Either string or array is accepted; the
/// SDK normalizes both into the typed exception's <c>Details</c> list.
/// </summary>
/// <remarks>
/// <see cref="Message"/> and <see cref="MessageCapital"/> intentionally bind two different JSON
/// property names — Mailgun emits lower-case <c>message</c> from most endpoints and capital
/// <c>Message</c> from a few v1 ones. This works because
/// <see cref="Mailgun.Serialization.MailgunJsonOptions.Default"/> sets
/// <c>PropertyNameCaseInsensitive = false</c>. If that flag is ever flipped to <c>true</c>, both
/// properties will collide on the same case-insensitive name and one will silently shadow the
/// other. Keep the option false, or refactor this DTO with a custom converter.
/// </remarks>
internal sealed class MailgunErrorResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("Message")]
    public string? MessageCapital { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("details")]
    public object? Details { get; set; }

    [JsonPropertyName("errors")]
    public object? Errors { get; set; }
}
