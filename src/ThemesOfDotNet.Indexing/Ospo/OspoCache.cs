using System.Text.Json;

using ThemesOfDotNet.Indexing.Storage;

namespace ThemesOfDotNet.Indexing.Ospo;

public sealed class OspoCache
{
    private readonly KeyValueStore _store;

    public OspoCache(KeyValueStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
    }

    private static string Key => "ospo.json";

    public Task ClearAsync()
    {
        return _store.ClearAsync();
    }

    public async Task<IReadOnlyList<OspoLink>> LoadAsync()
    {
        var json = await _store.LoadAsync(Key);
        if (json is null)
            return Array.Empty<OspoLink>();

        return JsonSerializer.Deserialize<IReadOnlyList<OspoLink>>(json)!;
    }

    public async Task StoreAsync(IReadOnlyList<OspoLink> links)
    {
        ArgumentNullException.ThrowIfNull(links);

        var json = JsonSerializer.Serialize(links)!;
        await _store.StoreAsync(Key, json);
    }
}
