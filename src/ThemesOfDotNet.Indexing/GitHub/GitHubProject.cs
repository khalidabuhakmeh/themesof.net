namespace ThemesOfDotNet.Indexing.GitHub;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public sealed class GitHubProject
{
    public long Id { get; set; }
    public string NodeId { get; set; }
    public string Name { get; set; }
    public int Number { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Url { get; set; }
    public List<GitHubProjectColumn> Columns { get; set; } = new();
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
