using System.Diagnostics;

namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed class WorkItemRoadmapEntry
{
    internal WorkItemRoadmapEntry(WorkItemRoadmap roadmap,
                                  WorkItem workItem,
                                  IReadOnlyList<(WorkItemMilestone Milestone, WorkItemState State)> states)
    {
        ArgumentNullException.ThrowIfNull(roadmap);
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(states);

        Roadmap = roadmap;
        WorkItem = workItem;
        States = states;
    }

    public Workspace Workspace => Roadmap.Workspace;

    public WorkItemRoadmap Roadmap { get; }

    public WorkItem WorkItem { get; }

    public IReadOnlyList<(WorkItemMilestone Milestone, WorkItemState State)> States { get; }

    public (WorkItemMilestone Milestone, WorkItemState State) GetState(WorkItemMilestone milestone)
    {
        var result = States[0];

        foreach (var (t, s) in States)
        {
            if (t.Version > milestone.Version)
                break;

            result = (t, s);
        }

        return result;
    }

    public void GetStates(WorkItemMilestone from,
                          WorkItemMilestone to,
                          out (WorkItemMilestone Milestone, WorkItemState State)? before,
                          out (WorkItemMilestone Milestone, WorkItemState State)? after,
                          out WorkItemState?[] states)
    {
        if (from.Product != to.Product)
            throw new ArgumentException("to must be the same product as from", nameof(to));

        if (to.Version < from.Version)
            throw new ArgumentOutOfRangeException(nameof(to), "to cannot be before from");

        var fromIndex = IndexOf(Roadmap.Milestones, from);
        var toIndex = IndexOf(Roadmap.Milestones, to);
        var count = toIndex - fromIndex + 1;

        Debug.Assert(fromIndex >= 0);
        Debug.Assert(toIndex >= 0);
        Debug.Assert(count > 0);

        before = null;
        after = null;
        states = new WorkItemState?[count];

        foreach (var (t, s) in States)
        {
            var index = IndexOf(Roadmap.Milestones, t);

            if (t.Version < from.Version)
                before = (t, s);
            else if (t.Version > to.Version)
                after = (t, s);
            else if (index >= 0)
                states[index - fromIndex] = s;
        }

        static int IndexOf(IReadOnlyList<WorkItemMilestone> list, WorkItemMilestone element)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == element)
                    return i;
            }

            return -1;
        }
    }
}
