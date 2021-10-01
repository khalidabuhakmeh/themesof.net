using Azure.Storage.Blobs;

namespace ThemesOfDotNet.Indexing.Storage;

public sealed class AzureBlobStorageStore : KeyValueStore
{
    private readonly string _connectionString;
    private readonly string _containerName;
    private readonly string _prefix;

    public AzureBlobStorageStore(string connectionString, string containerName, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        ArgumentNullException.ThrowIfNull(containerName);
        ArgumentNullException.ThrowIfNull(prefix);

        _connectionString = connectionString;
        _containerName = containerName;
        _prefix = prefix;
    }

    public override KeyValueStore CreatedNested(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var nestedPrefix = _prefix.Length == 0
                                ? name
                                : _prefix + "/" + name;

        return new AzureBlobStorageStore(_connectionString, _containerName, nestedPrefix);
    }

    public override async Task ClearAsync()
    {
        var client = new BlobContainerClient(_connectionString, _containerName);
        if (!await client.ExistsAsync())
            return;

        await foreach (var blob in client.GetBlobsAsync(prefix: _prefix))
        {
            var blobClient = client.GetBlobClient(blob.Name);
            await blobClient.DeleteIfExistsAsync();
        }
    }

    public override async IAsyncEnumerable<string> GetKeysAsync()
    {
        var client = new BlobContainerClient(_connectionString, _containerName);
        if (await client.ExistsAsync())
        {
            await foreach (var blob in client.GetBlobsAsync(prefix: _prefix))
            {
                var relativeName = blob.Name.Substring(_prefix.Length + 1);
                yield return relativeName;
            }
        }
    }

    public override async Task<string?> LoadAsync(string key)
    {
        var client = new BlobClient(_connectionString, _containerName, $"{_prefix}/{key}");
        if (!await client.ExistsAsync())
            return null;

        using (var memoryStream = new MemoryStream())
        {
            await client.DownloadToAsync(memoryStream);

            memoryStream.Position = 0;

            using (var streamReader = new StreamReader(memoryStream))
                return await streamReader.ReadToEndAsync();
        }
    }

    public override async Task StoreAsync(string key, string value)
    {
        var containerClient = new BlobContainerClient(_connectionString, _containerName);
        await containerClient.CreateIfNotExistsAsync();

        var client = new BlobClient(_connectionString, _containerName, $"{_prefix}/{key}");
        await client.UploadAsync(new BinaryData(value), overwrite: true);
    }

    public override Task RemoveAsync(string key)
    {
        var client = new BlobClient(_connectionString, _containerName, $"{_prefix}/{key}");
        return client.DeleteIfExistsAsync();
    }
}
