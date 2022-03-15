using Terrajobst.GitHubEvents;

using ThemesOfDotNet.Indexing.AzureDevOps;
using ThemesOfDotNet.Indexing.GitHub;
using ThemesOfDotNet.Indexing.Ospo;
using ThemesOfDotNet.Indexing.Releases;
using ThemesOfDotNet.Indexing.Storage;
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Services;

public sealed class WorkspaceService
{
    private readonly WorkspaceCrawler _workspaceCrawler;
    
    public WorkspaceService(IConfiguration configuration,
                            IWebHostEnvironment environment)
    {
        var gitHubAppId = configuration["GitHubAppId"];
        var gitHubAppPrivateKey = configuration["GitHubAppPrivateKey"];
        var azureDevOpsToken = configuration["AzureDevOpsToken"];
        var ospoToken = configuration["OspoToken"];

        var connectionString = configuration["BlobConnectionString"];
        var workspaceDataStore = (KeyValueStore) new AzureBlobStorageStore(connectionString, "cache");

        if (environment.IsDevelopment())
        {
            var directoryPath = Path.Join(Path.GetDirectoryName(Environment.ProcessPath), "cache");
            workspaceDataStore = new FallbackStore(new FileSystemStore(directoryPath), workspaceDataStore);
        }

        var workspaceDataCache = new WorkspaceDataCache(workspaceDataStore);

        var gitHubCrawler = new GitHubCrawler(gitHubAppId, gitHubAppPrivateKey, workspaceDataCache.GitHubCache);
        var azureDevOpsCrawler = new AzureDevOpsCrawler(azureDevOpsToken, workspaceDataCache.AzureDevOpsCache);
        var ospoCrawler = new OspoCrawler(ospoToken, workspaceDataCache.OspoCache);
        var releaseCrawler = new ReleaseCrawler(workspaceDataCache.ReleaseCache);

        _workspaceCrawler = new WorkspaceCrawler(workspaceDataCache, releaseCrawler, gitHubCrawler, azureDevOpsCrawler, ospoCrawler);
    }

    public Workspace Workspace { get; private set; } = Workspace.Empty;

    public event EventHandler? WorkspaceChanged;

    public async Task InitializeAsync()
    {
        await _workspaceCrawler.LoadFromCacheAsync();

        UpdateWorkspace();
    }

    public async Task UpdateGitHubAsync(GitHubEventMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _workspaceCrawler.UpdateGitHubAsync(message);

        UpdateWorkspace();
    }

    public async Task UpdateAzureDevOpsAsync()
    {
        await _workspaceCrawler.UpdateAzureDevOpsAsync();

        UpdateWorkspace();
    }

    public async Task UpdateOspoAsync()
    {
        await _workspaceCrawler.UpdateOspoAsync();

        UpdateWorkspace();
    }

    private void UpdateWorkspace()
    {
        var snapshot = _workspaceCrawler.GetSnapshot();
        Workspace = Workspace.Create(snapshot);
        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }
}
