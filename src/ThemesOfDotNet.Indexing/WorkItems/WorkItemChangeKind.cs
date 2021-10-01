namespace ThemesOfDotNet.Indexing.WorkItems;

public enum WorkItemChangeKind
{
    KindChanged,
    StateChanged,
    PriorityChanged,
    CostChanged,
    MilestoneChanged,
    TitleChanged,
    AssigneeAdded,
    AssigneeRemoved,
    IsBottomUpChanged
}
