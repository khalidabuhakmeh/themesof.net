using ThemesOfDotNet.Indexing.Ospo;
using ThemesOfDotNet.Indexing.Storage;
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Services;

public sealed class OspoCrawlerService : TimerService
{
    private readonly ILogger<OspoCrawlerService> _logger;

    private readonly WorkspaceDataCache _workspaceDataCache;
    private readonly OspoCrawler _crawler;

    public OspoCrawlerService(ILogger<OspoCrawlerService> logger,
                              IConfiguration configuration)
    {
        var token = configuration["OspoToken"];

        var connectionString = configuration["BlobConnectionString"];
        var workspaceDataStore = (KeyValueStore)new AzureBlobStorageStore(connectionString, "cache");
        _workspaceDataCache = new WorkspaceDataCache(workspaceDataStore);

        _crawler = new OspoCrawler(token, _workspaceDataCache.OspoCache);
        _logger = logger;
    }

    public IReadOnlyList<OspoLink> Links { get; private set; } = Array.Empty<OspoLink>();

    protected override TimeSpan RefreshInterval => TimeSpan.FromHours(2);

    protected override async Task InitializeAsync()
    {
        Links = await _workspaceDataCache.OspoCache.LoadAsync();
    }

    protected override async Task RefreshAsync()
    {
        _logger.LogInformation("Refreshing OSPO cache...");
        try
        {
            await _crawler.CrawlAsync();
            Links = await _workspaceDataCache.OspoCache.LoadAsync();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while refreshing data from OSPO");
        }
    }

    public event EventHandler? Changed;
}
