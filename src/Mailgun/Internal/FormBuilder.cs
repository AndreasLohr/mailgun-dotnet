using System.Globalization;

namespace Mailgun.Internal;

/// <summary>
/// Collects KVP pairs for <c>application/x-www-form-urlencoded</c> request bodies. Like
/// <see cref="QueryBuilder"/> but emits to a body rather than a query string. Mailgun uses
/// this content type for most v3/v4 form-shaped endpoints.
/// </summary>
internal sealed class FormBuilder
{
    private readonly List<KeyValuePair<string, string>> _pairs = new();

    public FormBuilder Add(string name, string? value)
    {
        if (value is not null)
            _pairs.Add(new(name, value));
        return this;
    }

    public FormBuilder Add(string name, int? value) =>
        Add(name, value?.ToString(CultureInfo.InvariantCulture));

    public FormBuilder Add(string name, long? value) =>
        Add(name, value?.ToString(CultureInfo.InvariantCulture));

    public FormBuilder Add(string name, double? value) =>
        Add(name, value?.ToString("R", CultureInfo.InvariantCulture));

    public FormBuilder Add(string name, bool? value) =>
        Add(name, value is null ? null : (value.Value ? "yes" : "no"));

    public FormBuilder Add(string name, DateTimeOffset? value) =>
        Add(name, value?.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture));

    public FormBuilder AddArray(string name, IEnumerable<string>? values)
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

    /// <summary>
    /// Mailgun's <c>o:</c>, <c>h:</c>, <c>v:</c>, <c>t:</c> message options dictionaries.
    /// Each entry becomes a separate form field.
    /// </summary>
    public FormBuilder AddPrefixed(string prefix, IReadOnlyDictionary<string, string>? values)
    {
        if (values is null)
            return this;
        foreach (var kv in values)
        {
            if (kv.Value is not null)
                _pairs.Add(new(prefix + kv.Key, kv.Value));
        }
        return this;
    }

    public IReadOnlyList<KeyValuePair<string, string>> Build() => _pairs;

    public bool IsEmpty => _pairs.Count == 0;

    public FormUrlEncodedContent ToContent() => new(_pairs);
}
