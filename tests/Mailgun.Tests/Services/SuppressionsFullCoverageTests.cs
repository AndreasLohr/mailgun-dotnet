using System.IO;
using System.Net;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

/// <summary>
/// Covers Complaints/Unsubscribes/Allowlists CRUD + imports — the original SuppressionsServiceTests
/// only covers Bounces and one Unsubscribe delete-with-tag scenario.
/// </summary>
public class SuppressionsFullCoverageTests
{
    // ──────────── Bounces ────────────

    [Fact]
    public async Task Bounces_Get_Create_Delete_All_paths()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"address\":\"x@y.com\",\"code\":\"550\",\"error\":\"e\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        var b = await client.Suppressions.Bounces.GetAsync("mg.example.com", "x@y.com");
        await client.Suppressions.Bounces.CreateAsync("mg.example.com", "x@y.com", code: "550", error: "boom");
        await client.Suppressions.Bounces.DeleteAsync("mg.example.com", "x@y.com");
        await client.Suppressions.Bounces.DeleteAllAsync("mg.example.com");

        Assert.Equal("x@y.com", b.Address);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal(HttpMethod.Delete, handler.Requests[2].Method);
        Assert.Equal(HttpMethod.Delete, handler.Requests[3].Method);
        Assert.EndsWith("/v3/mg.example.com/bounces/x%40y.com", handler.Requests[0].Uri.AbsolutePath);
        Assert.EndsWith("/v3/mg.example.com/bounces", handler.Requests[3].Uri.AbsolutePath);
        Assert.Contains("address=x%40y.com", handler.Requests[1].Body!, StringComparison.Ordinal);
        Assert.Contains("code=550", handler.Requests[1].Body!, StringComparison.Ordinal);
        Assert.Contains("error=boom", handler.Requests[1].Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bounces_ImportCsv_streams_file_as_text_csv_multipart()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("address,code\nx@y.com,550\n"));

        await client.Suppressions.Bounces.ImportCsvAsync("mg.example.com", ms, fileName: "b.csv");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/mg.example.com/bounces/import", req.Uri.AbsolutePath);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
        Assert.Contains("text/csv", req.Body!, StringComparison.Ordinal);
        Assert.Contains("b.csv", req.Body!, StringComparison.Ordinal);
    }

    // ──────────── Complaints ────────────

    [Fact]
    public async Task Complaints_full_CRUD_and_import()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"address\":\"x@y\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Suppressions.Complaints.ListAsync("d", limit: 5);
        await client.Suppressions.Complaints.GetAsync("d", "x@y");
        await client.Suppressions.Complaints.CreateAsync("d", "x@y");
        await client.Suppressions.Complaints.DeleteAsync("d", "x@y");
        await client.Suppressions.Complaints.DeleteAllAsync("d");
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("address\nx@y\n"));
        await client.Suppressions.Complaints.ImportCsvAsync("d", ms);

        Assert.Equal(6, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Contains("/v3/d/complaints", r.Uri.AbsolutePath));
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
        Assert.Equal(HttpMethod.Delete, handler.Requests[3].Method);
        Assert.Equal(HttpMethod.Delete, handler.Requests[4].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[5].Method);
        Assert.Contains("address=x%40y", handler.Requests[2].Body!, StringComparison.Ordinal);
    }

    // ──────────── Unsubscribes ────────────

    [Fact]
    public async Task Unsubscribes_full_CRUD_with_tags_and_import()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"address\":\"x@y\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Suppressions.Unsubscribes.ListAsync("d");
        await client.Suppressions.Unsubscribes.GetAsync("d", "x@y");
        await client.Suppressions.Unsubscribes.CreateAsync("d", "x@y", tags: new[] { "promo", "weekly" });
        await client.Suppressions.Unsubscribes.DeleteAsync("d", "x@y"); // no tag
        await client.Suppressions.Unsubscribes.DeleteAllAsync("d");
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("address\nx@y\n"));
        await client.Suppressions.Unsubscribes.ImportCsvAsync("d", ms);

        Assert.Equal(6, handler.Requests.Count);
        Assert.Contains("tags=promo%2Cweekly", handler.Requests[2].Body!, StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Delete, handler.Requests[3].Method);
        // No tag → no query
        Assert.Equal(string.Empty, handler.Requests[3].Uri.Query);
        Assert.Equal(HttpMethod.Delete, handler.Requests[4].Method);
    }

    // ──────────── Allowlists ────────────

    [Fact]
    public async Task Allowlists_supports_both_address_and_domain_entries()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"total_count\":0}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"value\":\"x@y\",\"type\":\"address\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Suppressions.Allowlists.ListAsync("d");
        await client.Suppressions.Allowlists.GetAsync("d", "x@y");
        await client.Suppressions.Allowlists.CreateAsync("d", address: "a@b");
        await client.Suppressions.Allowlists.CreateAsync("d", domainValue: "example.com");
        await client.Suppressions.Allowlists.DeleteAsync("d", "x@y");
        await client.Suppressions.Allowlists.DeleteAllAsync("d");
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("address\nx@y\n"));
        await client.Suppressions.Allowlists.ImportCsvAsync("d", ms);

        Assert.Equal(7, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Contains("/v3/d/whitelists", r.Uri.AbsolutePath));
        Assert.Contains("address=a%40b", handler.Requests[2].Body!, StringComparison.Ordinal);
        Assert.Contains("domain=example.com", handler.Requests[3].Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Allowlists_Create_requires_either_address_or_domain()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Suppressions.Allowlists.CreateAsync("d"));
    }

    [Fact]
    public async Task Allowlists_Create_rejects_both_address_and_domain_together()
    {
        // The public interface promises "Either address or domainValue must be set (not both)."
        // Previously only the neither-supplied case was rejected; supplying both let an ambiguous
        // request through to Mailgun.
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Suppressions.Allowlists.CreateAsync("d", address: "x@y.com", domainValue: "y.com"));
    }
}
