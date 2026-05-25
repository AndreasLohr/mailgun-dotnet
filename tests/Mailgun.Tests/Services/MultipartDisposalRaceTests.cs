using System.Net;
using Mailgun.Models.Messages;
using Mailgun.Models.MailingLists;
using Mailgun.Tests.TestHelpers;

namespace Mailgun.Tests.Services;

/// <summary>
/// Regression for a critical use-after-dispose bug where multipart-bodied requests had
/// <c>using var mp = new MultipartBuilder()...</c> inside a non-async method returning
/// <see cref="Task"/>. <c>using</c> in a non-async method disposes immediately after the
/// inner method returns its Task — i.e. <em>before</em> the underlying HTTP handler reads
/// the body. <see cref="System.Net.Http.MultipartFormDataContent.Dispose"/> clears the
/// nested parts list, so any later body read sees an empty multipart.
/// <para>
/// The default <see cref="MockHttpMessageHandler"/> reads the body synchronously at the top
/// of <c>SendAsync</c>, so it captures the body before disposal can run — masking the bug.
/// <see cref="YieldingMultipartHandler"/> here intentionally yields before reading, which is
/// what a real <c>HttpClientHandler</c> does once a Task is returned to the caller.
/// </para>
/// </summary>
public class MultipartDisposalRaceTests
{
    /// <summary>
    /// HTTP message handler that deterministically reproduces the use-after-dispose race against
    /// a buggy <c>using var mp = ...; return _http.PostMultipartAsync(...)</c> call site, even
    /// under a busy xUnit parallel runner. Signals when it has captured the request and is about
    /// to read the body, then waits on a caller-supplied release gate before reading. The test
    /// can use the signal to confirm the SDK's outer async frame has returned its Task (and, in
    /// the buggy variant, disposed the multipart) before releasing the read.
    /// </summary>
    private sealed class GatedMultipartHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _reachedReadPoint = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Awaited by the test to detect that the handler is suspended just before the body read.</summary>
        public Task ReachedReadPoint => _reachedReadPoint.Task;

        /// <summary>The body bytes captured AFTER the test releases the gate. Empty / throws on a buggy SDK.</summary>
        public string? CapturedBody { get; private set; }
        public string? CapturedContentType { get; private set; }
        public Exception? ReadException { get; private set; }

        public void Release() => _release.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedContentType = request.Content?.Headers.ContentType?.MediaType;

            // Tell the test we're suspended at the read point. From this moment until Release()
            // fires, the test owns the question: "has the SDK's `using` scope exited yet?"
            _reachedReadPoint.SetResult();
            await _release.Task.ConfigureAwait(false);

            try
            {
                CapturedBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Capture exceptions instead of throwing so the test asserts deterministically;
                // on a buggy SDK this would be ObjectDisposedException (net8.0) or
                // InvalidOperationException("Collection was modified") (net10.0).
                ReadException = ex;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"<x>\",\"message\":\"ok\"}", System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    private static (MailgunClient client, GatedMultipartHandler handler) Build()
    {
        var handler = new GatedMultipartHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mailgun.test/") };
        var client = new MailgunClient(new MailgunClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://api.mailgun.test",
            HttpClient = http,
        });
        return (client, handler);
    }

    /// <summary>
    /// Drives a multipart-bodied SDK call against the gated handler so the race is forced
    /// deterministically: the SDK's outer Task is observed BEFORE the handler reads the body,
    /// so a buggy `using var mp = ...; return _http.PostMultipartAsync(...)` call site will
    /// have disposed the multipart in the window between the two.
    /// </summary>
    private static async Task RunGatedAsync(Func<Task> sdkCall, GatedMultipartHandler handler)
    {
        var sdkTask = sdkCall();
        // Wait until the handler is suspended at the read point. By this moment, the SDK's
        // outer async frame has yielded its Task back to us — which on a buggy call site
        // means the `using` scope has already exited and the multipart is disposed.
        await handler.ReachedReadPoint.WaitAsync(TimeSpan.FromSeconds(5));
        // Release the gate: read the body now.
        handler.Release();
        await sdkTask;
    }

