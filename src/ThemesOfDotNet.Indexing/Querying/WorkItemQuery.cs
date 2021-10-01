using ThemesOfDotNet.Indexing.GitHub;
using ThemesOfDotNet.Indexing.Querying.Ranges;
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Indexing.Querying;

public sealed class WorkItemQuery : Query<WorkItem>
{
    private static readonly QueryHandlers _handlers = CreateHandlers(typeof(WorkItemQuery));

    public static WorkItemQuery Empty { get; } = new WorkItemQuery(QueryContext.Empty, string.Empty);

    public WorkItemQuery(QueryContext context, string text)
        : base(context, text)
    {
    }

    public WorkItemGrouping Grouping { get; private set; }

    protected override QueryHandlers GetHandlers()
    {
        return _handlers;
    }

    protected override bool ContainsText(WorkItem value, string text)
    {
        return value.Title.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    [QueryHandler("group:none")]
    private void GroupByNone()
    {
        Grouping = WorkItemGrouping.None;
    }

    [QueryHandler("group:parent")]
    private void GroupByParent()
    {
        Grouping = WorkItemGrouping.Parent;
    }

    [QueryHandler("group:theme")]
    private void GroupByTheme()
    {
        Grouping = WorkItemGrouping.Theme;
    }

    [QueryHandler("group:area")]
    private void GroupByArea()
    {
        Grouping = WorkItemGrouping.Area;
    }

    [QueryHandler("is:private")]
    private static bool IsPrivate(WorkItem workItem)
    {
        return workItem.IsPrivate;
    }

    [QueryHandler("is:bottomup")]
    private static bool IsBottomUp(WorkItem workItem)
    {
        return workItem.IsBottomUp;
    }

    [QueryHandler("is:open", "state:open")]
    private static bool IsOpen(WorkItem workItem)
    {
        return workItem.IsOpen;
    }

    [QueryHandler("is:closed", "state:closed")]
    private static bool IsClosed(WorkItem workItem)
    {
        return workItem.IsClosed;
    }

    [QueryHandler("is:proposed", "state:proposed")]
    private static bool IsProposed(WorkItem workItem)
    {
        return workItem.State == WorkItemState.Proposed;
    }

    [QueryHandler("is:committed", "state:committed")]
    private static bool IsCommitted(WorkItem workItem)
    {
        return workItem.State == WorkItemState.Committed;
    }

    [QueryHandler("is:inprogress", "state:inprogress")]
    private static bool IsInProgress(WorkItem workItem)
    {
        return workItem.State == WorkItemState.InProgress;
    }

    [QueryHandler("is:cut", "state:cut")]
    private static bool IsCut(WorkItem workItem)
    {
        return workItem.State == WorkItemState.Cut;
    }

    [QueryHandler("is:completed", "state:completed")]
    private static bool IsCompleted(WorkItem workItem)
    {
        return workItem.State == WorkItemState.Completed;
    }

    [QueryHandler("is:theme", "type:theme")]
    private static bool IsTheme(WorkItem workItem)
    {
        return workItem.Kind == WorkItemKind.Theme;
    }

    [QueryHandler("is:epic", "type:epic")]
    private static bool IsEpic(WorkItem workItem)
    {
        return workItem.Kind == WorkItemKind.Epic;
    }

    [QueryHandler("is:userstory", "type:userstory")]
    private static bool IsUserStory(WorkItem workItem)
    {
        return workItem.Kind == WorkItemKind.UserStory;
    }

    [QueryHandler("is:task", "type:task")]
    private static bool IsTask(WorkItem workItem)
    {
        return workItem.Kind == WorkItemKind.Task;
    }

    [QueryHandler("team")]
    private static bool HasTeam(WorkItem workItem, string value)
    {
        return workItem.Teams.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    [QueryHandler("area")]
    private static bool HasArea(WorkItem workItem, string value)
    {
        return workItem.Areas.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    [QueryHandler("assignee")]
    private static bool HasAssignee(WorkItem workItem, string value)
    {
        return workItem.Assignees.Any(u => u.Matches(value));
    }

    [QueryHandler("milestone")]
    private static bool HasMilestone(WorkItem workItem, RangeSyntax<WorkItemVersion> value)
    {
        return workItem.Milestone is not null &&
               value.Contains(workItem.Milestone.Version);
    }

    [QueryHandler("product")]
    private static bool HasProduct(WorkItem workItem, string value)
    {
        return string.Equals(workItem.Milestone?.Product.Name, value, StringComparison.OrdinalIgnoreCase);
    }

    [QueryHandler("cost")]
    private static bool HasCost(WorkItem workItem, RangeSyntax<WorkItemCost> range)
    {
        return workItem.Cost is not null && range.Contains(workItem.Cost.Value);
    }

    [QueryHandler("author")]
    private static bool HasAuthor(WorkItem workItem, string value)
    {
        return workItem.CreatedBy.Matches(value);
    }

    [QueryHandler("created")]
    private static bool HasCreated(WorkItem workItem, RangeSyntax<DateTimeOffset> range)
    {
        return range.Contains(workItem.CreatedAt);
    }

    [QueryHandler("priority")]
    private static bool HasPriority(WorkItem workItem, RangeSyntax<int> range)
    {
        return workItem.Priority is not null && range.Contains(workItem.Priority.Value);
    }

    [QueryHandler("org")]
    private static bool HasOrg(WorkItem workItem, string value)
    {
        return workItem.Original is GitHubIssue issue &&
               string.Equals(issue.Repo.Owner, value, StringComparison.OrdinalIgnoreCase);
    }

    [QueryHandler("repo")]
    private static bool HasRepo(WorkItem workItem, string value)
    {
        return workItem.Original is GitHubIssue issue &&
               (string.Equals(issue.Repo.Name, value, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Repo.FullName, value, StringComparison.OrdinalIgnoreCase));
    }

    [QueryHandler("no:assignee")]
    private static bool HasNoAssignees(WorkItem workItem)
    {
        return workItem.Assignees.Count == 0;
    }

    [QueryHandler("no:milestone")]
    private static bool HasNoMilestone(WorkItem workItem)
    {
        return workItem.Milestone is null;
    }

    [QueryHandler("no:cost")]
    private static bool HasNoCost(WorkItem workItem)
    {
        return workItem.Cost is null;
    }

    [QueryHandler("no:priority")]
    private static bool HasNoPriority(WorkItem workItem)
    {
        return workItem.Priority is null;
    }
}
