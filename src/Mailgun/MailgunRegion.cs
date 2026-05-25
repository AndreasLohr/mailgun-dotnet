namespace Mailgun;

/// <summary>
/// Mailgun deployment region. Determines the API base URL: <c>api.mailgun.net</c> for
/// <see cref="Us"/> and <c>api.eu.mailgun.net</c> for <see cref="Eu"/>.
/// </summary>
public enum MailgunRegion
{
    /// <summary>United States region (default) — <c>https://api.mailgun.net/</c>.</summary>
    Us = 0,

    /// <summary>European Union region — <c>https://api.eu.mailgun.net/</c>.</summary>
    Eu = 1,
}
