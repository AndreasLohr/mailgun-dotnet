namespace Mailgun.Tests.Http;

public class RegionTests
{
    [Fact]
    public void Us_region_resolves_to_us_base_url()
    {
        var opts = new MailgunClientOptions { ApiKey = "k", Region = MailgunRegion.Us };
        Assert.Equal("https://api.mailgun.net", opts.ResolveBaseUrl());
    }

    [Fact]
    public void Eu_region_resolves_to_eu_base_url()
    {
        var opts = new MailgunClientOptions { ApiKey = "k", Region = MailgunRegion.Eu };
        Assert.Equal("https://api.eu.mailgun.net", opts.ResolveBaseUrl());
    }

    [Fact]
    public void Explicit_base_url_overrides_region()
    {
        var opts = new MailgunClientOptions
        {
            ApiKey = "k",
            Region = MailgunRegion.Eu,
            BaseUrl = "https://api.mailgun.test",
        };
        Assert.Equal("https://api.mailgun.test", opts.ResolveBaseUrl());
    }
}
