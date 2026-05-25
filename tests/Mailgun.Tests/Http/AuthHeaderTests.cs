using System.Net;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Http;

public class AuthHeaderTests
{
    [Fact]
    public async Task Every_request_carries_Basic_authorization_header_with_api_user_and_key()
    {
        var (client, handler) = TestMailgunClient.Create(apiKey: "key-secret");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        _ = await client.Routes.ListAsync();

        var req = Assert.Single(handler.Requests);
        Assert.True(req.Headers.ContainsKey("Authorization"), "Authorization header missing");
        var header = req.Headers["Authorization"];
        Assert.StartsWith("Basic ", header, StringComparison.Ordinal);
        var token = header["Basic ".Length..];
        var decoded = System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(token));
        Assert.Equal("api:key-secret", decoded);
    }

    [Fact]
    public async Task Auth_header_is_attached_when_caller_supplies_their_own_HttpClient()
    {
        // Regression: when a caller injects an HttpClient (e.g. via IHttpClientFactory + AddMailgun),
        // the SDK must still authenticate. Auth is per-request, not via a DelegatingHandler.
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");
        var injected = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "injected-key",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = injected,
        });

        _ = await client.Routes.ListAsync();

        var req = Assert.Single(handler.Requests);
        var token = req.Headers["Authorization"]["Basic ".Length..];
        Assert.Equal("api:injected-key", System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(token)));
    }

    [Fact]
    public async Task User_agent_includes_sdk_identifier_and_optional_suffix()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");
        var injected = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = injected,
            UserAgent = "myapp/1.0",
        });

        _ = await client.Routes.ListAsync();

        var req = Assert.Single(handler.Requests);
        var ua = req.Headers["User-Agent"];
        Assert.Contains("Mailgun-DotNet", ua, StringComparison.Ordinal);
        Assert.Contains("myapp/1.0", ua, StringComparison.Ordinal);
    }
}