    [Fact]
    public async Task Messages_SendAsync_with_attachment_survives_a_gated_handler()
    {
        var (client, handler) = Build();

        await RunGatedAsync(
            () => client.Messages.SendAsync("mg.example.com", new SendMessageRequest
            {
                From = "x@mg.example.com",
                To = { "alice@example.com" },
                Subject = "s",
                Text = "t",
                Attachments = { new MessageAttachment("hello.txt", System.Text.Encoding.UTF8.GetBytes("hi"), "text/plain") },
            }),
            handler);

        Assert.Null(handler.ReadException);
        Assert.StartsWith("multipart/form-data", handler.CapturedContentType, StringComparison.Ordinal);
        Assert.Contains("hello.txt", handler.CapturedBody!, StringComparison.Ordinal);
        Assert.Contains("hi", handler.CapturedBody!, StringComparison.Ordinal);
        Assert.Contains("from", handler.CapturedBody!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Bounces")]
    [InlineData("Complaints")]
    [InlineData("Unsubscribes")]
    [InlineData("Allowlists")]
    public async Task Suppressions_ImportCsv_survives_a_gated_handler(string serviceName)
    {
        var (client, handler) = Build();
        const string csv = "address,code\nx@example.com,550\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        await RunGatedAsync(
            () => serviceName switch
            {
                "Bounces" => client.Suppressions.Bounces.ImportCsvAsync("mg.example.com", stream),
                "Complaints" => client.Suppressions.Complaints.ImportCsvAsync("mg.example.com", stream),
                "Unsubscribes" => client.Suppressions.Unsubscribes.ImportCsvAsync("mg.example.com", stream),
                "Allowlists" => client.Suppressions.Allowlists.ImportCsvAsync("mg.example.com", stream),
                _ => throw new ArgumentOutOfRangeException(nameof(serviceName)),
            },
            handler);

        Assert.Null(handler.ReadException);
        Assert.StartsWith("multipart/form-data", handler.CapturedContentType, StringComparison.Ordinal);
        Assert.Contains(csv, handler.CapturedBody!, StringComparison.Ordinal);
        Assert.Contains("text/csv", handler.CapturedBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailingLists_BulkAddMembersCsv_survives_a_gated_handler()
    {
        var (client, handler) = Build();
        const string csv = "address,name\nalice@example.com,Alice\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        await RunGatedAsync(
            () => client.MailingLists.BulkAddMembersCsvAsync("list@y", stream, upsert: true),
            handler);

        Assert.Null(handler.ReadException);
        Assert.StartsWith("multipart/form-data", handler.CapturedContentType, StringComparison.Ordinal);
        Assert.Contains(csv, handler.CapturedBody!, StringComparison.Ordinal);
        Assert.Contains("upsert", handler.CapturedBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validate_CreateBulk_survives_a_gated_handler()
    {
        var (client, handler) = Build();
        const string csv = "address\nalice@example.com\nbob@example.com\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        // The mock response payload doesn't perfectly match BulkValidationJob, so swallow the
        // post-read deserialization error and assert on the body-capture path only.
        try
        {
            await RunGatedAsync(
                () => (Task)client.Validate.CreateBulkAsync("my-list", stream),
                handler);
        }
        catch (Mailgun.Exceptions.MailgunSerializationException) { /* response-shape mismatch is fine */ }

        Assert.Null(handler.ReadException);
        Assert.StartsWith("multipart/form-data", handler.CapturedContentType, StringComparison.Ordinal);
        Assert.Contains(csv, handler.CapturedBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validate_CreateBulkPreview_survives_a_gated_handler()
    {
        var (client, handler) = Build();
        const string csv = "address\nalice@example.com\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        try
        {
            await RunGatedAsync(
                () => (Task)client.Validate.CreateBulkPreviewAsync("my-list", stream),
                handler);
        }
        catch (Mailgun.Exceptions.MailgunSerializationException) { /* response-shape mismatch is fine */ }

        Assert.Null(handler.ReadException);
        Assert.StartsWith("multipart/form-data", handler.CapturedContentType, StringComparison.Ordinal);
        Assert.Contains(csv, handler.CapturedBody!, StringComparison.Ordinal);
    }
}
