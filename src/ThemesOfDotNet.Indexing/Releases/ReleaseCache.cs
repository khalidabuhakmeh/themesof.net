using System.Text.Json;

using ThemesOfDotNet.Indexing.Storage;

namespace ThemesOfDotNet.Indexing.Releases;

public sealed class ReleaseCache
{
    private readonly KeyValueStore _store;

    public ReleaseCache(KeyValueStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
    }

    private static string Key => "releases.json";

    public async Task<IReadOnlyList<ReleaseInfo>> LoadAsync()
    {
        var json = await _store.LoadAsync(Key);
        if (json is null)
            return Array.Empty<ReleaseInfo>();

        return JsonSerializer.Deserialize<IReadOnlyList<ReleaseInfo>>(json)!;
    }

    public async Task StoreAsync(IReadOnlyList<ReleaseInfo> links)
    {
        ArgumentNullException.ThrowIfNull(links);

        var json = JsonSerializer.Serialize(links)!;
        await _store.StoreAsync(Key, json);
    }
}
