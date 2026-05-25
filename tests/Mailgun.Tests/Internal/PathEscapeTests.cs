using Mailgun.Internal;

namespace Mailgun.Tests.Internal;

public class PathEscapeTests
{
    [Fact]
    public void Encodes_at_sign_and_slashes()
    {
        Assert.Equal("user%40example.com", PathEscape.Segment("user@example.com"));
        Assert.Equal("a%2Fb", PathEscape.Segment("a/b"));
    }

    [Fact]
    public void Encodes_spaces_and_unicode()
    {
        Assert.Equal("hello%20world", PathEscape.Segment("hello world"));
        Assert.Equal("caf%C3%A9", PathEscape.Segment("café"));
    }

    [Fact]
    public void Throws_on_null()
    {
        Assert.Throws<ArgumentNullException>(() => PathEscape.Segment(null!));
    }
}
