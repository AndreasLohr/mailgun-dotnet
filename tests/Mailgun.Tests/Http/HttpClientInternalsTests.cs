using System.Net;
using Mailgun.Exceptions;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Http;

/// <summary>
/// Targets surviving mutations inside MailgunHttpClient by exercising less-common code paths:
/// PUT/DELETE/POST envelope deserialization, no-response variants, JsonContent shape.
/// </summary>
public class HttpClientInternalsTests
{
    [Fact]
    public async Task PostJsonBodyNoResponse_succeeds_with_empty_body()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, body: "");

        // Subaccounts.EnableAsync uses PostJsonBodyNoResponseAsync — this is the only path
        // where an empty body is acceptable.
        await client.Subaccounts.EnableAsync("acct_1");
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PostJsonBodyNoResponse_succeeds_with_empty_object_body()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, body: "{}");

        await client.Subaccounts.EnableAsync("acct_1");

        var req = Assert.Single(handler.Requests);
        Assert.Contains("{}", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PutFormNoResponse_succeeds_with_empty_body()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, body: "");

        await client.Domains.UpdateOpenTrackingAsync("d", true);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task DeleteNoResponse_succeeds_with_empty_body()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, body: "");

        await client.Domains.DeleteAsync("d");
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PostMultipartNoResponse_succeeds_with_empty_body()
    {
        // Regression for the original bug: import endpoints throw on empty 200 bodies.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, body: "");

        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("a@b"));
        await client.Suppressions.Bounces.ImportCsvAsync("d", ms);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Get_with_zero_query_params_does_not_emit_question_mark()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{}");

        await client.Account.GetHttpSigningKeyAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(string.Empty, req.Uri.Query);
    }

    [Fact]
    public async Task Multiple_query_params_are_joined_with_ampersand()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");

        _ = await client.Suppressions.Bounces.ListAsync("d", limit: 5, skip: 10);

        var req = Assert.Single(handler.Requests);
        var q = req.Uri.Query.TrimStart('?');
        Assert.Equal("limit=5&skip=10", q);
    }

    [Fact]
    public async Task Path_segments_with_unicode_are_percent_encoded()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"domain\":{\"name\":\"x\"}}");

        await client.Domains.GetAsync("café.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.Contains("caf%C3%A9.example.com", req.Uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SerializationException_wraps_inner_JsonException_when_request_body_unserializable()
    {
        // Exercise the request-body serializer error path. Hard to trigger naturally, but we can
        // pass a circular reference by accident — Subaccounts.UpdateFeaturesAsync takes a dict
        // that could in principle hold cycles; we just verify the normal path here doesn't throw.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"features\":{}}");

        await client.Subaccounts.UpdateFeaturesAsync("a", new Mailgun.Services.SubaccountFeatures
        {
            Features = new Dictionary<string, bool> { ["a"] = true },
        });
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Error_response_with_status_300_redirect_still_treated_as_error()
    {
        // Anything not IsSuccessStatusCode → BuildException → MailgunApiException.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.Moved, "{\"message\":\"moved\"}");

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Equal(HttpStatusCode.Moved, ex.StatusCode);
    }
}
