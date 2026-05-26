using System.Text;
using Mailgun.Internal;

namespace Mailgun.Tests.Internal;

public class MultipartBuilderTests
{
    [Fact]
    public async Task AddFile_Stream_respects_caller_Position_does_not_leak_pre_position_bytes()
    {
        // Regression for the silently-corrupting MemoryStream fast path: previously the SDK read
        // from offset 0 regardless of Position, so a caller that consumed (e.g.) a header line
        // before passing the stream got those header bytes uploaded as part of the file.
        var bytes = Encoding.ASCII.GetBytes("HELLO_WORLD_PAYLOAD");
        using var ms = new MemoryStream(bytes, index: 0, count: bytes.Length, writable: true, publiclyVisible: true)
        {
            Position = 6, // skip "HELLO_"
        };

        using var mp = new MultipartBuilder().AddFile("attachment", "a.txt", ms, "text/plain");
        var body = await mp.Build().ReadAsStringAsync();

        Assert.Contains("WORLD_PAYLOAD", body, StringComparison.Ordinal);
        // The pre-position substring "HELLO_" must NOT appear anywhere in the body part.
        // (We can't just Assert.DoesNotContain("HELLO_") because boundary headers might happen
        // to contain "HELLO" by accident — but the underscore-suffixed form is distinctive.)
        Assert.DoesNotContain("HELLO_WORLD", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddFile_Stream_does_not_alias_callers_backing_buffer()
    {
        // Regression for the buffer-aliasing fast path: when the caller's MemoryStream covered the
        // entire backing array, the SDK assigned `buffer = seg.Array` and handed the caller's raw
        // array straight to ByteArrayContent. Later mutations to the buffer changed the wire body.
        var buffer = Encoding.ASCII.GetBytes("ORIGINAL_DATA__");
        using var ms = new MemoryStream(buffer, index: 0, count: buffer.Length, writable: true, publiclyVisible: true);

        using var mp = new MultipartBuilder().AddFile("attachment", "a.txt", ms, "text/plain");
        // Caller mutates the underlying buffer AFTER AddFile returns — must not affect body.
        for (var i = 0; i < 8; i++) buffer[i] = (byte)'X';

        var body = await mp.Build().ReadAsStringAsync();
        Assert.Contains("ORIGINAL", body, StringComparison.Ordinal);
        Assert.DoesNotContain("XXXXXXXX", body, StringComparison.Ordinal);
    }

    [Fact]
    public void AddFile_Stream_does_not_take_ownership_of_callers_stream()
    {
        // Regression for the silent-ownership-transfer bug: the SDK already copies the bytes into a
        // private byte[], so it must NOT also dispose the caller's stream when the builder is
        // disposed. Callers commonly want to rewind/reuse/retry the stream after the SDK call.
        var ms = new MemoryStream(Encoding.ASCII.GetBytes("address\nx@example.com\n"));
        var mp = new MultipartBuilder();
        mp.AddFile("file", "list.csv", ms, "text/csv");
        mp.Dispose();

        // If Dispose stole the caller's stream, Position would throw ObjectDisposedException.
        Assert.True(ms.CanRead, "Caller's stream must remain usable after MultipartBuilder.Dispose().");
        _ = ms.Position;
        ms.Dispose();
    }
}
