
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Services;

public sealed class WorkItemAccessCheck
{
    public static WorkItemAccessCheck None => new(wi => false);

    public static WorkItemAccessCheck All => new(wi => true);

    public static WorkItemAccessCheck PublicOnly => new(wi => !wi.IsPrivate);

    private readonly Predicate<WorkItem> _predicate;

    private WorkItemAccessCheck(Predicate<WorkItem> predicate)
    {
        _predicate = predicate;
    }

    public bool Perform(WorkItem workItem)
    {
        return _predicate(workItem);
    }

    public override string ToString()
    {
        if (this == None) return "None";
        if (this == All) return "All";
        if (this == PublicOnly) return "Public Only";
        return "Unknown";
    }
}
