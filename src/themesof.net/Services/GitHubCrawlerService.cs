using ThemesOfDotNet.Indexing.GitHub;
using ThemesOfDotNet.Indexing.Storage;
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Services;

public sealed class GitHubCrawlerService
{
    private readonly string _gitHubAppId;
    private readonly string _gitHubAppPrivateKey;
    private readonly GitHubCache _cache;
    private GitHubIncrementalCrawler? _crawler;

    public GitHubCrawlerService(IConfiguration configuration)
    {
        _gitHubAppId = configuration["GitHubAppId"];
        _gitHubAppPrivateKey = configuration["GitHubAppPrivateKey"];

        var connectionString = configuration["BlobConnectionString"];
        var workspaceDataStore = (KeyValueStore)new AzureBlobStorageStore(connectionString, "cache");
        var workspaceDataCache = new WorkspaceDataCache(workspaceDataStore);

        _cache = workspaceDataCache.GitHubCache;
    }

    public async Task InitializeAsync()
    {
        _crawler = await GitHubIncrementalCrawler.CreateAsync(_gitHubAppId, _gitHubAppPrivateKey, _cache);
    }

    public GitHubIncrementalCrawler Crawler
    {
        get
        {
            if (_crawler is null)
                throw new InvalidOperationException($"Must call {nameof(InitializeAsync)}() before");

            return _crawler;
        }
    }
}
