using System.Net;
using Mailgun.Models.Routes;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class RouteBuilderTests
{
    // ---------- RouteExpression rendering ----------

    [Fact]
    public void MatchRecipient_renders_with_quoted_pattern() =>
        Assert.Equal(
            "match_recipient(\"support@.*\")",
            RouteExpression.MatchRecipient("support@.*").Render());

    [Fact]
    public void MatchHeader_renders_with_two_quoted_args() =>
        Assert.Equal(
            "match_header(\"X-Campaign\", \"^spring-.*$\")",
            RouteExpression.MatchHeader("X-Campaign", "^spring-.*$").Render());

    [Fact]
    public void CatchAll_renders_paren_pair() =>
        Assert.Equal("catch_all()", RouteExpression.CatchAll().Render());

    [Fact]
    public void EscapeQuoted_escapes_embedded_quote_and_backslash()
    {
        // pattern contains both " and \ — both must be backslash-escaped inside the Mailgun DSL string literal.
        var rendered = RouteExpression.MatchRecipient("a\"b\\c").Render();
        Assert.Equal("match_recipient(\"a\\\"b\\\\c\")", rendered);
    }

    [Fact]
    public void Or_renders_n_ary_with_comma_separation()
    {
        var expr = RouteExpression.Or(
            RouteExpression.MatchRecipient("support@.*"),
            RouteExpression.MatchRecipient("help@.*"));
        Assert.Equal("or(match_recipient(\"support@.*\"), match_recipient(\"help@.*\"))", expr.Render());
    }

    [Fact]
    public void And_renders_n_ary_with_comma_separation()
    {
        var expr = RouteExpression.And(
            RouteExpression.MatchRecipient("support@.*"),
            RouteExpression.MatchHeader("X-Priority", "high"));
        Assert.Equal("and(match_recipient(\"support@.*\"), match_header(\"X-Priority\", \"high\"))", expr.Render());
    }

    [Fact]
    public void Not_renders_unary()
    {
        var expr = RouteExpression.Not(RouteExpression.MatchRecipient("noreply@.*"));
        Assert.Equal("not(match_recipient(\"noreply@.*\"))", expr.Render());
    }

    [Fact]
    public void Nested_combinators_render_correctly()
    {
        // not(or(catch_all(), match_recipient("a")))
        var expr = RouteExpression.Not(
            RouteExpression.Or(
                RouteExpression.CatchAll(),
                RouteExpression.MatchRecipient("a")));
        Assert.Equal("not(or(catch_all(), match_recipient(\"a\")))", expr.Render());
    }

    [Fact]
    public void And_with_fewer_than_two_children_throws() =>
        Assert.Throws<ArgumentException>(() => RouteExpression.And(RouteExpression.CatchAll()));

    [Fact]
    public void Or_with_fewer_than_two_children_throws() =>
        Assert.Throws<ArgumentException>(() => RouteExpression.Or(RouteExpression.CatchAll()));

    [Fact]
    public void Raw_passes_through_unchanged() =>
        Assert.Equal("custom(\"thing\")", RouteExpression.Raw("custom(\"thing\")").Render());

    // ---------- RouteBuilder.Build() shape ----------

    [Fact]
    public void Build_materializes_scalars_expression_and_actions()
    {
        var (client, _) = TestMailgunClient.Create();

        var req = client.Routes.NewRoute()
            .Priority(10)
            .Description("Forward support")
            .MatchRecipient("support@mg.example.com")
            .Forward("https://hooks.example.com/mailgun")
            .Store("https://hooks.example.com/notify")
            .Stop()
            .Build();

        Assert.Equal(10, req.Priority);
        Assert.Equal("Forward support", req.Description);
        Assert.Equal("match_recipient(\"support@mg.example.com\")", req.Expression);
        Assert.Equal(
            new[]
            {
                "forward(\"https://hooks.example.com/mailgun\")",
                "store(notify=\"https://hooks.example.com/notify\")",
                "stop()",
            },
            req.Actions);
    }

    [Fact]
    public void Store_with_no_notify_renders_paren_pair()
    {
        var (client, _) = TestMailgunClient.Create();
        var req = client.Routes.NewRoute().CatchAll().Store().Build();
        Assert.Equal(new[] { "store()" }, req.Actions);
    }

    [Fact]
    public void Match_overwrites_previously_set_expression()
    {
        var (client, _) = TestMailgunClient.Create();
        var req = client.Routes.NewRoute()
            .MatchRecipient("first@.*")
            .MatchRecipient("second@.*")
            .Build();
        Assert.Equal("match_recipient(\"second@.*\")", req.Expression);
    }

    [Fact]
    public void Match_accepts_complex_expression_tree()
    {
        var (client, _) = TestMailgunClient.Create();
        var req = client.Routes.NewRoute()
            .Match(RouteExpression.Or(
                RouteExpression.MatchRecipient("support@.*"),
                RouteExpression.MatchRecipient("help@.*")))
            .Forward("https://example.com")
            .Build();
        Assert.Equal("or(match_recipient(\"support@.*\"), match_recipient(\"help@.*\"))", req.Expression);
    }

    [Fact]
    public void Forward_escapes_url_with_embedded_quotes()
    {
        var (client, _) = TestMailgunClient.Create();
        var req = client.Routes.NewRoute().CatchAll().Forward("https://x.com/?q=\"hi\"").Build();
        Assert.Equal(new[] { "forward(\"https://x.com/?q=\\\"hi\\\"\")" }, req.Actions);
    }

    [Fact]
    public void Action_raw_escape_hatch_passes_through_unchanged()
    {
        var (client, _) = TestMailgunClient.Create();
        var req = client.Routes.NewRoute()
            .CatchAll()
            .Action("anything_mailgun_supports(\"x\", \"y\")")
            .Build();
        Assert.Equal(new[] { "anything_mailgun_supports(\"x\", \"y\")" }, req.Actions);
    }

    [Fact]
    public void Forward_rejects_empty_url()
    {
        var (client, _) = TestMailgunClient.Create();
        Assert.Throws<ArgumentException>(() => client.Routes.NewRoute().Forward(""));
    }

    // ---------- End-to-end through IRoutesService ----------

    [Fact]
    public async Task CreateAsync_posts_to_v3_routes_with_form_body()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"message\":\"ok\",\"route\":{\"id\":\"r-built\",\"priority\":0,\"expression\":\"match_recipient(\\\"support@.*\\\")\",\"actions\":[\"forward(\\\"https://x\\\")\",\"stop()\"]}}");

        var route = await client.Routes.NewRoute()
            .Priority(0)
            .Description("Forward support")
            .MatchRecipient("support@.*")
            .Forward("https://x")
            .Stop()
            .CreateAsync();

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/routes", req.Uri.AbsolutePath);
        Assert.Equal("application/x-www-form-urlencoded", req.ContentType);
        Assert.Contains("expression=match_recipient", req.Body, StringComparison.Ordinal);
        Assert.Contains("action=forward", req.Body, StringComparison.Ordinal);
        Assert.Contains("action=stop", req.Body, StringComparison.Ordinal);
        Assert.Equal("r-built", route.Id);
    }

    [Fact]
    public async Task UpdateAsync_puts_to_v3_routes_id_with_form_body()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"message\":\"ok\",\"route\":{\"id\":\"r-1\",\"priority\":5}}");

        await client.Routes.NewRoute()
            .Priority(5)
            .CatchAll()
            .Store()
            .UpdateAsync("r-1");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v3/routes/r-1", req.Uri.AbsolutePath);
        Assert.Contains("priority=5", req.Body, StringComparison.Ordinal);
        Assert.Contains("expression=catch_all", req.Body, StringComparison.Ordinal);
        Assert.Contains("action=store", req.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void NewRoute_returns_a_fresh_builder_per_call()
    {
        var (client, _) = TestMailgunClient.Create();
        var a = client.Routes.NewRoute().Priority(1);
        var b = client.Routes.NewRoute().Priority(2);
        Assert.NotSame(a, b);
        Assert.Equal(1, a.Build().Priority);
        Assert.Equal(2, b.Build().Priority);
    }
}
