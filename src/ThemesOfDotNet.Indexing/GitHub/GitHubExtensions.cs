using Octokit;

namespace ThemesOfDotNet.Indexing.GitHub;

public static class GitHubExtensions
{
    public static GitHubIssueId GetId(this Issue issue)
    {
        return GitHubIssueId.Parse(issue.HtmlUrl);
    }

    public static GitHubIssueId GetId(this GitHubIssue issue)
    {
        return GitHubIssueId.Parse(issue.HtmlUrl);
    }

    public static GitHubRepoId GetId(this GitHubRepo repo)
    {
        return new GitHubRepoId(repo.Owner, repo.Name);
    }

    public static string FixedTitle(this GitHubIssue issue)
    {
        var labels = Constants.LabelsForThemesEpicsAndUserStories;
        var prefixes = labels.Select(l => $"[{l}]:")
                             .Concat(labels.Select(l => $"[{l}]"))
                             .Concat(labels.Select(l => $"{l}:"))
                             .ToArray();

        var result = issue.Title;

        foreach (var prefix in prefixes)
        {
            if (result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                result = result.Substring(prefix.Length).Trim();
        }

        return result;
    }
}
