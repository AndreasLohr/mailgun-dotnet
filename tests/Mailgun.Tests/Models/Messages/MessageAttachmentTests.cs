using Mailgun.Models.Messages;

namespace Mailgun.Tests.Models.Messages;

public class MessageAttachmentTests
{
    [Fact]
    public void Ctor_takes_defensive_copy_of_content_array()
    {
        // Regression: previously MessageAttachment stored the caller's byte[] by reference, so a
        // mutation between construction and the corresponding SendAsync would silently flow onto
        // the wire. The constructor now copies on intake, matching MultipartBuilder.AddFile's
        // defensive-copy contract at the next layer down.
        var buffer = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45 }; // "ABCDE"
        var attachment = new MessageAttachment("file.bin", buffer, "application/octet-stream");

        // Mutate the caller's buffer AFTER construction.
        for (var i = 0; i < buffer.Length; i++) buffer[i] = 0x58; // "XXXXX"

        // The attachment's captured content must reflect what was passed in, not the post-mutation
        // state.
        Assert.Equal(new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45 }, attachment.Content);
    }

    [Fact]
    public void Ctor_returned_Content_is_not_the_same_array_instance()
    {
        // The strongest statement of the defensive-copy contract: the returned array is a fresh
        // allocation, not an alias. Mutating attachment.Content directly also no longer affects
        // the caller's original buffer (and vice-versa).
        var buffer = new byte[] { 1, 2, 3 };
        var attachment = new MessageAttachment("f", buffer);

        Assert.NotSame(buffer, attachment.Content);
        Assert.Equal(buffer, attachment.Content);
    }

    [Fact]
    public void Ctor_rejects_null_content()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MessageAttachment("file.bin", null!, "application/octet-stream"));
    }

    [Fact]
    public void Ctor_rejects_blank_file_name()
    {
        var buffer = new byte[] { 1, 2, 3 };
        Assert.Throws<ArgumentException>(() => new MessageAttachment("", buffer));
        Assert.Throws<ArgumentException>(() => new MessageAttachment("   ", buffer));
    }

    [Fact]
    public void Ctor_accepts_empty_content_array()
    {
        // Zero-byte attachments are wire-legal; the copy step must not regress that. (Buffer.BlockCopy
        // with count=0 is a no-op, but we still want the assertion in case the implementation ever
        // adds a "non-empty" guard.)
        var attachment = new MessageAttachment("empty.bin", Array.Empty<byte>(), "application/octet-stream");
        Assert.Empty(attachment.Content);
    }
}
