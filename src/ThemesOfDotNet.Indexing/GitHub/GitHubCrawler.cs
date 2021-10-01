
using Octokit;

using Spectre.Console;

namespace ThemesOfDotNet.Indexing.GitHub;

public abstract class GitHubCrawler
{
    private static readonly HashSet<string> _relevantEventNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "labeled",
        "unlabeled",
        "milestoned",
        "demilestoned",
        "assigned",
        "unassigned",
        "closed",
        "renamed",
        "reopened",
        "added_to_project",
        "moved_columns_in_project",
        "removed_from_project"
    };

    private readonly GitHubAppClient _client;

    public GitHubCrawler(string appId, string privateKey)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(privateKey);

        _client = new GitHubAppClient(new ProductHeaderValue("themesofdotnet"), appId, privateKey);
    }

    protected async Task<T> CallGitHub<T>(Func<GitHubClient, Task<T>> operation)
    {
        return await _client.InvokeAsync(operation);
    }

    protected static void FillRepo(GitHubRepo target, string owner, Repository repo)
    {
        target.Id = repo.Id;
        target.NodeId = repo.NodeId;
        target.Owner = owner;
        target.Name = repo.Name;
        target.IsPublic = !repo.Private;
    }

    protected static void FillMilestones(GitHubRepo target, IReadOnlyList<Milestone> milestones)
    {
        foreach (var milestone in milestones)
        {
            var convertedMilestone = ConvertMilestone(milestone);
            target.Milestones.Add(convertedMilestone);
        }
    }

    protected static void FillLabels(GitHubRepo target, IReadOnlyList<Label> labels)
    {
        foreach (var label in labels)
        {
            var convertedLabel = ConvertLabel(label);
            if (target.Labels.Contains(label.Id))
            {
                var existingLabel = target.Labels[label.Id];
                AnsiConsole.MarkupLine($"[yellow]warning: repo {target.FullName} has a duplicated label with ID {label.Id}. New: '{label.Name}'. Existing: '{existingLabel.Name}'[/]");
            }
            else
            {
                target.Labels.Add(convertedLabel);
            }
        }
    }

    protected static void FillLabel(GitHubLabel target, Label label)
    {
        target.Id = label.Id;
        target.NodeId = label.NodeId;
        target.Name = label.Name;
        target.Description = label.Description;
        target.Color = label.Color;
    }

    protected static void FillMilestone(GitHubMilestone target, Milestone milestone)
    {
        target.Id = milestone.Id;
        target.NodeId = milestone.NodeId;
        target.IsOpen = milestone.State.Value == ItemState.Open;
        target.Title = milestone.Title;
        target.Description = milestone.Description;
    }

    protected static void FillIssue(GitHubIssue target, GitHubRepo repo, Issue issue)
    {
        var id = issue.Id;
        var nodeId = issue.NodeId;
        var number = issue.Number;
        var isOpen = issue.State.Value == ItemState.Open;
        var title = issue.Title;
        var body = issue.Body ?? string.Empty;
        var assignees = issue.Assignees.Select(a => a.Login).ToArray();
        var labels = issue.Labels.Select(l => repo.Labels.GetValueOrDefault(l.Id))
                                 .Where(l => l is not null)
                                 .Select(l => l!)
                                 .ToArray();
        var milestone = issue.Milestone is null
                            ? null
                            : repo.Milestones.GetValueOrDefault(issue.Milestone.Id);
        var createdAt = issue.CreatedAt;
        var createdBy = issue.User.Login;
        var updatedAt = issue.UpdatedAt;
        var closedAt = issue.ClosedAt;
        var closedBy = issue.ClosedBy?.Login;

        target.Repo = repo;
        target.Id = id;
        target.NodeId = nodeId;
        target.Number = number;
        target.IsOpen = isOpen;
        target.Title = title;
        target.Body = body;
        target.Assignees = assignees;
        target.Labels = labels;
        target.Milestone = milestone;
        target.CreatedAt = createdAt;
        target.CreatedBy = createdBy;
        target.UpdatedAt = updatedAt;
        target.ClosedAt = closedAt;
        target.ClosedBy = closedBy;
    }

    protected static void FillTimeline(GitHubIssue issue, IReadOnlyList<TimelineEventInfo> timelineEvents)
    {
        var events = new List<GitHubIssueEvent>(timelineEvents.Count);

        foreach (var timelineEvent in timelineEvents)
        {
            if (!_relevantEventNames.Contains(timelineEvent.Event.StringValue))
                continue;

            var crawledEvent = new GitHubIssueEvent
            {
                Id = timelineEvent.Id,
                NodeId = timelineEvent.NodeId,
                Event = timelineEvent.Event.StringValue,
                Actor = timelineEvent.Actor?.Login,
                CreatedAt = timelineEvent.CreatedAt,
                CommitId = timelineEvent.CommitId,
                Assignee = timelineEvent.Assignee?.Login,
                Label = timelineEvent.Label?.Name,
                Milestone = timelineEvent.Milestone?.Title,
            };
            events.Add(crawledEvent);

            if (timelineEvent.Rename is not null)
            {
                crawledEvent.Rename = new GitHubRenameEvent
                {
                    From = timelineEvent.Rename.From,
                    To = timelineEvent.Rename.To
                };
            }

            if (timelineEvent.ProjectCard is not null)
            {
                crawledEvent.Card = new GitHubCardEvent
                {
                    CardId = timelineEvent.ProjectCard.Id,
                    ProjectId = timelineEvent.ProjectCard.ProjectId,
                    ColumnName = timelineEvent.ProjectCard.ColumnName,
                    PreviousColumnName = timelineEvent.ProjectCard.PreviousColumnName
                };
            }
        }

        issue.Events = events.ToArray();
    }

    protected static void FillProject(GitHubProject target, Project project)
    {
        target.Id = project.Id;
        target.NodeId = project.NodeId;
        target.Name = project.Name;
        target.Number = project.Number;
        target.CreatedAt = project.CreatedAt;
        target.CreatedBy = project.Creator.Login;
        target.UpdatedAt = project.UpdatedAt;
        target.Url = project.HtmlUrl;
    }

    protected static void FillProjectColumn(GitHubProjectColumn target, ProjectColumn column)
    {
        target.Id = column.Id;
        target.NodeId = column.NodeId;
        target.Name = column.Name;
        target.CreatedAt = column.CreatedAt;
        target.UpdatedAt = column.UpdatedAt;
    }

    protected static void FillProjectCard(GitHubCard target, ProjectCard card)
    {
        var issueIdText = (string?)null;

        if (GitHubIssueId.TryParse(card.ContentUrl, out var issueId) ||
            GitHubIssueId.TryParse(card.Note, out issueId))
        {
            issueIdText = issueId.ToString();
        }

        target.Id = card.Id;
        target.NodeId = card.NodeId;
        target.CreatedAt = card.CreatedAt;
        target.CreatedBy = card.Creator.Login;
        target.Note = card.Note;
        target.UpdatedAt = card.UpdatedAt;
        target.IssueId = issueIdText;
    }

    private static GitHubLabel ConvertLabel(Label label)
    {
        var result = new GitHubLabel();
        FillLabel(result, label);
        return result;
    }

    private static GitHubMilestone ConvertMilestone(Milestone milestone)
    {
        var result = new GitHubMilestone();
        FillMilestone(result, milestone);
        return result;
    }
}
