using System.Collections.ObjectModel;

namespace ThemesOfDotNet.Indexing.GitHub;

public sealed class GitHubMilestoneCollection : KeyedCollection<long, GitHubMilestone>
{
    protected override long GetKeyForItem(GitHubMilestone item)
    {
        return item.Id;
    }
}
