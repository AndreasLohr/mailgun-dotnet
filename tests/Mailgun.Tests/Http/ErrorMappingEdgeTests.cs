using System.Net;
using Mailgun.Exceptions;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Http;

/// <summary>
/// Mailgun's error envelopes have several shapes — { "message": "..." }, { "Message": "..." },
/// { "error": "..." }, plus a "details" or "errors" field that can be a string, a string array,
/// or an object. These tests pin each shape down so the FlattenJson / BuildException paths can't
/// drift.
/// </summary>
public class ErrorMappingEdgeTests
{
    [Fact]
    public async Task Recognizes_lowercase_message_field()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, "{\"message\":\"bad request\"}");

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Equal("bad request", ex.ErrorMessage);
    }

    [Fact]
    public async Task Recognizes_capital_Message_field_fallback()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, "{\"Message\":\"capital\"}");

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Equal("capital", ex.ErrorMessage);
    }

    [Fact]
    public async Task Recognizes_error_field_when_no_message()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, "{\"error\":\"oops\"}");

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Equal("oops", ex.ErrorMessage);
    }

    [Fact]
    public async Task Flattens_details_string()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, "{\"message\":\"x\",\"details\":\"extra info\"}");

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Contains("extra info", ex.Details);
    }

    [Fact]
    public async Task Flattens_details_array_of_strings()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, "{\"message\":\"x\",\"details\":[\"a\",\"b\"]}");

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Contains("a", ex.Details);
        Assert.Contains("b", ex.Details);
    }

    [Fact]
    public async Task Flattens_details_object_into_key_value_strings()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, "{\"errors\":{\"name\":\"is required\",\"email\":\"invalid\"}}");

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Contains(ex.Details, d => d.Contains("name", StringComparison.Ordinal) && d.Contains("required", StringComparison.Ordinal));
        Assert.Contains(ex.Details, d => d.Contains("email", StringComparison.Ordinal) && d.Contains("invalid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unparseable_body_yields_exception_with_raw_body_preserved()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.BadRequest, "<html>nope</html>", contentType: "text/html");

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Null(ex.ErrorMessage);   // body wasn't JSON
        Assert.Empty(ex.Details);
        Assert.Equal("<html>nope</html>", ex.RawResponseBody);
    }

    [Fact]
    public async Task Empty_error_body_still_throws_with_no_message_or_details()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.InternalServerError, body: "");

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Null(ex.ErrorMessage);
        Assert.Empty(ex.Details);
    }

    [Fact]
    public async Task Builds_human_readable_Message_from_status_code_alone()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.Forbidden, body: "");

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Contains("403", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Forbidden", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Builds_message_with_error_text_when_present()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.UnprocessableEntity, "{\"message\":\"validation failed\"}");

        var ex = await Assert.ThrowsAsync<MailgunApiException>(() => client.Domains.GetAsync("d"));
        Assert.Contains("validation failed", ex.Message, StringComparison.Ordinal);
        Assert.Contains("422", ex.Message, StringComparison.Ordinal);
    }
}
