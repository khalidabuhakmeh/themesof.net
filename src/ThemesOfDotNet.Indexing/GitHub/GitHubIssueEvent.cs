using System.Text;

namespace ThemesOfDotNet.Indexing.GitHub;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public sealed class GitHubIssueEvent
{
    public long Id { get; set; }
    public string NodeId { get; set; }
    public string Event { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? Actor { get; set; }
    public string? CommitId { get; set; }
    public string? Assignee { get; set; }
    public string? Label { get; set; }
    public string? Milestone { get; set; }
    public GitHubRenameEvent? Rename { get; set; }
    public GitHubCardEvent? Card { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.Append($"Event: {Event}, Created: {CreatedAt}");

        if (Actor is not null)
            sb.Append($", Actor: {Actor}");

        if (CommitId is not null)
            sb.Append($", CommitId: {CommitId}");

        if (Assignee is not null)
            sb.Append($", Assignee: {Assignee}");

        if (Label is not null)
            sb.Append($", Label: {Label}");

        if (Milestone is not null)
            sb.Append($", Label: {Milestone}");

        if (Card?.ProjectId is not null)
            sb.Append($", ProjectId: {Card.ProjectId}");

        if (Card?.CardId is not null)
            sb.Append($", CardId: {Card.CardId}");

        if (Card?.ColumnName is not null)
            sb.Append($", ColumnName: {Card.ColumnName}");

        if (Card?.PreviousColumnName is not null)
            sb.Append($", PreviousColumnName: {Card.PreviousColumnName}");

        return sb.ToString();
    }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
