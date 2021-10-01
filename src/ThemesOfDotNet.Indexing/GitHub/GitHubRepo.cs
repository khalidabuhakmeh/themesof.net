using System.Text.Json.Serialization;

namespace ThemesOfDotNet.Indexing.GitHub;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public sealed class GitHubRepo
{
    public long Id { get; set; }

    public string NodeId { get; set; }

    public string Owner { get; set; }

    public string Name { get; set; }

    public bool IsPublic { get; set; }

    public GitHubLabelCollection Labels { get; set; } = new();

    public GitHubMilestoneCollection Milestones { get; set; } = new();

    public GitHubIssueCollection Issues { get; set; } = new();

    [JsonIgnore]
    public string FullName => $"{Owner}/{Name}";
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
