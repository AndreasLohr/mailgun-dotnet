using Mailgun.Internal;

namespace Mailgun.Tests.Internal;

/// <summary>
/// FormBuilder is internal; reached directly via the SDK's
/// <c>InternalsVisibleTo("Mailgun.Tests")</c>. Tests verify Mailgun's documented form
/// conventions: bool serializes as <c>yes</c>/<c>no</c>, DateTimeOffset as RFC-1123 UTC, etc.
/// </summary>
public class FormBuilderTests
{
    [Fact]
    public void Bool_serializes_as_yes_or_no_per_Mailgun_convention()
    {
        var fb = new FormBuilder();
        fb.Add("a", (bool?)true);
        fb.Add("b", (bool?)false);
        fb.Add("c", (bool?)null);
        var pairs = fb.Build();
        Assert.Contains(new KeyValuePair<string, string>("a", "yes"), pairs);
        Assert.Contains(new KeyValuePair<string, string>("b", "no"), pairs);
        Assert.DoesNotContain(pairs, p => p.Key == "c");
    }

    [Fact]
    public void Int_long_double_use_invariant_culture()
    {
        var fb = new FormBuilder();
        fb.Add("i", (int?)42);
        fb.Add("l", (long?)1234567890123L);
        fb.Add("d", (double?)1.5);
        var pairs = fb.Build();
        Assert.Contains(new KeyValuePair<string, string>("i", "42"), pairs);
        Assert.Contains(new KeyValuePair<string, string>("l", "1234567890123"), pairs);
        Assert.Contains(pairs, p => p.Key == "d" && p.Value == "1.5");
    }

    [Fact]
    public void DateTimeOffset_emits_rfc2822_numeric_offset_utc()
    {
        // Mailgun's stricter endpoints reject "GMT" textual zone; the SDK formats via MailgunDate
        // (RFC-2822 with numeric -0000 offset, UTC-normalized).
        var fb = new FormBuilder();
        var dt = new DateTimeOffset(2026, 5, 16, 12, 34, 56, TimeSpan.FromHours(2));
        fb.Add("t", (DateTimeOffset?)dt);
        var pairs = fb.Build();
        Assert.Contains(pairs, p => p.Key == "t" && p.Value!.EndsWith("-0000", StringComparison.Ordinal));
        Assert.DoesNotContain(pairs, p => p.Key == "t" && p.Value!.Contains("GMT", StringComparison.Ordinal));
        Assert.Contains(pairs, p => p.Key == "t" && p.Value!.Contains("16 May 2026 10:34:56", StringComparison.Ordinal));
    }

    [Fact]
    public void Null_values_are_skipped()
    {
        var fb = new FormBuilder();
        fb.Add("a", (string?)null);
        fb.Add("b", "x");
        var pairs = fb.Build();
        Assert.DoesNotContain(pairs, p => p.Key == "a");
        Assert.Contains(new KeyValuePair<string, string>("b", "x"), pairs);
    }

    [Fact]
    public void AddArray_emits_one_pair_per_value()
    {
        var fb = new FormBuilder();
        fb.AddArray("tag", new[] { "a", "b", "c" });
        var pairs = fb.Build();
        Assert.Equal(3, pairs.Count(p => p.Key == "tag"));
        Assert.Contains(new KeyValuePair<string, string>("tag", "a"), pairs);
        Assert.Contains(new KeyValuePair<string, string>("tag", "b"), pairs);
        Assert.Contains(new KeyValuePair<string, string>("tag", "c"), pairs);
    }

    [Fact]
    public void AddPrefixed_prepends_prefix_to_each_key()
    {
        var fb = new FormBuilder();
        fb.AddPrefixed("v:", new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
        var pairs = fb.Build();
        Assert.Contains(new KeyValuePair<string, string>("v:a", "1"), pairs);
        Assert.Contains(new KeyValuePair<string, string>("v:b", "2"), pairs);
    }

    [Fact]
    public void IsEmpty_is_true_until_first_value_added()
    {
        var fb = new FormBuilder();
        Assert.True(fb.IsEmpty);
        fb.Add("k", "v");
        Assert.False(fb.IsEmpty);
    }
}
