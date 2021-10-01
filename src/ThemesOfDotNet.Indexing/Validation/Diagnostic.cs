using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Indexing.Validation;

public sealed class Diagnostic
{
    public Diagnostic(string id, string message, IReadOnlyList<WorkItem> workItems)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(workItems);

        Id = id;
        Message = message;
        WorkItems = workItems;
        Assignees = GetAssignees(workItems);
    }

    public string Id { get; }
    public string Message { get; }
    public IReadOnlyList<WorkItem> WorkItems { get; }
    public IReadOnlyList<WorkItemUser> Assignees { get; }

    private static IReadOnlyList<WorkItemUser> GetAssignees(IEnumerable<WorkItem> workItems)
    {
        var result = new HashSet<WorkItemUser>();

        foreach (var workItem in workItems)
        {
            if (workItem.Assignees.Any())
                result.UnionWith(workItem.Assignees);
            else
                result.Add(workItem.CreatedBy);
        }

        return result.OrderBy(u => u.DisplayName).ToArray();
    }
}
