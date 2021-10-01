namespace ThemesOfDotNet.Indexing.AzureDevOps;

public sealed class AzureDevOpsWorkItem
{
    public AzureDevOpsWorkItem(string queryId,
                               int id,
                               string type,
                               string title,
                               string state,
                               long? priority,
                               string? cost,
                               string? milestone,
                               string? target,
                               string? release,
                               DateTime createdAt,
                               string createdBy,
                               string? assignedTo,
                               string url,
                               IReadOnlyList<string> tags,
                               IReadOnlyList<AzureDevOpsFieldChange> changes,
                               IReadOnlyList<int> childIds)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(createdBy);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(childIds);

        QueryId = queryId;
        Id = id;
        Type = type;
        Title = title;
        State = state;
        Priority = priority;
        Cost = cost;
        Milestone = milestone;
        Target = target;
        Release = release;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        AssignedTo = assignedTo;
        Url = url;
        Tags = tags;
        Changes = changes;
        ChildIds = childIds;
    }

    public string QueryId { get; }
    public int Id { get; }
    public string Type { get; }
    public string Title { get; }
    public string State { get; }
    public long? Priority { get; }
    public string? Cost { get; }
    public string? Milestone { get; }
    public string? Target { get; }
    public string? Release { get; }
    public DateTime CreatedAt { get; }
    public string CreatedBy { get; }
    public string? AssignedTo { get; }
    public string Url { get; }
    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyList<AzureDevOpsFieldChange> Changes { get; }
    public IReadOnlyList<int> ChildIds { get; }
}
