using ThemesOfDotNet.Indexing.AzureDevOps;
using ThemesOfDotNet.Indexing.GitHub;

namespace ThemesOfDotNet.Indexing.WorkItems;

public interface IWorkspaceCrawlerQueue
{
    void Enqueue(GitHubIssueId issueId);
    void Enqueue(AzureDevOpsQueryId queryId);
    void Enqueue(AzureDevOpsWorkItemId workItemId);
}
