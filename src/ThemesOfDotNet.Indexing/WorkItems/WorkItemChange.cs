using Humanizer;

namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed class WorkItemChange
{
    internal WorkItemChange(WorkItemUser actor,
                            DateTimeOffset when,
                            WorkItemChangeKind kind,
                            object? value,
                            object? previousValue)
    {
        ArgumentNullException.ThrowIfNull(actor);

        Actor = actor;
        When = when;
        Kind = kind;
        Value = value;
        PreviousValue = previousValue;
    }

    public Workspace Workspace => Actor.Workspace;

    public WorkItemUser Actor { get; }

    public DateTimeOffset When { get; }

    public WorkItemChangeKind Kind { get; }

    public object? Value { get; }

    public object? PreviousValue { get; }

    public override string ToString()
    {
        switch (Kind)
        {
            case WorkItemChangeKind.KindChanged:
            {
                var from = (WorkItemKind)PreviousValue!;
                var to = (WorkItemKind)Value!;
                return $"{When.Humanize()} {Actor} updated the issue kind from {from.Humanize()} to {to.Humanize()}";
            }
            case WorkItemChangeKind.StateChanged:
            {
                var from = (WorkItemState)PreviousValue!;
                var to = (WorkItemState)Value!;
                return $"{When.Humanize()} {Actor} updated the state from {from.Humanize()} to {to.Humanize()}";
            }
            case WorkItemChangeKind.PriorityChanged:
            {
                var from = ((int?)PreviousValue)?.ToString();
                var to = ((int?)Value)?.ToString();
                if (from is not null && to is not null)
                    return $"{When.Humanize()} {Actor} updated the priority from {from} to {to}";
                else if (from is null)
                    return $"{When.Humanize()} {Actor} set the priority to {to}";
                else
                    return $"{When.Humanize()} {Actor} cleared the priority";
            }
            case WorkItemChangeKind.CostChanged:
            {
                var from = ((WorkItemCost?)PreviousValue)?.Humanize();
                var to = ((WorkItemCost?)Value)?.Humanize();
                if (from is not null && to is not null)
                    return $"{When.Humanize()} {Actor} updated the cost from {from} to {to}";
                else if (from is null)
                    return $"{When.Humanize()} {Actor} set the cost to {to}";
                else
                    return $"{When.Humanize()} {Actor} cleared the cost";
            }
            case WorkItemChangeKind.MilestoneChanged:
            {
                var from = ((WorkItemMilestone?)PreviousValue)?.ToString();
                var to = ((WorkItemMilestone?)Value)?.ToString();
                if (from is not null && to is not null)
                    return $"{When.Humanize()} {Actor} updated the milestone from {from} to {to}";
                else if (from is null)
                    return $"{When.Humanize()} {Actor} set the milestone to {to}";
                else
                    return $"{When.Humanize()} {Actor} cleared the milestone";
            }
            case WorkItemChangeKind.TitleChanged:
            {
                var from = (string?)PreviousValue;
                var to = (string?)Value;
                return $"{When.Humanize()} {Actor} changed the title from '{from}' to '{to}'";
            }
            case WorkItemChangeKind.IsBottomUpChanged:
                var bottomUpOrTopDown = Value switch
                {
                    true => "bottom up",
                    _ => "top down"
                };
                return $"{When.Humanize()} {Actor} marked the issue as {bottomUpOrTopDown}";
            case WorkItemChangeKind.AssigneeAdded:
                return $"{When.Humanize()} {Actor} assigned {Value}";
            case WorkItemChangeKind.AssigneeRemoved:
                return $"{When.Humanize()} {Actor} unassigned {Value}";
            default:
                return $"On {When} {Actor} {Kind} from '{PreviousValue}' to '{Value}'";
        }
    }
}
