using ThemesOfDotNet.Indexing.Storage;
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Services;

public sealed class WorkspaceService
{
    private readonly ILogger<WorkspaceService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly GitHubCrawlerService _gitHubCrawlerService;
    private readonly AzureDevOpsCrawlerService _azureDevOpsCrawlerService;
    private readonly OspoCrawlerService _ospoCrawlerService;

    private WorkspaceData? _workspaceData;

    public WorkspaceService(ILogger<WorkspaceService> logger,
                            IConfiguration configuration,
                            IWebHostEnvironment environment,
                            GitHubCrawlerService gitHubCrawlerService,
                            AzureDevOpsCrawlerService azureDevOpsCrawlerService,
                            OspoCrawlerService ospoCrawlerService)
    {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
        _gitHubCrawlerService = gitHubCrawlerService;
        _azureDevOpsCrawlerService = azureDevOpsCrawlerService;
        _ospoCrawlerService = ospoCrawlerService;
    }

    public Workspace Workspace { get; private set; } = Workspace.Empty;

    public event EventHandler? WorkspaceChanged;

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing workspace");

        var connectionString = _configuration["BlobConnectionString"];
        if (connectionString is null)
            return;

        var workspaceDataStore = (KeyValueStore)new AzureBlobStorageStore(connectionString, "cache");

        if (_environment.IsDevelopment())
        {
            var directoryPath = Path.Join(Path.GetDirectoryName(Environment.ProcessPath), "cache");
            workspaceDataStore = new FallbackStore(new FileSystemStore(directoryPath), workspaceDataStore);
        }

        var workspaceDataCache = new WorkspaceDataCache(workspaceDataStore);
        var workspaceData = await workspaceDataCache.LoadAsync();

        UpdateWorkspace(workspaceData);

        _gitHubCrawlerService.Crawler.Changed += CrawlerChanged;
        _azureDevOpsCrawlerService.Changed += CrawlerChanged;
        _ospoCrawlerService.Changed += CrawlerChanged;
    }

    private void CrawlerChanged(object? sender, EventArgs e)
    {
        if (_workspaceData is null)
            return;

        _gitHubCrawlerService.Crawler.GetSnapshot(out var issues,
                                                  out var transferMap,
                                                  out var projects);

        var data = new WorkspaceData(
            _workspaceData.Configuration,
            _workspaceData.Releases,
            issues,
            transferMap,
            projects,
            _azureDevOpsCrawlerService.WorkItems,
            _ospoCrawlerService.Links
        );

        UpdateWorkspace(data);
    }

    private void UpdateWorkspace(WorkspaceData workspaceData)
    {
        _logger.LogInformation("Updating workspace");
        _workspaceData = workspaceData;
        Workspace = Workspace.Create(workspaceData);
        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }
}
