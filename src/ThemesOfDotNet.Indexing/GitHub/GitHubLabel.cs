namespace ThemesOfDotNet.Indexing.GitHub;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public sealed class GitHubLabel
{
    public long Id { get; set; }
    public string NodeId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Color { get; set; }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
