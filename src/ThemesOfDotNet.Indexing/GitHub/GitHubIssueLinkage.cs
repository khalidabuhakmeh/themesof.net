
using ThemesOfDotNet.Indexing.AzureDevOps;

namespace ThemesOfDotNet.Indexing.GitHub;

public sealed class GitHubIssueLinkage
{
    public GitHubIssueLinkage(IReadOnlyList<GitHubIssueLink> issueLinks,
                              IReadOnlyList<GitHubWorkItemLink> workItemLinks,
                              IReadOnlyList<AzureDevOpsQueryId> queryIds)
    {
        ArgumentNullException.ThrowIfNull(issueLinks);
        ArgumentNullException.ThrowIfNull(workItemLinks);
        ArgumentNullException.ThrowIfNull(queryIds);

        IssueLinks = issueLinks;
        WorkItemLinks = workItemLinks;
        QueryIds = queryIds;
    }

    public IReadOnlyList<GitHubIssueLink> IssueLinks { get; }
    public IReadOnlyList<GitHubWorkItemLink> WorkItemLinks { get; }
    public IReadOnlyList<AzureDevOpsQueryId> QueryIds { get; }
}
