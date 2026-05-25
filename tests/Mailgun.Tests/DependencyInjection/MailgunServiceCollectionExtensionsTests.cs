using System.Net;
using Mailgun.Extensions.DependencyInjection;
using Mailgun.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Mailgun.Tests.DependencyInjection;

public class MailgunServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMailgun_binds_options_from_IConfigurationSection()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mailgun:ApiKey"] = "from-section",
                ["Mailgun:Region"] = "Eu",
                ["Mailgun:MaxRetries"] = "7",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddMailgun(config.GetSection("Mailgun"));
        using var sp = services.BuildServiceProvider();

        var opts = sp.GetRequiredService<IOptions<MailgunClientOptions>>().Value;
        Assert.Equal("from-section", opts.ApiKey);
        Assert.Equal(MailgunRegion.Eu, opts.Region);
        Assert.Equal(7, opts.MaxRetries);
    }

    [Fact]
    public void AddMailgun_binds_from_IConfiguration_using_default_section_name()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mailgun:ApiKey"] = "from-root",
                ["Mailgun:BaseUrl"] = "https://api.mailgun.test",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddMailgun((IConfiguration)config);
        using var sp = services.BuildServiceProvider();

        var opts = sp.GetRequiredService<IOptions<MailgunClientOptions>>().Value;
        Assert.Equal("from-root", opts.ApiKey);
        Assert.Equal("https://api.mailgun.test", opts.BaseUrl);
    }

    [Fact]
    public void AddMailgun_IConfigurationSection_throws_on_null_section()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddMailgun((IConfigurationSection)null!));
    }

    [Fact]
    public void AddMailgun_IConfiguration_throws_on_null_configuration()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddMailgun((IConfiguration)null!));
    }

    [Fact]
    public void Config_binding_composes_with_subsequent_Configure_for_code_only_fields()
    {
        // Code-only fields like OnResponse can't come from config — verify the user can patch them
        // after binding via a second Configure call.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Mailgun:ApiKey"] = "k" })
            .Build();
        var called = 0;

        var services = new ServiceCollection();
        services.AddMailgun(config.GetSection("Mailgun"));
        services.Configure<MailgunClientOptions>(o => o.OnResponse = _ => called++);
        using var sp = services.BuildServiceProvider();

        var opts = sp.GetRequiredService<IOptions<MailgunClientOptions>>().Value;
        Assert.Equal("k", opts.ApiKey);
        Assert.NotNull(opts.OnResponse);
    }

    [Fact]
    public void AddMailgun_registers_IMailgunClient_as_singleton()
    {
        var services = new ServiceCollection();
        services.AddMailgun(o => { o.ApiKey = "k"; });
        using var sp = services.BuildServiceProvider();

        var a = sp.GetRequiredService<IMailgunClient>();
        var b = sp.GetRequiredService<IMailgunClient>();
        Assert.Same(a, b);
    }

    [Fact]
    public void Options_validation_fails_when_ApiKey_is_missing()
    {
        var services = new ServiceCollection();
        services.AddMailgun(o => { o.ApiKey = ""; });
        using var sp = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => sp.GetRequiredService<IMailgunClient>());
    }

    [Fact]
    public async Task Client_authenticates_when_HttpClient_comes_from_IHttpClientFactory()
    {
        // The regression we're guarding: previously AddMailgun's named HttpClient had no auth handler,
        // so resolved clients made unauthenticated requests. Auth is now per-request, so this works.
        var services = new ServiceCollection();
        var mock = new MockHttpMessageHandler();
        mock.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        services.AddMailgun(o =>
        {
            o.ApiKey = "di-key";
            o.BaseUrl = "https://api.mailgun.test";
        });
        // Swap the named HttpClient's primary handler with our mock so we observe the request.
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(b =>
            {
                if (b.Name == MailgunServiceCollectionExtensions.HttpClientName)
                {
                    b.PrimaryHandler = mock;
                }
            });
        });

        using var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IMailgunClient>();
        _ = await client.Routes.ListAsync();

        var req = Assert.Single(mock.Requests);
        Assert.True(req.Headers.ContainsKey("Authorization"));
        var token = req.Headers["Authorization"]["Basic ".Length..];
        Assert.Equal("api:di-key", System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(token)));
    }
}
