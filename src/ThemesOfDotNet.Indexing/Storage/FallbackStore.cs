namespace ThemesOfDotNet.Indexing.Storage;

public sealed class FallbackStore : KeyValueStore
{
    private readonly KeyValueStore _primaryStore;
    private readonly KeyValueStore _fallbackStore;

    public FallbackStore(KeyValueStore primaryStore, KeyValueStore fallbackStore)
    {
        ArgumentNullException.ThrowIfNull(primaryStore);
        ArgumentNullException.ThrowIfNull(fallbackStore);

        _primaryStore = primaryStore;
        _fallbackStore = fallbackStore;
    }

    public override KeyValueStore CreatedNested(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var nestedPrimary = _primaryStore.CreatedNested(name);
        var nestedFallback = _fallbackStore.CreatedNested(name);
        return new FallbackStore(nestedPrimary, nestedFallback);
    }

    public override Task ClearAsync()
    {
        throw new NotSupportedException();
    }

    public override async IAsyncEnumerable<string> GetKeysAsync()
    {
        var primaryHasAnyKeys = false;

        await foreach (var key in _primaryStore.GetKeysAsync())
        {
            primaryHasAnyKeys = true;
            yield return key;
        }

        if (!primaryHasAnyKeys)
        {
            await foreach (var key in _fallbackStore.GetKeysAsync())
            {
                yield return key;
            }
        }
    }

    public override async Task<string?> LoadAsync(string key)
    {
        var result = await _primaryStore.LoadAsync(key);
        if (result is null)
        {
            result = await _fallbackStore.LoadAsync(key);
            if (result is not null)
                await _primaryStore.StoreAsync(key, result);
        }

        return result;
    }

    public override Task StoreAsync(string key, string value)
    {
        throw new NotSupportedException();
    }

    public override Task RemoveAsync(string key)
    {
        throw new NotSupportedException();
    }
}
