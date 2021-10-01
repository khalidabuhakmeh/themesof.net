using System.Text.Json.Serialization;

namespace ThemesOfDotNet.Indexing.GitHub;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public sealed class GitHubIssue
{
    public GitHubRepo Repo { get; set; }
    public int Id { get; set; }
    public string NodeId { get; set; }
    public int Number { get; set; }
    public bool IsOpen { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public IReadOnlyList<string> Assignees { get; set; }
    public IReadOnlyList<GitHubLabel> Labels { get; set; }
    public GitHubMilestone? Milestone { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public IReadOnlyList<GitHubIssueEvent> Events { get; set; } = Array.Empty<GitHubIssueEvent>();

    [JsonIgnore]
    public string HtmlUrl => $"https://github.com/{Repo.FullName}/issues/{Number}";

    public override string ToString()
    {
        return $"{Repo.FullName}#{Number}: {Title}";
    }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
