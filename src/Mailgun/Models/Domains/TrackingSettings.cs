using System.Text.Json.Serialization;

namespace Mailgun.Models.Domains;

/// <summary>Domain-wide tracking settings (open, click, unsubscribe).</summary>
public sealed class TrackingSettings
{
    [JsonPropertyName("open")] public OpenTrackingSettings? Open { get; init; }
    [JsonPropertyName("click")] public ClickTrackingSettings? Click { get; init; }
    [JsonPropertyName("unsubscribe")] public UnsubscribeTrackingSettings? Unsubscribe { get; init; }
}

/// <summary>Open-tracking config.</summary>
public sealed class OpenTrackingSettings
{
    [JsonPropertyName("active")] public bool Active { get; init; }
    [JsonPropertyName("place_at_the_top")] public bool? PlaceAtTheTop { get; init; }
}

/// <summary>Click-tracking config. <c>active</c> may be a string: <c>"yes" | "no" | "htmlonly"</c>.</summary>
public sealed class ClickTrackingSettings
{
    [JsonPropertyName("active")] public string? Active { get; init; }
}

/// <summary>Unsubscribe-tracking config.</summary>
public sealed class UnsubscribeTrackingSettings
{
    [JsonPropertyName("active")] public bool Active { get; init; }
    [JsonPropertyName("html_footer")] public string? HtmlFooter { get; init; }
    [JsonPropertyName("text_footer")] public string? TextFooter { get; init; }
}
