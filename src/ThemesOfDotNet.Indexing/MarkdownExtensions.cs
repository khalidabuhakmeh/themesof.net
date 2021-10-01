using ThemesOfDotNet.Indexing.GitHub;
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Indexing;

internal static class MarkdownExtensions
{
    public static string ToMarkdownLink(this GitHubIssueId id)
    {
        return $"[{id}](https://github.com/{id.Owner}/{id.Repo}/issues/{id.Number})";
    }

    public static string ToMarkdownLink(this WorkItem workItem)
    {
        return $"[{workItem.Id}]({workItem.Url})";
    }
}
