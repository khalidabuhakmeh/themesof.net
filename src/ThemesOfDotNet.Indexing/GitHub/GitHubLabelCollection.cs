using System.Collections.ObjectModel;

namespace ThemesOfDotNet.Indexing.GitHub;

public sealed class GitHubLabelCollection : KeyedCollection<long, GitHubLabel>
{
    protected override long GetKeyForItem(GitHubLabel item)
    {
        return item.Id;
    }
}
