using System.Globalization;

namespace Mailgun.Internal;

internal sealed class QueryBuilder
{
    private readonly List<KeyValuePair<string, string?>> _pairs = new();

    public QueryBuilder Add(string name, string? value)
    {
        if (value is not null)
            _pairs.Add(new(name, value));
        return this;
    }

    public QueryBuilder Add(string name, int? value) =>
        Add(name, value?.ToString(CultureInfo.InvariantCulture));

    public QueryBuilder Add(string name, long? value) =>
        Add(name, value?.ToString(CultureInfo.InvariantCulture));

    public QueryBuilder Add(string name, bool? value) =>
        Add(name, value is null ? null : (value.Value ? "yes" : "no"));

    public QueryBuilder Add(string name, DateTimeOffset? value) =>
        Add(name, value is null ? null : MailgunDate.FormatRfc2822(value.Value));

    public QueryBuilder AddArray(string name, IEnumerable<string>? values)
    {
        if (values is null)
            return this;
        foreach (var v in values)
        {
            if (v is not null)
                _pairs.Add(new(name, v));
        }
        return this;
    }

    public IReadOnlyList<KeyValuePair<string, string?>> Build() => _pairs;

    public bool IsEmpty => _pairs.Count == 0;
}
