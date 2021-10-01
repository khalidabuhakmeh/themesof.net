using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet;

public static class UrlHelpers
{
    public static string GetUrl(this WorkItemKind kind)
    {
        return $"icons/kind_{kind.ToString().ToLowerInvariant()}.svg";
    }

    public static string GetUrl(this WorkItemState state)
    {
        return $"icons/state_{state.ToString().ToLowerInvariant()}.svg";
    }

    public static string GetUrl(this WorkItemChangeKind kind)
    {
        switch (kind)
        {
            case WorkItemChangeKind.PriorityChanged:
                return $"icons/change_priority.svg";
            case WorkItemChangeKind.CostChanged:
                return $"icons/change_cost.svg";
            case WorkItemChangeKind.MilestoneChanged:
                return $"icons/change_milestone.svg";
            default:
                throw new Exception(@"Unhandled value '{kind}'");
        }
    }
}
