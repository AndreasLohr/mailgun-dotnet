using System.Net;
using System.Text;
using Mailgun.Models.Messages;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class MailgunMessageBuilderTests
{
    [Fact]
    public void Build_returns_the_underlying_request_with_all_scalar_properties_set()
    {
        var (client, _) = TestMailgunClient.Create();
        var dt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var req = client.Messages.NewMessage()
            .From("sender@mg.example.com")
            .Subject("hello")
            .Text("plain body")
            .Html("<p>html body</p>")
            .Template("welcome")
            .TemplateVersion("v2")
            .TemplateText(true)
            .RecipientVariables("{\"a@b.c\":{\"name\":\"A\"}}")
            .TestMode()
            .Dkim(false)
            .DeliverAt(dt)
            .DeliveryTimeOptimizePeriod("24h")
            .TimeZoneLocalize("0900")
            .Tracking("htmlonly")
            .TrackingClicks("true")
            .TrackingOpens()
            .RequireTls()
            .SkipVerification()
            .SendingIp("203.0.113.1")
            .SendingIpPool("warm-pool")
            .TrackingPixelLocationTop()
            .Build();

        Assert.Equal("sender@mg.example.com", req.From);
        Assert.Equal("hello", req.Subject);
        Assert.Equal("plain body", req.Text);
        Assert.Equal("<p>html body</p>", req.Html);
        Assert.Equal("welcome", req.Template);
        Assert.Equal("v2", req.TemplateVersion);
        Assert.True(req.TemplateText);
        Assert.Equal("{\"a@b.c\":{\"name\":\"A\"}}", req.RecipientVariables);
        Assert.True(req.TestMode);
        Assert.False(req.Dkim);
        Assert.Equal(dt, req.DeliveryTime);
        Assert.Equal("24h", req.DeliveryTimeOptimizePeriod);
        Assert.Equal("0900", req.TimeZoneLocalize);
        Assert.Equal("htmlonly", req.Tracking);
        Assert.Equal("true", req.TrackingClicks);
        Assert.True(req.TrackingOpens);
        Assert.True(req.RequireTls);
        Assert.True(req.SkipVerification);
        Assert.Equal("203.0.113.1", req.SendingIp);
        Assert.Equal("warm-pool", req.SendingIpPool);
        Assert.True(req.TrackingPixelLocationTop);
    }

    [Fact]
    public void Collection_setters_accept_one_or_many_and_append()
    {
        var (client, _) = TestMailgunClient.Create();

        var req = client.Messages.NewMessage()
            .To("alice@example.com")
            .To("bob@example.com", "carol@example.com")
            .Cc("cc@example.com")
            .Bcc("bcc1@example.com", "bcc2@example.com")
            .Tag("welcome")
            .Tag("v2", "campaign-spring")
            .Campaign("camp-1", "camp-2")
            .Build();

        Assert.Equal(new[] { "alice@example.com", "bob@example.com", "carol@example.com" }, req.To);
        Assert.Equal(new[] { "cc@example.com" }, req.Cc);
        Assert.Equal(new[] { "bcc1@example.com", "bcc2@example.com" }, req.Bcc);
        Assert.Equal(new[] { "welcome", "v2", "campaign-spring" }, req.Tags);
        Assert.Equal(new[] { "camp-1", "camp-2" }, req.Campaigns);
    }

    [Fact]
    public void Dictionary_setters_overwrite_on_duplicate_key()
    {
        var (client, _) = TestMailgunClient.Create();

        var req = client.Messages.NewMessage()
            .TemplateVariable("name", "Alice")
            .TemplateVariable("name", "Bob")           // overwrites
            .Header("X-My-Header", "x")
            .CustomVariable("source", "signup")
            .Option("require-tls", "true")
            .Build();

        Assert.Equal("Bob", req.TemplateVariables["name"]);
        Assert.Equal("x", req.CustomHeaders["X-My-Header"]);
        Assert.Equal("signup", req.CustomVariables["source"]);
        Assert.Equal("true", req.AdditionalOptions["require-tls"]);
    }

    [Fact]
    public void Attach_and_Inline_overloads_register_attachments_and_trigger_multipart()
    {
        var (client, _) = TestMailgunClient.Create();

        var prebuilt = new MessageAttachment("doc.pdf", new byte[] { 1, 2, 3 }, "application/pdf");
        var req = client.Messages.NewMessage()
            .Attach("hello.txt", Encoding.UTF8.GetBytes("hi"), "text/plain")
            .Attach(prebuilt)
            .Inline("logo.png", new byte[] { 9 }, "image/png")
            .Build();

        Assert.Equal(2, req.Attachments.Count);
        Assert.Equal("hello.txt", req.Attachments[0].FileName);
        Assert.Same(prebuilt, req.Attachments[1]);
        Assert.Single(req.Inline);
        Assert.True(req.RequiresMultipart);
    }

    [Fact]
    public async Task SendAsync_dispatches_the_built_request_through_IMessagesService()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"<built>\",\"message\":\"Queued.\"}");

        var resp = await client.Messages.NewMessage()
            .From("sender@mg.example.com")
            .To("alice@example.com")
            .Subject("hi")
            .Text("body")
            .TestMode()
            .SendAsync("mg.example.com");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/mg.example.com/messages", req.Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("to=alice%40example.com", req.Body, StringComparison.Ordinal);
        Assert.Contains("o%3Atestmode=yes", req.Body, StringComparison.Ordinal);
        Assert.Equal("<built>", resp.Id);
    }

    [Fact]
    public void NewMessage_returns_a_fresh_builder_per_call()
    {
        var (client, _) = TestMailgunClient.Create();

        var a = client.Messages.NewMessage().From("a@example.com");
        var b = client.Messages.NewMessage().From("b@example.com");

        Assert.NotSame(a, b);
        Assert.Equal("a@example.com", a.Build().From);
        Assert.Equal("b@example.com", b.Build().From);
    }

    [Fact]
    public void Dictionary_setter_rejects_empty_key()
    {
        var (client, _) = TestMailgunClient.Create();
        var builder = client.Messages.NewMessage();

        Assert.Throws<ArgumentException>(() => builder.Header("", "v"));
        Assert.Throws<ArgumentException>(() => builder.TemplateVariable("", "v"));
        Assert.Throws<ArgumentException>(() => builder.CustomVariable("", "v"));
        Assert.Throws<ArgumentException>(() => builder.Option("", "v"));
    }

    [Fact]
    public void Attach_with_null_attachment_throws()
    {
        var (client, _) = TestMailgunClient.Create();
        var builder = client.Messages.NewMessage();
        Assert.Throws<ArgumentNullException>(() => builder.Attach((MessageAttachment)null!));
        Assert.Throws<ArgumentNullException>(() => builder.Inline((MessageAttachment)null!));
    }

    [Fact]
    public void DeliveryTime_alias_sets_the_same_property_as_DeliverAt()
    {
        var (client, _) = TestMailgunClient.Create();
        var when = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var req = client.Messages.NewMessage().DeliveryTime(when).Build();

        Assert.Equal(when, req.DeliveryTime);
    }

    [Fact]
    public void CustomHeader_alias_sets_the_same_dictionary_as_Header()
    {
        var (client, _) = TestMailgunClient.Create();

        var req = client.Messages.NewMessage()
            .CustomHeader("X-Foo", "bar")
            .Header("X-Bar", "baz")    // both aliases participate in the same dictionary
            .CustomHeader("X-Foo", "overwritten")
            .Build();

        Assert.Equal("overwritten", req.CustomHeaders["X-Foo"]);
        Assert.Equal("baz", req.CustomHeaders["X-Bar"]);
    }

    [Fact]
    public void CustomHeader_alias_rejects_empty_key()
    {
        var (client, _) = TestMailgunClient.Create();
        Assert.Throws<ArgumentException>(() => client.Messages.NewMessage().CustomHeader("", "v"));
    }

    [Fact]
    public void AmpHtml_setter_populates_request_AmpHtml()
    {
        var (client, _) = TestMailgunClient.Create();
        const string amp = "<!doctype html><html amp4email>...</html>";

        var req = client.Messages.NewMessage().AmpHtml(amp).Build();

        Assert.Equal(amp, req.AmpHtml);
    }
}
