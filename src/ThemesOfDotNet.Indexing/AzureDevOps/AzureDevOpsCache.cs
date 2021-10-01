using System.Text.Json;

using ThemesOfDotNet.Indexing.Storage;

namespace ThemesOfDotNet.Indexing.AzureDevOps;

public sealed class AzureDevOpsCache
{
    private readonly KeyValueStore _store;

    public AzureDevOpsCache(KeyValueStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
    }

    private static string Key => "workItems.json";

    public Task ClearAsync()
    {
        return _store.ClearAsync();
    }

    public async Task<IReadOnlyList<AzureDevOpsWorkItem>> LoadAsync()
    {
        var json = await _store.LoadAsync(Key);
        if (json is null)
            return Array.Empty<AzureDevOpsWorkItem>();

        return JsonSerializer.Deserialize<IReadOnlyList<AzureDevOpsWorkItem>>(json)!;
    }

    public async Task StoreAsync(IReadOnlyList<AzureDevOpsWorkItem> workItems)
    {
        ArgumentNullException.ThrowIfNull(workItems);

        var json = JsonSerializer.Serialize(workItems)!;
        await _store.StoreAsync(Key, json);
    }
}
