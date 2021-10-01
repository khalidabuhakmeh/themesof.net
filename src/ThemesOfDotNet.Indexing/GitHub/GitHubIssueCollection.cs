using System.Collections.ObjectModel;

namespace ThemesOfDotNet.Indexing.GitHub;

public sealed class GitHubIssueCollection : KeyedCollection<int, GitHubIssue>
{
    protected override int GetKeyForItem(GitHubIssue item)
    {
        return item.Number;
    }
}
