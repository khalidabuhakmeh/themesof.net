using ThemesOfDotNet.Indexing.AzureDevOps;
using ThemesOfDotNet.Indexing.Configuration;
using ThemesOfDotNet.Indexing.GitHub;
using ThemesOfDotNet.Indexing.Ospo;
using ThemesOfDotNet.Indexing.Releases;
using ThemesOfDotNet.Indexing.Storage;

namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed class WorkspaceDataCache
{
    public WorkspaceDataCache(KeyValueStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        ConfigurationCache = new SubscriptionConfigurationCache(store.CreatedNested("Config"));
        ReleaseCache = new ReleaseCache(store.CreatedNested("Release"));
        GitHubCache = new GitHubCache(store.CreatedNested("GitHub"));
        AzureDevOpsCache = new AzureDevOpsCache(store.CreatedNested("AzureDevOps"));
        OspoCache = new OspoCache(store.CreatedNested("Ospo"));
    }

    public SubscriptionConfigurationCache ConfigurationCache { get; }

    public ReleaseCache ReleaseCache { get; }

    public GitHubCache GitHubCache { get; }

    public AzureDevOpsCache AzureDevOpsCache { get; }

    public OspoCache OspoCache { get; }

    public async Task<WorkspaceData> LoadAsync()
    {
        var configuration = await ConfigurationCache.LoadAsync();
        var releases = await ReleaseCache.LoadAsync();

        var gitHubRepos = await GitHubCache.LoadReposAsync();
        var gitHubIssues = gitHubRepos.SelectMany(r => r.Issues).ToArray();
        var gitHubTransferMap = await GitHubCache.LoadTransferMapAsync();
        var gitHubProjects = await GitHubCache.LoadProjectsAsync();

        var azureDevOpsWorkItems = await AzureDevOpsCache.LoadAsync();

        var ospoLinks = await OspoCache.LoadAsync();

        return new WorkspaceData(configuration,
                                 releases,
                                 gitHubIssues,
                                 gitHubTransferMap,
                                 gitHubProjects,
                                 azureDevOpsWorkItems,
                                 ospoLinks);
    }
}
