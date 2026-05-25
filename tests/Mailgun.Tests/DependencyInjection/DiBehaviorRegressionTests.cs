using System.Net;
using Mailgun.Extensions.DependencyInjection;
using Mailgun.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Mailgun.Tests.DependencyInjection;

/// <summary>
/// Regression tests for two previously-shipping DI bugs the field-level review caught.
/// Both fail deterministically against the un-fixed extension on both target frameworks.
///
/// <para>Bug 1: <see cref="MailgunServiceCollectionExtensions.AddMailgun"/> rebuilt the
/// <see cref="MailgunClientOptions"/> field-by-field but omitted <c>OnResponse</c>, so any
/// callback set via <c>AddMailgun(o =&gt; o.OnResponse = ...)</c> was silently dropped.</para>
///
/// <para>Bug 2: the named <see cref="HttpClient"/> the extension creates had no
/// <c>RateLimitHandler</c> in its pipeline. <c>MailgunHttpClient.ctor</c> only attaches the
/// SDK's retry handler on the owned-HttpClient path (when no HttpClient is supplied), so DI
/// callers got zero retries regardless of <c>MaxRetries</c>.</para>
/// </summary>
public class DiBehaviorRegressionTests
{
    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses;
        public int CallCount { get; private set; }

        public CountingHandler(params HttpStatusCode[] statuses) =>
            _statuses = new Queue<HttpStatusCode>(statuses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var status = _statuses.Count > 0 ? _statuses.Dequeue() : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("{\"items\":[],\"total_count\":0}", System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private static IServiceProvider Build(
        Action<MailgunClientOptions> configure,
        HttpMessageHandler primaryHandler)
    {
        var services = new ServiceCollection();
        services.AddMailgun(configure);
        services.ConfigureAll<HttpClientFactoryOptions>(o =>
        {
            o.HttpMessageHandlerBuilderActions.Add(b =>
            {
                if (b.Name == MailgunServiceCollectionExtensions.HttpClientName)
                {
                    b.PrimaryHandler = primaryHandler;
                }
            });
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AddMailgun_propagates_OnResponse_callback_to_the_resolved_client()
    {
        var captured = new List<MailgunResponseMetadata>();
        var primary = new CountingHandler(HttpStatusCode.OK);

        using var sp = (ServiceProvider)Build(
            o =>
            {
                o.ApiKey = "k";
                o.BaseUrl = "https://api.mailgun.test";
                o.OnResponse = md => captured.Add(md);
            },
            primary);

        var client = sp.GetRequiredService<IMailgunClient>();
        _ = await client.Routes.ListAsync();

        var only = Assert.Single(captured);
        Assert.Equal(HttpStatusCode.OK, only.StatusCode);
    }

    [Fact]
    public async Task AddMailgun_retries_429_via_the_named_HttpClient_pipeline()
    {
        // Without the fix, the named HttpClient has no RateLimitHandler in its pipeline and
        // MailgunHttpClient.ctor's owned-HttpClient retry path is skipped because DI supplies
        // the HttpClient. Result: zero retries, even with MaxRetries=5. With the fix, the
        // RateLimitHandler is attached and three consecutive 429s + one 200 produce four calls.
        var primary = new CountingHandler(
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.OK);

        using var sp = (ServiceProvider)Build(
            o =>
            {
                o.ApiKey = "k";
                o.BaseUrl = "https://api.mailgun.test";
                o.MaxRetries = 3;
            },
            primary);

        var client = sp.GetRequiredService<IMailgunClient>();
        _ = await client.Routes.ListAsync();

        Assert.Equal(4, primary.CallCount);
    }

    [Fact]
    public async Task AddMailgun_retries_5xx_on_idempotent_methods_only()
    {
        // Companion to the 429 test: GET 500 + 200 → 2 calls (one retry). Kills any future
        // regression that drops 5xx retry but keeps 429 retry.
        var primary = new CountingHandler(HttpStatusCode.InternalServerError, HttpStatusCode.OK);

        using var sp = (ServiceProvider)Build(
            o =>
            {
                o.ApiKey = "k";
                o.BaseUrl = "https://api.mailgun.test";
                o.MaxRetries = 2;
            },
            primary);

        var client = sp.GetRequiredService<IMailgunClient>();
        _ = await client.Routes.ListAsync();

        Assert.Equal(2, primary.CallCount);
    }
}
