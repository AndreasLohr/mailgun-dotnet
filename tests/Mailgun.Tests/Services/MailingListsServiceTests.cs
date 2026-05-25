using System.IO;
using System.Net;
using Mailgun.Models.MailingLists;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class MailingListsServiceTests
{
    [Fact]
    public async Task List_paginates_via_v3_lists_pages()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"address\":\"a@x\"}],\"paging\":{\"next\":\"https://api.mailgun.test/x?skip=1\"},\"total_count\":1}");

        var page = await client.MailingLists.ListAsync(limit: 10, skip: 0);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/lists/pages", req.Uri.AbsolutePath);
        Assert.Single(page.Items);
        Assert.Equal("a@x", page.Items[0].Address);
    }

    [Fact]
    public async Task Get_extracts_list_from_envelope()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"list\":{\"address\":\"weekly@x\",\"name\":\"Weekly\"}}");

        var l = await client.MailingLists.GetAsync("weekly@x");

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/lists/weekly%40x", req.Uri.AbsolutePath);
        Assert.Equal("Weekly", l.Name);
    }

    [Fact]
    public async Task Create_posts_required_address_and_optional_fields()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"list\":{\"address\":\"x@y\"}}");

        await client.MailingLists.CreateAsync(new CreateMailingListRequest
        {
            Address = "x@y",
            Name = "n",
            Description = "d",
            AccessLevel = "members",
            ReplyPreference = "list",
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v3/lists", req.Uri.AbsolutePath);
        Assert.Contains("address=x%40y", req.Body!, StringComparison.Ordinal);
        Assert.Contains("name=n", req.Body!, StringComparison.Ordinal);
        Assert.Contains("description=d", req.Body!, StringComparison.Ordinal);
        Assert.Contains("access_level=members", req.Body!, StringComparison.Ordinal);
        Assert.Contains("reply_preference=list", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_rejects_blank_address()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.MailingLists.CreateAsync(new CreateMailingListRequest { Address = "" }));
    }

    [Fact]
    public async Task Update_uses_PUT_and_supports_address_change()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"list\":{\"address\":\"new@y\"}}");

        await client.MailingLists.UpdateAsync("old@y", new UpdateMailingListRequest { Address = "new@y", Name = "N" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v3/lists/old%40y", req.Uri.AbsolutePath);
        Assert.Contains("address=new%40y", req.Body!, StringComparison.Ordinal);
        Assert.Contains("name=N", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_calls_DELETE_on_list_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.MailingLists.DeleteAsync("x@y");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v3/lists/x%40y", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task ListMembers_paginates_and_filters_by_subscribed()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"items\":[],\"paging\":{},\"total_count\":0}");

        await client.MailingLists.ListMembersAsync("list@y", limit: 5, skip: 1, subscribed: true);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/lists/list%40y/members/pages", req.Uri.AbsolutePath);
        Assert.Contains("subscribed=yes", req.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddMember_posts_member_form_with_upsert_flag()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"member\":{\"address\":\"a@b\"}}");

        await client.MailingLists.AddMemberAsync("l@y", new AddMemberRequest
        {
            Address = "a@b",
            Name = "Alice",
            Subscribed = true,
            Upsert = true,
            Vars = "{\"plan\":\"pro\"}",
        });

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/lists/l%40y/members", req.Uri.AbsolutePath);
        Assert.Contains("address=a%40b", req.Body!, StringComparison.Ordinal);
        Assert.Contains("name=Alice", req.Body!, StringComparison.Ordinal);
        Assert.Contains("subscribed=yes", req.Body!, StringComparison.Ordinal);
        Assert.Contains("upsert=yes", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteMember_calls_DELETE_on_member_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.MailingLists.DeleteMemberAsync("l@y", "a@b");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v3/lists/l%40y/members/a%40b", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task BulkAddMembers_posts_json_array_form_field()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.MailingLists.BulkAddMembersAsync("l@y", new[]
        {
            new AddMemberRequest { Address = "a@b", Name = "A" },
            new AddMemberRequest { Address = "c@d", Name = "C" },
        }, upsert: true);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/lists/l%40y/members.json", req.Uri.AbsolutePath);
        Assert.Contains("members=", req.Body!, StringComparison.Ordinal);
        Assert.Contains("upsert=yes", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BulkAddMembers_rejects_empty_and_over_1000()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.MailingLists.BulkAddMembersAsync("l@y", Array.Empty<AddMemberRequest>()));
        var big = Enumerable.Range(0, 1001).Select(i => new AddMemberRequest { Address = $"a{i}@b" }).ToArray();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.MailingLists.BulkAddMembersAsync("l@y", big));
    }

    [Fact]
    public async Task BulkAddMembersCsv_streams_file_as_multipart()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("address,name\na@b,A\n"));
        await client.MailingLists.BulkAddMembersCsvAsync("l@y", ms, upsert: true);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v3/lists/l%40y/members.csv", req.Uri.AbsolutePath);
        Assert.StartsWith("multipart/form-data", req.ContentType, StringComparison.Ordinal);
        Assert.Contains("upsert", req.Body!, StringComparison.Ordinal);
        Assert.Contains("text/csv", req.Body!, StringComparison.Ordinal);
    }
}
