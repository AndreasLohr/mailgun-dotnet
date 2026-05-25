using System.Net;
using Mailgun.Models.Templates;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

public class TemplatesServiceTests
{
    [Fact]
    public async Task List_hits_v4_templates_with_limit_and_skip()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"items\":[{\"name\":\"t1\"},{\"name\":\"t2\"}],\"paging\":{},\"total_count\":2}");

        var page = await client.Templates.ListAsync(limit: 25, skip: 0);

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.EndsWith("/v4/templates", req.Uri.AbsolutePath);
        Assert.Equal("limit=25&skip=0", req.Uri.Query.TrimStart('?'));
        Assert.Equal(2, page.Items.Count);
        Assert.Equal("t1", page.Items[0].Name);
    }

    [Fact]
    public async Task Get_extracts_template_from_envelope_and_passes_active_query()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK,
            "{\"template\":{\"name\":\"welcome\",\"description\":\"hi\",\"version\":{\"tag\":\"v1\",\"template\":\"<p>hi</p>\",\"engine\":\"handlebars\"}}}");

        var t = await client.Templates.GetAsync("welcome", active: true);

        var req = Assert.Single(handler.Requests);
        Assert.EndsWith("/v4/templates/welcome", req.Uri.AbsolutePath);
        Assert.Equal("active=yes", req.Uri.Query.TrimStart('?'));
        Assert.Equal("welcome", t.Name);
        Assert.Equal("v1", t.Version!.Tag);
    }

    [Fact]
    public async Task Create_posts_form_with_name_description_and_headers_json()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"template\":{\"name\":\"t\"}}");

        await client.Templates.CreateAsync(new CreateTemplateRequest
        {
            Name = "t",
            Description = "d",
            Template = "<p>hi</p>",
            Tag = "initial",
            Engine = "handlebars",
            Comment = "c",
            Headers = new Dictionary<string, string> { ["X-A"] = "1" },
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v4/templates", req.Uri.AbsolutePath);
        Assert.Contains("name=t", req.Body!, StringComparison.Ordinal);
        Assert.Contains("description=d", req.Body!, StringComparison.Ordinal);
        Assert.Contains("template=", req.Body!, StringComparison.Ordinal);
        Assert.Contains("tag=initial", req.Body!, StringComparison.Ordinal);
        Assert.Contains("engine=handlebars", req.Body!, StringComparison.Ordinal);
        Assert.Contains("headers=", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_rejects_blank_name()
    {
        var (client, _) = TestMailgunClient.Create();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Templates.CreateAsync(new CreateTemplateRequest { Name = "" }));
    }

    [Fact]
    public async Task Update_puts_description()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"template\":{\"name\":\"t\",\"description\":\"new\"}}");

        await client.Templates.UpdateAsync("t", "new");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v4/templates/t", req.Uri.AbsolutePath);
        Assert.Equal("description=new", req.Body);
    }

    [Fact]
    public async Task Delete_calls_DELETE_on_template_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"deleted\"}");

        await client.Templates.DeleteAsync("t");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v4/templates/t", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task Copy_posts_to_copy_subpath()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"template\":{\"name\":\"clone\"}}");

        await client.Templates.CopyAsync("source", "clone");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v4/templates/source/copy/clone", req.Uri.AbsolutePath);
    }

    [Fact]
    public async Task Rename_puts_new_name_to_rename_subpath()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"template\":{\"name\":\"new\"}}");

        await client.Templates.RenameAsync("old", "new");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, req.Method);
        Assert.EndsWith("/v4/templates/old/rename", req.Uri.AbsolutePath);
        Assert.Equal("name=new", req.Body);
    }

    [Fact]
    public async Task CreateVersion_posts_form_with_required_fields()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"template\":{\"name\":\"t\"}}");

        await client.Templates.CreateVersionAsync("t", new CreateTemplateVersionRequest
        {
            Tag = "v2",
            Template = "<p>v2</p>",
            Engine = "handlebars",
            Active = true,
        });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/v4/templates/t/versions", req.Uri.AbsolutePath);
        Assert.Contains("tag=v2", req.Body!, StringComparison.Ordinal);
        Assert.Contains("active=yes", req.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteVersion_calls_DELETE_on_version_path()
    {
        var (client, handler) = TestMailgunClient.Create();
        handler.EnqueueResponse(HttpStatusCode.OK, "{\"message\":\"ok\"}");

        await client.Templates.DeleteVersionAsync("t", "v1");

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.EndsWith("/v4/templates/t/versions/v1", req.Uri.AbsolutePath);
    }
}
