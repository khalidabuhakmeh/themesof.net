namespace ThemesOfDotNet.Indexing.Storage;

public sealed class FileSystemStore : KeyValueStore
{
    private readonly string _cacheDirectory;

    public FileSystemStore(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
    }

    public override KeyValueStore CreatedNested(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return new FileSystemStore(Path.Join(_cacheDirectory, name));
    }

    public override async IAsyncEnumerable<string> GetKeysAsync()
    {
        if (!Directory.Exists(_cacheDirectory))
            yield break;

        var fileNames = Directory.GetFiles(_cacheDirectory, "*", SearchOption.AllDirectories);
        foreach (var fileName in fileNames)
        {
            var relativePath = Path.GetRelativePath(_cacheDirectory, fileName);
            yield return relativePath;
        }

        await Task.CompletedTask;
    }

    public override Task ClearAsync()
    {
        if (Directory.Exists(_cacheDirectory))
            Directory.Delete(_cacheDirectory, recursive: true);

        return Task.CompletedTask;
    }

    public override async Task<string?> LoadAsync(string key)
    {
        var fileName = Path.Combine(_cacheDirectory, key);
        if (File.Exists(fileName))
            return await File.ReadAllTextAsync(fileName);

        return null;
    }

    public override async Task StoreAsync(string key, string value)
    {
        var fileName = Path.Combine(_cacheDirectory, key);
        var directoryName = Path.GetDirectoryName(fileName)!;
        Directory.CreateDirectory(directoryName);
        await File.WriteAllTextAsync(fileName, value);
    }

    public override Task RemoveAsync(string key)
    {
        var fileName = Path.Combine(_cacheDirectory, key);
        File.Delete(fileName);
        return Task.CompletedTask;
    }
}
