using System.Net;
using System.Reflection;
using Mailgun.Exceptions;
using Mailgun.Extensions.DependencyInjection;
using Mailgun.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Mailgun.Tests.DependencyInjection;

/// <summary>
/// Proves direct <see cref="MailgunClient"/> construction and DI-based construction honor the same
/// option values — closing the recurring class of bug where a newly-added <see cref="MailgunClientOptions"/>
/// field was forgotten in the DI projection (OnResponse, then AllowInsecureBaseUrl + MaxResponseContentBytes).
/// </summary>
public class OptionsParityTests
{
    private sealed class CannedHandler : HttpMessageHandler
    {
        private readonly string _body;
        public CannedHandler(string body) => _body = body;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json"),
            });
    }

    private static IServiceProvider Build(Action<MailgunClientOptions> configure, HttpMessageHandler primaryHandler)
    {
        var services = new ServiceCollection();
        services.AddMailgun(configure);
        services.ConfigureAll<HttpClientFactoryOptions>(o =>
        {
            o.HttpMessageHandlerBuilderActions.Add(b =>
            {
                if (b.Name == MailgunServiceCollectionExtensions.HttpClientName)
                    b.PrimaryHandler = primaryHandler;
            });
        });
        return services.BuildServiceProvider();
    }

    // ── Structural guard: the clone must carry EVERY settable option ──────────────────────────

    [Fact]
    public void CloneWithHttpClient_propagates_every_settable_option()
    {
        // Set every public settable property (except HttpClient, which the clone overrides) to a
        // distinct non-default value, then assert the clone preserved each. MemberwiseClone makes
        // this inherently complete; the test locks the behavior so a future refactor to a manual
        // copy can't silently drop a field.
        var http = new HttpClient();
        var original = new MailgunClientOptions
        {
            ApiKey = "key-123",
            Region = MailgunRegion.Eu,
            BaseUrl = "https://custom.example.test",
            Timeout = TimeSpan.FromSeconds(42),
            MaxRetries = 7,
            UserAgent = "myapp/9.9",
            OnBehalfOf = "acct_parity",
            OnResponse = _ => { },
            AllowInsecureBaseUrl = true,
            MaxResponseContentBytes = 12345,
        };

        var clone = InvokeCloneWithHttpClient(original, http);

        var settable = typeof(MailgunClientOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

        foreach (var prop in settable)
        {
            if (prop.Name == nameof(MailgunClientOptions.HttpClient))
            {
                Assert.Same(http, prop.GetValue(clone));
                continue;
            }
            Assert.Equal(prop.GetValue(original), prop.GetValue(clone));
        }

        // And it must be a distinct instance (mutating the clone can't leak back to the shared
        // IOptions singleton).
        Assert.NotSame(original, clone);
    }

    private static MailgunClientOptions InvokeCloneWithHttpClient(MailgunClientOptions options, HttpClient http)
    {
        var method = typeof(MailgunClientOptions).GetMethod(
            "CloneWithHttpClient", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        return (MailgunClientOptions)method!.Invoke(options, new object[] { http })!;
    }

    // ── DI honors AllowInsecureBaseUrl (would throw at construction without it) ────────────────

    [Fact]
    public void Di_honors_AllowInsecureBaseUrl_optin()
    {
        using var sp = (ServiceProvider)Build(
            o =>
            {
                o.ApiKey = "k";
                o.BaseUrl = "http://gateway.internal";
                o.AllowInsecureBaseUrl = true;
            },
            new CannedHandler("{}"));

        // Resolving constructs MailgunClient; without the option propagating, the ctor throws on the
        // non-HTTPS base URL. A clean resolve proves the flag reached the client.
        var client = sp.GetRequiredService<IMailgunClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void Di_without_AllowInsecureBaseUrl_still_rejects_http_base_url()
    {
        using var sp = (ServiceProvider)Build(
            o =>
            {
                o.ApiKey = "k";
                o.BaseUrl = "http://gateway.internal";
            },
            new CannedHandler("{}"));

        // Parity with direct construction: the same http:// base URL must be rejected.
        Assert.Throws<ArgumentException>(() => sp.GetRequiredService<IMailgunClient>());
    }

    // ── DI honors MaxResponseContentBytes ─────────────────────────────────────────────────────

    [Fact]
    public async Task Di_honors_MaxResponseContentBytes()
    {
        using var sp = (ServiceProvider)Build(
            o =>
            {
                o.ApiKey = "k";
                o.BaseUrl = "https://api.mailgun.test";
                o.MaxResponseContentBytes = 100;
            },
            new CannedHandler(new string('x', 2000)));

        var client = sp.GetRequiredService<IMailgunClient>();
        // The 2000-byte body exceeds the 100-byte cap → MailgunSerializationException, proving the
        // cap propagated through DI (the default 64 MiB would have let the body through to a JSON
        // parse error instead).
        await Assert.ThrowsAsync<MailgunSerializationException>(() => client.Domains.ListAsync());
    }

    [Fact]
    public async Task Direct_and_di_construction_agree_on_max_response_cap_behavior()
    {
        // Direct construction with the same options + handler.
        var directHandler = new MockHttpMessageHandler();
        directHandler.EnqueueResponse(HttpStatusCode.OK, new string('x', 2000));
        using var directHttp = new HttpClient(directHandler) { BaseAddress = new Uri("https://api.mailgun.test/") };
        using var directClient = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = directHttp,
            MaxResponseContentBytes = 100,
        });
        await Assert.ThrowsAsync<MailgunSerializationException>(() => directClient.Domains.ListAsync());

        // DI construction with the same option value behaves identically.
        using var sp = (ServiceProvider)Build(
            o =>
            {
                o.ApiKey = "k";
                o.BaseUrl = "https://api.mailgun.test";
                o.MaxResponseContentBytes = 100;
            },
            new CannedHandler(new string('x', 2000)));
        var diClient = sp.GetRequiredService<IMailgunClient>();
        await Assert.ThrowsAsync<MailgunSerializationException>(() => diClient.Domains.ListAsync());
    }
}
