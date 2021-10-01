using System.Text.RegularExpressions;

namespace ThemesOfDotNet.Indexing.GitHub;

public record struct GitHubIssueId(GitHubRepoId RepoId, int Number)
    : IComparable<GitHubIssueId>, IComparable
{
    public string Owner => RepoId.Owner;

    public string Repo => RepoId.Name;

    public GitHubIssueId(string Owner, string Name, int Number)
        : this(new GitHubRepoId(Owner, Name), Number)
    {
    }

    public void Deconstruct(out string Owner, out string Repo, out int Number)
    {
        Owner = this.Owner;
        Repo = this.Repo;
        Number = this.Number;
    }

    public int CompareTo(object? obj)
    {
        if (obj is GitHubIssueId other)
            return CompareTo(other);

        return 1;
    }

    public int CompareTo(GitHubIssueId other)
    {
        var result = RepoId.CompareTo(other.RepoId);
        if (result != 0)
            return result;

        return Number.CompareTo(other.Number);
    }

    public static GitHubIssueId Parse(string text)
    {
        if (TryParse(text, out var result))
            return result;

        throw new FormatException($"'{text}' isn't a valid GitHub issue");
    }

    public static bool TryParse(string? text, out GitHubIssueId result)
    {
        result = default;

        if (string.IsNullOrEmpty(text))
            return false;

        var match = Regex.Match(text, @"
            https?://github.com/(?<owner>[a-zA-Z0-9._-]+)/(?<repo>[a-zA-Z0-9._-]+)/(issues|pull)/(?<number>[0-9]+)|
            https?://api.github.com/repos/(?<owner>[a-zA-Z0-9._-]+)/(?<repo>[a-zA-Z0-9._-]+)/(issues|pull)/(?<number>[0-9]+)|
            (?<owner>[a-zA-Z0-9._-]+)/(?<repo>[a-zA-Z0-9._-]+)\#(?<number>[0-9]+)
        ", RegexOptions.IgnorePatternWhitespace);

        if (!match.Success)
            return false;

        var owner = match.Groups["owner"].Value;
        var repo = match.Groups["repo"].Value;
        var numberText = match.Groups["number"].Value;

        if (!int.TryParse(numberText, out var number))
            return false;

        result = new GitHubIssueId(owner, repo, number);
        return true;
    }

    public override string ToString()
    {
        return $"{Owner}/{Repo}#{Number}";
    }
}
