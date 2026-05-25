using System.Net;
using System.Text;

namespace Mailgun.Tests.TestHelpers;

/// <summary>
/// Records incoming HTTP requests and returns canned responses. Use to test the SDK against
/// a fake HTTP transport without making real network calls.
/// </summary>
/// <remarks>
/// By default this handler reads the request body synchronously at the top of SendAsync, which
/// captures the bytes before any disposal in the caller's frame can fire. That matches what a
/// "fast loopback" looks like to the SDK and keeps most tests deterministic. Set
/// <see cref="YieldBeforeReadingBody"/> = true to make the handler <c>await Task.Yield()</c>
/// before the read — that exposes use-after-dispose / disposal-race bugs in the caller, mirroring
/// the timing of a real <c>HttpClientHandler</c> reading the body over the wire.
/// </remarks>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Response> _responses = new();

    /// <summary>All requests received, in order.</summary>
    public List<RecordedRequest> Requests { get; } = new();

    /// <summary>
    /// When true, <c>await Task.Yield()</c> before reading the request body. Off by default to
    /// keep the bulk of the suite fast and deterministic. Enable it for tests that need to
    /// surface body-read timing bugs (the canonical example is a non-async caller that wraps
    /// the request content in a <c>using</c> — the <c>using</c> disposes the moment control
    /// returns from <c>HttpClient.SendAsync</c>, but the real wire reads the body after that
    /// point. Yielding here puts the test on the same side of the race as a real network.
    /// </summary>
    public bool YieldBeforeReadingBody { get; set; }

    /// <summary>Enqueue a response for the next request. Multiple enqueues are FIFO.</summary>
    public MockHttpMessageHandler EnqueueResponse(
        HttpStatusCode status,
        string? body = null,
        string contentType = "application/json",
        IDictionary<string, string>? headers = null)
    {
        _responses.Enqueue(new Response(status, body, contentType, headers));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (YieldBeforeReadingBody)
        {
            await Task.Yield();
        }

        var capturedBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var capturedContentType = request.Content?.Headers.ContentType?.MediaType;

        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
            capturedBody,
            capturedContentType));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"No queued mock response for {request.Method} {request.RequestUri}.");
        }

        var canned = _responses.Dequeue();
        var response = new HttpResponseMessage(canned.Status);
        if (canned.Body is not null)
        {
            response.Content = new StringContent(canned.Body, Encoding.UTF8, canned.ContentType);
        }
        if (canned.Headers is not null)
        {
            foreach (var kv in canned.Headers)
            {
                response.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }
        return response;
    }

    private sealed record Response(
        HttpStatusCode Status,
        string? Body,
        string ContentType,
        IDictionary<string, string>? Headers);

    /// <summary>A recorded request observed by this handler.</summary>
    public sealed record RecordedRequest(
        HttpMethod Method,
        Uri Uri,
        IReadOnlyDictionary<string, string> Headers,
        string? Body,
        string? ContentType);
}
