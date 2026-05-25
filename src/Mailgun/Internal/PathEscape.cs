namespace Mailgun.Internal;

internal static class PathEscape
{
    /// <summary>URL-encodes a value to be embedded as a single path segment (e.g. domain or email).</summary>
    public static string Segment(string value) =>
        Uri.EscapeDataString(value ?? throw new ArgumentNullException(nameof(value)));
}
