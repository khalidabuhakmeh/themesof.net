using ThemesOfDotNet.Indexing.AzureDevOps;
using ThemesOfDotNet.Indexing.Configuration;
using ThemesOfDotNet.Indexing.Storage;
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Services;

public sealed class AzureDevOpsCrawlerService : TimerService
{
    private readonly ILogger<AzureDevOpsCrawlerService> _logger;
    private readonly WorkspaceDataCache _workspaceDataCache;
    private readonly AzureDevOpsQueryCrawler _crawler;

    private IReadOnlyList<AzureDevOpsQueryConfiguration>? _queries;

    public AzureDevOpsCrawlerService(ILogger<AzureDevOpsCrawlerService> logger,
                                     IConfiguration configuration)
    {
        var token = configuration["AzureDevOpsToken"];

        var connectionString = configuration["BlobConnectionString"];
        var workspaceDataStore = (KeyValueStore)new AzureBlobStorageStore(connectionString, "cache");
        _workspaceDataCache = new WorkspaceDataCache(workspaceDataStore);

        _crawler = new AzureDevOpsQueryCrawler(token, _workspaceDataCache.AzureDevOpsCache);
        _logger = logger;
    }

    protected override TimeSpan RefreshInterval => TimeSpan.FromMinutes(30);

    public IReadOnlyList<AzureDevOpsWorkItem> WorkItems { get; private set; } = Array.Empty<AzureDevOpsWorkItem>();

    protected override async Task InitializeAsync()
    {
        WorkItems = await _workspaceDataCache.AzureDevOpsCache.LoadAsync();
    }

    protected override async Task RefreshAsync()
    {
        _logger.LogInformation("Refreshing AzureDevOps cache...");
        try
        {
            if (_queries is null)
            {
                var workspaceData = await _workspaceDataCache.LoadAsync();
                _queries = workspaceData.Configuration.AzureDevOpsQueries;
            }

            await _crawler.CrawlAsync(_queries);
            WorkItems = await _workspaceDataCache.AzureDevOpsCache.LoadAsync();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while refreshing data from AzureDevOps");
        }
    }

    public event EventHandler? Changed;
}
