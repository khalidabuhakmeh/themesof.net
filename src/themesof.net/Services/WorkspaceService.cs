using ThemesOfDotNet.Indexing.Storage;
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Services;

public sealed class WorkspaceService
{
    private readonly ILogger<WorkspaceService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly GitHubCrawlerService _crawlerService;

    private WorkspaceData? _workspaceData;

    public WorkspaceService(ILogger<WorkspaceService> logger,
                            IConfiguration configuration,
                            IWebHostEnvironment environment,
                            GitHubCrawlerService crawlerService)
    {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
        _crawlerService = crawlerService;
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
    }

    public void Invalidate()
    {
        if (_workspaceData is null)
            return;

        _crawlerService.Crawler.GetSnapshot(out var issues,
                                            out var transferMap,
                                            out var projects);

        // TODO: Update OSPO Links

        var data = new WorkspaceData(
            _workspaceData.Configuration,
            _workspaceData.Releases,
            issues,
            transferMap,
            projects,
            _workspaceData.AzureDevOpsWorkItems,
            _workspaceData.OspoLinks
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
