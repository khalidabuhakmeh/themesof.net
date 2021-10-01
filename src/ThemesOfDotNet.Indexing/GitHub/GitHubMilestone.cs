namespace ThemesOfDotNet.Indexing.GitHub;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public sealed class GitHubMilestone
{
    public long Id { get; set; }
    public string NodeId { get; set; }
    public bool IsOpen { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
