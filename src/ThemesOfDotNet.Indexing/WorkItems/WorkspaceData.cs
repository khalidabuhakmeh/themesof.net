
using ThemesOfDotNet.Indexing.AzureDevOps;
using ThemesOfDotNet.Indexing.Configuration;
using ThemesOfDotNet.Indexing.GitHub;
using ThemesOfDotNet.Indexing.Ospo;
using ThemesOfDotNet.Indexing.Releases;

namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed class WorkspaceData
{
    public WorkspaceData(SubscriptionConfiguration configuration,
                         IReadOnlyList<ReleaseInfo> releases,
                         IReadOnlyList<GitHubIssue> gitHubIssues,
                         IReadOnlyDictionary<GitHubIssueId, GitHubIssueId> gitHubTransferMap,
                         IReadOnlyList<GitHubProject> gitHubProjects,
                         IReadOnlyList<AzureDevOpsWorkItem> azureDevOpsWorkItems,
                         IReadOnlyList<OspoLink> ospoLinks)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(gitHubIssues);
        ArgumentNullException.ThrowIfNull(gitHubTransferMap);
        ArgumentNullException.ThrowIfNull(gitHubProjects);
        ArgumentNullException.ThrowIfNull(azureDevOpsWorkItems);
        ArgumentNullException.ThrowIfNull(ospoLinks);

        Configuration = configuration;
        Releases = releases;
        GitHubIssues = gitHubIssues;
        GitHubTransferMap = gitHubTransferMap;
        GitHubProjects = gitHubProjects;
        AzureDevOpsWorkItems = azureDevOpsWorkItems;
        OspoLinks = ospoLinks;
    }

    public SubscriptionConfiguration Configuration { get; }

    public IReadOnlyList<ReleaseInfo> Releases { get; }

    public IReadOnlyList<GitHubIssue> GitHubIssues { get; }

    public IReadOnlyDictionary<GitHubIssueId, GitHubIssueId> GitHubTransferMap { get; }

    public IReadOnlyList<GitHubProject> GitHubProjects { get; }

    public IReadOnlyList<AzureDevOpsWorkItem> AzureDevOpsWorkItems { get; }

    public IReadOnlyList<OspoLink> OspoLinks { get; }
}
