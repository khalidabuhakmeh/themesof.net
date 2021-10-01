namespace ThemesOfDotNet.Indexing.Storage;

public abstract class KeyValueStore
{
    public abstract KeyValueStore CreatedNested(string name);

    public abstract IAsyncEnumerable<string> GetKeysAsync();

    public abstract Task ClearAsync();

    public abstract Task<string?> LoadAsync(string key);

    public abstract Task StoreAsync(string key, string value);

    public abstract Task RemoveAsync(string key);
}
