using System.Collections.ObjectModel;

namespace ThemesOfDotNet.Indexing;

public static class KeyedCollectionExtensions
{
    public static TValue? GetValueOrDefault<TKey, TValue>(this KeyedCollection<TKey, TValue> collection, TKey key)
        where TKey : notnull
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (collection.TryGetValue(key, out var value))
            return value;

        return null;
    }
}
