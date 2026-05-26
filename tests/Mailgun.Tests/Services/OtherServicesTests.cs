using System.IO;
using System.Net;
using Mailgun.Models.Analytics;
using Mailgun.Services;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class OtherServicesTests
{
    // ──────────── Keys ────────────

    [Fact]
    public async Task Keys_List_Get_Create_Delete_cover_v1_keys()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[{\"id\":\"k1\"}],\"paging\":{},\"total_count\":1}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"key\":\"secret\",\"id\":\"k2\",\"role\":\"sending\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        var page = await client.Keys.ListAsync(limit: 25, kind: "sending");
        var created = await client.Keys.CreateAsync(new Mailgun.Models.Keys.CreateApiKeyRequest
        {
            Description = "deploy",
            Role = "sending",
            Domain = "mg.example.com",
        });
        await client.Keys.DeleteAsync("k2");

        Assert.Single(page.Items);
        Assert.Equal("secret", created.Key);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("kind=sending", handler.Requests[0].Uri.Query, StringComparison.Ordinal);
        Assert.Contains("description=deploy", handler.Requests[1].Body!, StringComparison.Ordinal);
        Assert.Contains("role=sending", handler.Requests[1].Body!, StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Delete, handler.Requests[2].Method);
        Assert.EndsWith("/v1/keys/k2", handler.Requests[2].Uri.AbsolutePath);
    }

    // ──────────── BounceClassification ────────────

    [Fact]
    public async Task BounceClassification_List_and_Get_use_v1_paths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[{\"code\":\"550\"}]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"code\":\"550\",\"category\":\"hard\"}");

        await client.BounceClassification.ListAsync();
        var c = await client.BounceClassification.GetAsync("550");

        Assert.EndsWith("/v1/bounce-classification", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v1/bounce-classification/550", handler.Requests[1].Uri.AbsolutePath);
        Assert.Equal("hard", c.Category);
    }

    [Fact]
    public async Task BounceClassification_QueryMetrics_posts_json_to_v2_metrics()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");

        await client.BounceClassification.QueryMetricsAsync(new BounceClassificationMetricsRequest
        {
            Start = "Wed, 01 May 2026 00:00:00 +0000",
            End = "Fri, 31 May 2026 23:59:59 +0000",
            Resolution = "1d",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("application/json", req.ContentType);
        Assert.EndsWith("/v2/bounce-classification/metrics", req.Uri.AbsolutePath);
        Assert.Contains("\"resolution\":\"1d\"", req.Body!, StringComparison.Ordinal);
    }

    // ──────────── AnalyticsTags ────────────

    [Fact]
    public async Task AnalyticsTags_List_Delete_Limits()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[{\"tag\":\"welcome\"}],\"total_count\":1}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"Tag deleted\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"limit\":4096,\"count\":12}");

        await client.AnalyticsTags.ListAsync(new AnalyticsTagsFilter
        {
            Pagination = new AnalyticsTagsPagination { Limit = 10, Skip = 0 },
        });
        await client.AnalyticsTags.DeleteAsync("welcome");
        var lim = await client.AnalyticsTags.GetLimitsAsync();

        Assert.Equal(4096, lim.Limit);
        // List → POST /v1/analytics/tags with JSON body.
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.EndsWith("/v1/analytics/tags", handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal("application/json", handler.Requests[0].ContentType);
        // Delete → DELETE /v1/analytics/tags with {"tag":"welcome"} in the body (NOT in the path).
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.EndsWith("/v1/analytics/tags", handler.Requests[1].Uri.AbsolutePath);
        Assert.Equal("application/json", handler.Requests[1].ContentType);
        Assert.Contains("\"tag\":\"welcome\"", handler.Requests[1].Body!, StringComparison.Ordinal);
        // Limits → GET /v1/analytics/tags/limits.
        Assert.Equal(HttpMethod.Get, handler.Requests[2].Method);
        Assert.EndsWith("/v1/analytics/tags/limits", handler.Requests[2].Uri.AbsolutePath);
    }

    [Fact]
    public async Task AnalyticsTags_Update_puts_json_body_with_tag_and_description()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"Tag updated\"}");

        await client.AnalyticsTags.UpdateAsync("welcome", "Sent on user signup");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        // Tag is in the body, NOT the URL — the endpoint is /v1/analytics/tags (no tag segment).
        Assert.EndsWith("/v1/analytics/tags", req.Uri.AbsolutePath);
        Assert.Equal("application/json", req.ContentType);
        Assert.Contains("\"tag\":\"welcome\"", req.Body!, StringComparison.Ordinal);
        Assert.Contains("\"description\":\"Sent on user signup\"", req.Body!, StringComparison.Ordinal);
    }

    // ──────────── Validate ────────────

    [Fact]
    public async Task Validate_single_uses_GET_with_address_query()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"address\":\"alice@example.com\",\"is_valid\":true,\"result\":\"deliverable\"}");

        var r = await client.Validate.ValidateAsync("alice@example.com");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v4/address/validate", req.Uri.AbsolutePath);
        Assert.Contains("address=alice%40example.com", req.Uri.Query, StringComparison.Ordinal);
        Assert.True(r.IsValid);
    }

    [Fact]
    public async Task Validate_CreateBulk_uploads_csv_as_multipart_to_listId_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"my-list\",\"status\":\"uploaded\"}");

        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("address\na@b.com\n"));
        await client.Validate.CreateBulkAsync("my-list", ms);

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v4/address/validate/bulk/my-list", req.Uri.AbsolutePath);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
        Assert.Contains("text/csv", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validate_ListBulk_DeleteBulk_use_correct_methods()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"jobs\":[]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"my-list\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Validate.ListBulkAsync();
        await client.Validate.GetBulkAsync("my-list");
        await client.Validate.DeleteBulkAsync("my-list");

        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Equal(HttpMethod.Delete, handler.Requests[2].Method);
    }

    // ──────────── InboxPlacement ────────────

    [Fact]
    public async Task InboxPlacement_seedlists_CRUD()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[{\"name\":\"main\"}]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"name\":\"main\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"name\":\"main\",\"description\":\"x\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"name\":\"main\",\"description\":\"y\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.InboxPlacement.ListSeedlistsAsync();
        await client.InboxPlacement.GetSeedlistAsync("main");
        await client.InboxPlacement.CreateSeedlistAsync(new CreateSeedlistRequest { Name = "main", Description = "x" });
        await client.InboxPlacement.UpdateSeedlistAsync("main", new UpdateSeedlistRequest { Description = "y" });
        await client.InboxPlacement.DeleteSeedlistAsync("main");

        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
        Assert.Equal(HttpMethod.Put, handler.Requests[3].Method);
        Assert.Equal(HttpMethod.Delete, handler.Requests[4].Method);
        Assert.All(handler.Requests, r => Assert.Contains("/v4/inbox/seedlists", r.Uri.AbsolutePath));
    }

    [Fact]
    public async Task InboxPlacement_CreateTest_posts_json_with_seedlist()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"r_1\",\"subject\":\"s\"}");

        await client.InboxPlacement.CreateTestAsync(new CreateInboxPlacementTestRequest
        {
            Seedlist = "main",
            Subject = "test",
            FromAddress = "x@y.com",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v4/inbox/tests", req.Uri.AbsolutePath);
        Assert.Contains("\"seedlist\":\"main\"", req.Body!, StringComparison.Ordinal);
    }

    // ──────────── Alerts / SendAlerts / Limits ────────────

    [Fact]
    public async Task Alerts_ListEvents_SubscribeEvent_use_v1_alerts_paths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[]}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Alerts.ListEventsAsync();
        await client.Alerts.SubscribeEventAsync("bounce", "email");

        Assert.EndsWith("/v1/alerts/events", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v1/alerts/events/subscribe", handler.Requests[1].Uri.AbsolutePath);
        Assert.Contains("event=bounce", handler.Requests[1].Body!, StringComparison.Ordinal);
        Assert.Contains("channel=email", handler.Requests[1].Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Alerts_RemoveEmail_uses_DELETE_with_email_segment()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Alerts.RemoveEmailAsync("a@b.com");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v1/alerts/email/recipients/a%40b.com", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task SendAlerts_CRUD_uses_documented_v1_thresholds_alerts_send_resource()
    {
        // The old /config and /queues/* endpoints don't exist in Mailgun's current API. The real
        // shape is a CRUD-over-named-rules resource: list/get/create/update/delete by name.
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"name\":\"bounce-spike\",\"metric\":\"failed_count\",\"comparator\":\"gt\",\"limit\":\"100\",\"dimension\":\"domain\"}],\"total\":1}");
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"name\":\"new-alert\",\"id\":\"alrt-1\"}");

        var list = await client.SendAlerts.ListAsync();
        await client.SendAlerts.CreateAsync(new SendAlertRule
        {
            Name = "new-alert", Metric = "failed_count", Comparator = "gt",
            Limit = "50", Dimension = "domain",
        });

        Assert.Single(list.Items!);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.EndsWith("/v1/thresholds/alerts/send", handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal("application/json", handler.Requests[1].ContentType);
        Assert.EndsWith("/v1/thresholds/alerts/send", handler.Requests[1].Uri.AbsolutePath);
        Assert.Contains("\"name\":\"new-alert\"", handler.Requests[1].Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAlerts_Create_rejects_missing_required_fields()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.SendAlerts.CreateAsync(new SendAlertRule { Name = "x" /* missing rest */ }));
    }
}
