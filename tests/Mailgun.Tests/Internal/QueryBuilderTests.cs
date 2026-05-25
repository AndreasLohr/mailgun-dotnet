using Mailgun.Internal;

namespace Mailgun.Tests.Internal;

/// <summary>
/// QueryBuilder is internal; reached directly via the SDK's
/// <c>InternalsVisibleTo("Mailgun.Tests")</c>. Verifies that the SDK serializes query
/// parameters with Mailgun's conventions (yes/no for bools, invariant ints, RFC-1123 dates,
/// nulls dropped).
/// </summary>
public class QueryBuilderTests
{
    [Fact]
    public void Bool_yes_no_dropping_null()
    {
        var qb = new QueryBuilder();
        qb.Add("a", (bool?)true);
        qb.Add("b", (bool?)false);
        qb.Add("c", (bool?)null);
        var pairs = qb.Build();
        Assert.Contains(new KeyValuePair<string, string?>("a", "yes"), pairs);
        Assert.Contains(new KeyValuePair<string, string?>("b", "no"), pairs);
        Assert.DoesNotContain(pairs, p => p.Key == "c");
    }

    [Fact]
    public void DateTimeOffset_serializes_as_rfc1123_utc()
    {
        var qb = new QueryBuilder();
        var dt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(5));
        qb.Add("t", (DateTimeOffset?)dt);
        var t = qb.Build().Single(p => p.Key == "t").Value!;
        Assert.EndsWith("GMT", t, StringComparison.Ordinal);
        Assert.Contains("01 Jan 2026", t, StringComparison.Ordinal);
        Assert.Contains("22:04:05", t, StringComparison.Ordinal); // converted to UTC
    }

    [Fact]
    public void IsEmpty_flips_on_first_real_value()
    {
        var qb = new QueryBuilder();
        Assert.True(qb.IsEmpty);
        qb.Add("k", (string?)null);
        Assert.True(qb.IsEmpty);     // null skipped
        qb.Add("k", "v");
        Assert.False(qb.IsEmpty);
    }

    [Fact]
    public void AddArray_appends_each_non_null_value()
    {
        var qb = new QueryBuilder();
        qb.AddArray("expand", new[] { "stats", "stats", "headers" });
        var pairs = qb.Build();
        Assert.Equal(3, pairs.Count(p => p.Key == "expand"));
    }
}
