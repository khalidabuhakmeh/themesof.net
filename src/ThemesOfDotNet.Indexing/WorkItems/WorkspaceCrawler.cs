using Terrajobst.GitHubEvents;

using ThemesOfDotNet.Indexing.AzureDevOps;
using ThemesOfDotNet.Indexing.Configuration;
using ThemesOfDotNet.Indexing.GitHub;
using ThemesOfDotNet.Indexing.Ospo;
using ThemesOfDotNet.Indexing.Releases;

namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed class WorkspaceCrawler : IWorkspaceCrawlerQueue
{
    private readonly WorkspaceDataCache _workspaceDataCache;
    private readonly ReleaseCrawler _releaseCrawler;
    private readonly GitHubCrawler _gitHubCrawler;
    private readonly AzureDevOpsCrawler _azureDevOpsCrawler;
    private readonly OspoCrawler _ospoCrawler;

    private SubscriptionConfiguration _configuration = SubscriptionConfiguration.Empty;

    public WorkspaceCrawler(WorkspaceDataCache workspaceDataCache,
                            ReleaseCrawler releaseCrawler,
                            GitHubCrawler gitHubCrawler,
                            AzureDevOpsCrawler azureDevOpsCrawler,
                            OspoCrawler ospoCrawler)
    {
        ArgumentNullException.ThrowIfNull(gitHubCrawler);
        ArgumentNullException.ThrowIfNull(azureDevOpsCrawler);
        
        _workspaceDataCache = workspaceDataCache;
        _releaseCrawler = releaseCrawler;
        _gitHubCrawler = gitHubCrawler;
        _azureDevOpsCrawler = azureDevOpsCrawler;
        _ospoCrawler = ospoCrawler;
    }

    public async Task CrawlAsync(SubscriptionConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _configuration = configuration;

        await _releaseCrawler.CrawlAsync();
        await _gitHubCrawler.CrawlRootsAsync(configuration.GitHubOrgs, this);
        await CrawlPendingAsync();
        await _ospoCrawler.CrawlAsync();

        await _releaseCrawler.SaveAsync();
        await _gitHubCrawler.SaveAsync();
        await _azureDevOpsCrawler.SaveAsync();
        await _ospoCrawler.SaveAsync();
    }

    private async Task CrawlPendingAsync()
    {
        while (_gitHubCrawler.HasPendingWork ||
               _azureDevOpsCrawler.HasPendingWork)
        {
            await _gitHubCrawler.CrawlPendingAsync(this);
            await _azureDevOpsCrawler.CrawlPendingAsync();
        }
    }

    public async Task LoadFromCacheAsync()
    {
        var workspaceData = await _workspaceDataCache.LoadAsync();

        _configuration = workspaceData.Configuration;

        _releaseCrawler.LoadFromCache(workspaceData.Releases);

        _gitHubCrawler.LoadFromCache(workspaceData.GitHubIssues,
                                     workspaceData.GitHubProjects,
                                     workspaceData.GitHubTransferMap);

        _azureDevOpsCrawler.LoadFromCache(workspaceData.AzureDevOpsWorkItems);

        _ospoCrawler.LoadFromCache(workspaceData.OspoLinks);
    }

    public async Task UpdateReleases()
    {
        await _releaseCrawler.UpdateAsync();
        await _releaseCrawler.SaveAsync();
    }

    public async Task UpdateGitHubAsync(GitHubEventMessage message)
    {
        await _gitHubCrawler.UpdateAsync(message, this);
        await CrawlPendingAsync();

        await _gitHubCrawler.SaveAsync();
        await _azureDevOpsCrawler.SaveAsync();
    }

    public async Task UpdateAzureDevOpsAsync()
    {
        await _azureDevOpsCrawler.UpdateAsync();
        await CrawlPendingAsync();

        await _gitHubCrawler.SaveAsync();
        await _azureDevOpsCrawler.SaveAsync();
    }

    public async Task UpdateOspoAsync()
    {
        await _ospoCrawler.UpdateAsync();
        await _ospoCrawler.SaveAsync();
    }

    public WorkspaceData GetSnapshot()
    {
        _releaseCrawler.GetSnapshot(out var releases);

        _gitHubCrawler.GetSnapshot(out var gitHubIssues,
                                   out var gitHubTransferMap,
                                   out var gitHubProjects);

        _azureDevOpsCrawler.GetSnapshot(out var azureDevOpsWorkItems);

        _ospoCrawler.GetSnapshot(out var ospoLinks);

        return new WorkspaceData(_configuration,
                                 releases,
                                 gitHubIssues,
                                 gitHubTransferMap,
                                 gitHubProjects,
                                 azureDevOpsWorkItems,
                                 ospoLinks);
    }

    void IWorkspaceCrawlerQueue.Enqueue(GitHubIssueId issueId)
    {
        _gitHubCrawler.Enqueue(issueId);
    }

    void IWorkspaceCrawlerQueue.Enqueue(AzureDevOpsQueryId queryId)
    {
        _azureDevOpsCrawler.Enqueue(queryId);
    }

    void IWorkspaceCrawlerQueue.Enqueue(AzureDevOpsWorkItemId workItemId)
    {
        _azureDevOpsCrawler.Enqueue(workItemId);
    }
}
