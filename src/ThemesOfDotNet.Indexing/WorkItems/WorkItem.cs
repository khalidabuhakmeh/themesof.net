namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed class WorkItem : IComparable<WorkItem>, IComparable
{
    private float? _percentComplete;

    internal WorkItem(Workspace workspace,
                      object original,
                      string id,
                      string url,
                      bool isPrivate,
                      bool isBottomUp,
                      WorkItemState state,
                      WorkItemKind kind,
                      string title,
                      int? priority,
                      WorkItemCost? cost,
                      DateTimeOffset createdAt,
                      WorkItemUser createdBy,
                      WorkItemMilestone? milestone,
                      IReadOnlyList<WorkItemUser> assignees,
                      IReadOnlyList<string> areas,
                      IReadOnlyList<string> teams,
                      IReadOnlyList<WorkItemChange> changes)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(createdBy);
        ArgumentNullException.ThrowIfNull(assignees);
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(teams);
        ArgumentNullException.ThrowIfNull(changes);

        Workspace = workspace;
        Original = original;
        Id = id;
        Url = url;
        IsPrivate = isPrivate;
        IsBottomUp = isBottomUp;
        State = state;
        Kind = kind;
        Title = title;
        Priority = priority;
        Cost = cost;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        Milestone = milestone;
        Assignees = assignees;
        Areas = areas;
        Teams = teams;
        Changes = changes;
    }

    public Workspace Workspace { get; }

    public object Original { get; }

    public string Id { get; }

    public string Url { get; }

    public bool IsPrivate { get; }

    public bool IsBottomUp { get; }

    public bool IsOpen => State == WorkItemState.Proposed ||
                          State == WorkItemState.Committed ||
                          State == WorkItemState.InProgress;

    public bool IsClosed => !IsOpen;

    public WorkItemState State { get; }

    public WorkItemKind Kind { get; }

    public string Title { get; }

    public int? Priority { get; }

    public WorkItemCost? Cost { get; }

    public DateTimeOffset CreatedAt { get; }

    public WorkItemUser CreatedBy { get; }

    public WorkItemMilestone? Milestone { get; }

    public IReadOnlyList<WorkItemUser> Assignees { get; }

    public IReadOnlyList<string> Areas { get; }

    public IReadOnlyList<string> Teams { get; }

    public IReadOnlyList<WorkItemChange> Changes { get; }

    public IReadOnlyList<WorkItem> Parents => Workspace.GetParents(this);

    public IReadOnlyList<WorkItem> Children => Workspace.GetChildren(this);

    public float PercentComplete
    {
        get
        {
            if (_percentComplete is null)
                _percentComplete = ComputePercentComplete();

            return _percentComplete.Value;
        }
    }

    private float ComputePercentComplete()
    {
        var openChildren = Descendants().Count(x => x.IsOpen);
        var closedChildren = Descendants().Count(x => x.IsClosed);
        var children = openChildren + closedChildren;
        return (float)closedChildren / children;
    }

    public IEnumerable<WorkItem> AncestorsAndSelf()
    {
        var stack = new Stack<WorkItem>();
        stack.Push(this);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;

            foreach (var child in node.Parents.AsEnumerable().Reverse())
                stack.Push(child);
        }
    }

    public IEnumerable<WorkItem> Ancestors()
    {
        return AncestorsAndSelf().Skip(1);
    }

    public IEnumerable<WorkItem> DescendantsAndSelf()
    {
        var stack = new Stack<WorkItem>();
        stack.Push(this);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;

            foreach (var child in node.Children.AsEnumerable().Reverse())
                stack.Push(child);
        }
    }

    public IEnumerable<WorkItem> Descendants()
    {
        return DescendantsAndSelf().Skip(1);
    }

    public int CompareTo(WorkItem? other)
    {
        if (other is null)
            return 1;

        if (Priority is not null || other.Priority is not null)
        {
            if (Priority is null)
                return 1;

            if (other.Priority is null)
                return -1;

            var result = Priority.Value.CompareTo(other.Priority.Value);
            if (result != 0)
                return result;
        }

        if (Kind != other.Kind)
            return Kind.CompareTo(other.Kind);

        return Title.CompareTo(other.Title);
    }

    int IComparable.CompareTo(object? obj)
    {
        return CompareTo(obj as WorkItem);
    }

    public override string ToString()
    {
        return $"{Kind}: {Title} ({Id})";
    }
}
