namespace Mailgun.Tests.TestHelpers;

/// <summary>
/// Factory for building a <see cref="MailgunClient"/> wired to a <see cref="MockHttpMessageHandler"/>.
/// The auth header is injected per-request inside <c>MailgunHttpClient.SendCoreAsync</c>, so the test
/// transport does not need to wrap an additional handler — anything reaching the mock has gone through
/// the full SDK pipeline (auth, OnBehalfOf, User-Agent).
/// </summary>
public static class TestMailgunClient
{
    public static (MailgunClient Client, MockHttpMessageHandler Handler) Create(
        string apiKey = "test-key",
        string baseUrl = "https://api.mailgun.test")
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
        };
        var options = new MailgunClientOptions
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            HttpClient = httpClient,
            MaxRetries = 0,
        };
        return (new MailgunClient(options), handler);
    }
}
