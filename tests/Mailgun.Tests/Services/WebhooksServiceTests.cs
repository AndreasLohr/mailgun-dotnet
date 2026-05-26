using System.Net;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class WebhooksServiceTests
{
    [Fact]
    public async Task CreateDomain_posts_form_with_id_and_urls()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"webhook\":{\"urls\":[\"https://a\",\"https://b\"]}}");

        await client.Webhooks.CreateDomainAsync("mg.example.com", "delivered",
            new[] { "https://a", "https://b" });

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/domains/mg.example.com/webhooks", req.Uri.AbsolutePath);
        Assert.Contains("id=delivered", req.Body!, StringComparison.Ordinal);
        Assert.Contains("url=https%3A%2F%2Fa", req.Body!, StringComparison.Ordinal);
        Assert.Contains("url=https%3A%2F%2Fb", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateDomain_rejects_zero_urls()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Webhooks.CreateDomainAsync("d", "delivered", Array.Empty<string>()));
    }

    [Fact]
    public async Task CreateDomain_rejects_more_than_three_urls()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Webhooks.CreateDomainAsync("d", "delivered",
                new[] { "https://a", "https://b", "https://c", "https://d" }));
    }

    [Fact]
    public async Task UpdateDomain_uses_PUT()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"webhook\":{\"urls\":[\"https://a\"]}}");

        await client.Webhooks.UpdateDomainAsync("d", "opened", new[] { "https://a" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v3/domains/d/webhooks/opened", req.Uri.AbsolutePath);
    }

    // ──────────── Modern ID-based account webhooks ────────────

    [Fact]
    public async Task ListAccountWebhooks_no_filter_hits_v1_webhooks_no_query()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"webhooks\":[{\"id\":\"wh1\",\"description\":\"d\",\"event_types\":[\"delivered\"],\"url\":\"https://x\"}]}");

        var resp = await client.Webhooks.ListAccountWebhooksAsync();

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/webhooks", req.Uri.AbsolutePath);
        Assert.Equal(string.Empty, req.Uri.Query);
        Assert.Single(resp.Webhooks);
        Assert.Equal("wh1", resp.Webhooks[0].Id);
        Assert.Equal(new[] { "delivered" }, resp.Webhooks[0].EventTypes);
    }

    [Fact]
    public async Task ListAccountWebhooks_with_id_filter_passes_single_comma_separated_query_param()
    {
        // Mailgun documents `webhook_ids` as a single comma-separated query param,
        // NOT repeated `webhook_ids=a&webhook_ids=b`.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"webhooks\":[]}");

        await client.Webhooks.ListAccountWebhooksAsync(new[] { "wh1", "wh2" });

        var req = Assert.Single(handler.Requests);
        var q = req.Uri.Query.TrimStart('?');
        Assert.Contains("webhook_ids=wh1%2Cwh2", q, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAccountWebhook_uses_id_in_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"id\":\"wh42\",\"description\":\"prod\",\"event_types\":[\"opened\"],\"url\":\"https://x\"}");

        var wh = await client.Webhooks.GetAccountWebhookAsync("wh42");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v1/webhooks/wh42", req.Uri.AbsolutePath);
        Assert.Equal("wh42", wh.Id);
        Assert.Equal("prod", wh.Description);
    }

    [Fact]
    public async Task CreateAccountWebhook_posts_multipart_with_url_description_and_event_types()
    {
        // Mailgun's POST /v1/webhooks is documented as multipart/form-data only.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"id\":\"new-id\",\"description\":\"prod hook\",\"event_types\":[\"delivered\",\"opened\"],\"url\":\"https://x\"}");

        var wh = await client.Webhooks.CreateAccountWebhookAsync(
            url: "https://x",
            eventTypes: new[] { "delivered", "opened" },
            description: "prod hook");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v1/webhooks", req.Uri.AbsolutePath);
        Assert.Equal("multipart/form-data", req.ContentType);
        // Multipart parts list each field name + value; the body has both verbatim.
        Assert.Contains("url", req.Body!, StringComparison.Ordinal);
        Assert.Contains("https://x", req.Body!, StringComparison.Ordinal);
        Assert.Contains("description", req.Body!, StringComparison.Ordinal);
        Assert.Contains("prod hook", req.Body!, StringComparison.Ordinal);
        Assert.Contains("event_types", req.Body!, StringComparison.Ordinal);
        Assert.Contains("delivered", req.Body!, StringComparison.Ordinal);
        Assert.Contains("opened", req.Body!, StringComparison.Ordinal);
        Assert.Equal("new-id", wh.Id);
    }

    [Fact]
    public async Task CreateAccountWebhook_rejects_empty_event_types()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Webhooks.CreateAccountWebhookAsync("https://x", Array.Empty<string>()));
    }

    [Fact]
    public async Task UpdateAccountWebhook_puts_multipart_with_required_url_and_event_types()
    {
        // Mailgun's PUT /v1/webhooks/{id} is full-replace semantics — url + event_types are required.
        // Mailgun responds 204 No Content; the SDK method returns Task (no body).
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.NoContent, string.Empty);

        await client.Webhooks.UpdateAccountWebhookAsync(
            id: "wh-1",
            url: "https://y",
            eventTypes: new[] { "clicked" },
            description: "updated");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v1/webhooks/wh-1", req.Uri.AbsolutePath);
        Assert.Equal("multipart/form-data", req.ContentType);
        Assert.Contains("url", req.Body!, StringComparison.Ordinal);
        Assert.Contains("https://y", req.Body!, StringComparison.Ordinal);
        Assert.Contains("description", req.Body!, StringComparison.Ordinal);
        Assert.Contains("updated", req.Body!, StringComparison.Ordinal);
        Assert.Contains("event_types", req.Body!, StringComparison.Ordinal);
        Assert.Contains("clicked", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAccountWebhook_uses_id_in_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.NoContent, string.Empty);

        await client.Webhooks.DeleteAccountWebhookAsync("wh-x");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v1/webhooks/wh-x", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task DeleteAccountWebhooks_with_ids_passes_query_params()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.NoContent, string.Empty);

        await client.Webhooks.DeleteAccountWebhooksAsync(new[] { "a", "b" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v1/webhooks", req.Uri.AbsolutePath);
        var q = req.Uri.Query.TrimStart('?');
        // Mailgun documents this as a single comma-separated `webhook_ids` query param.
        Assert.Contains("webhook_ids=a%2Cb", q, StringComparison.Ordinal);
        Assert.DoesNotContain("all=", q, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAccountWebhooks_with_all_passes_all_true()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.NoContent, string.Empty);

        await client.Webhooks.DeleteAccountWebhooksAsync(all: true);

        var req = Assert.Single(handler.Requests);
        var q = req.Uri.Query.TrimStart('?');
        Assert.Contains("all=true", q, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAccountWebhooks_rejects_neither_ids_nor_all()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Webhooks.DeleteAccountWebhooksAsync());
    }
}
