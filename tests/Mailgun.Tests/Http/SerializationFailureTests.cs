using System.Net;
using Mailgun.Exceptions;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Http;

public class SerializationFailureTests
{
    [Fact]
    public async Task Successful_200_with_empty_body_throws_MailgunSerializationException_when_response_expected()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, body: "");

        await Assert.ThrowsAsync<MailgunSerializationException>(() => client.Domains.GetAsync("d"));
    }

    [Fact]
    public async Task Successful_200_with_malformed_json_throws_MailgunSerializationException()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, body: "{ not valid json");

        await Assert.ThrowsAsync<MailgunSerializationException>(() => client.Domains.GetAsync("d"));
    }

    [Fact]
    public async Task Successful_200_with_explicit_null_throws_MailgunSerializationException()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, body: "null");

        await Assert.ThrowsAsync<MailgunSerializationException>(() => client.Domains.GetAsync("d"));
    }

    [Fact]
    public async Task Json_content_type_set_on_request_body()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");

        _ = await client.Analytics.QueryMetricsAsync(new Mailgun.Models.Analytics.MetricsRequest
        {
            Start = "Thu, 01 Jan 2026 00:00:00 +0000",
            End = "Fri, 02 Jan 2026 00:00:00 +0000",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal("application/json", req.ContentType);
    }

    [Fact]
    public async Task Accept_application_json_header_is_sent()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");

        _ = await client.Routes.ListAsync();

        var req = Assert.Single(handler.Requests);
        Assert.True(req.Headers.ContainsKey("Accept"));
        Assert.Contains("application/json", req.Headers["Accept"], StringComparison.Ordinal);
    }
}
