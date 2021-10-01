namespace ThemesOfDotNet.Indexing.Querying.Binding;

public sealed class BoundKevValueQuery : BoundQuery
{
    internal BoundKevValueQuery(bool isNegated, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        IsNegated = isNegated;
        Key = key;
        Value = value;
    }

    public bool IsNegated { get; }

    public string Key { get; }

    public string Value { get; }
}
