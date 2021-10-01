namespace ThemesOfDotNet.Indexing.GitHub;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public sealed class GitHubCardEvent
{
    public long CardId { get; set; }
    public long ProjectId { get; set; }
    public string ColumnName { get; set; }
    public string? PreviousColumnName { get; set; }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
