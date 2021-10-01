using System.Text;

using ThemesOfDotNet.Indexing.Storage;

namespace ThemesOfDotNet.Indexing.Configuration;

public sealed class SubscriptionConfigurationCache
{
    private readonly KeyValueStore _store;

    public SubscriptionConfigurationCache(KeyValueStore store)
    {
        _store = store;
    }

    private static string Key => "subscriptions.json";

    public async Task<SubscriptionConfiguration> LoadAsync()
    {
        var json = await _store.LoadAsync(Key);
        using var stream = new MemoryStream();

        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
            writer.Write(json);

        stream.Position = 0;

        return await SubscriptionConfiguration.LoadAsync(stream);
    }

    public async Task StoreAsync(SubscriptionConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        using var stream = new MemoryStream();

        await configuration.SaveAsync(stream);

        stream.Position = 0;

        using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
        {
            var json = reader.ReadToEnd();
            await _store.StoreAsync(Key, json);
        }
    }
}
