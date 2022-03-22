using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Indexing.PageTrees;

public sealed class PageTree
{
    public static PageTree Empty { get; } = new PageTree(Array.Empty<PageNode>());

    private PageTree(IReadOnlyList<PageNode> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);

        Roots = roots;
    }

    public IReadOnlyList<PageNode> Roots { get; }

    public static PageTree CreateFlat(Workspace workspace, Predicate<WorkItem> predicate)
    {
        var roots = workspace.WorkItems.Where(wi => predicate(wi))
                                       .Select(wi => new PageNode(wi, Array.Empty<PageNode>()))
                                       .ToArray();

        return new PageTree(roots);
    }

    public static PageTree CreateTree(Workspace workspace, Predicate<WorkItem> predicate)
    {
        var roots = workspace.RootWorkItems.Where(wi => predicate(wi))
                                           .Select(wi => ToPageNode(wi, predicate))
                                           .ToArray();

        return new PageTree(roots);
    }

    public static PageTree CreateTree(WorkItem root, Predicate<WorkItem> predicate)
    {
        return new PageTree(new[] { ToPageNode(root, predicate) });
    }

    public static PageTree CreateThematicTree(Workspace workspace, Predicate<WorkItem> predicate)
    {
        var roots = workspace.RootWorkItems.Where(wi => wi.Kind == WorkItemKind.Theme && predicate(wi))
                                           .Select(wi => ToPageNode(wi, predicate))
                                           .ToArray();

        return new PageTree(roots);
    }

    public static PageTree CreateAreaTree(Workspace workspace, Predicate<WorkItem> predicate)
    {
        var roots = workspace.AreaNodes.Where(n => IncludeAreaNode(n, predicate))
                                       .Select(n => ToPageNode(n, predicate))
                                       .ToArray();

        return new PageTree(roots);
    }

    public static PageTree CreateTeamDependencies(Workspace workspace, Predicate<WorkItem> predicate, string team)
    {
        return CreateTeamDependencies(workspace, predicate, team, dependencies: true);
    }

    public static PageTree CreateTeamDependents(Workspace workspace, Predicate<WorkItem> predicate, string team)
    {
        return CreateTeamDependencies(workspace, predicate, team, dependencies: false);
    }

    private static PageTree CreateTeamDependencies(Workspace workspace, Predicate<WorkItem> predicate, string team, bool dependencies = true)
    {
        var includedWorkItemsByTeam = new Dictionary<string, HashSet<WorkItem>>(StringComparer.OrdinalIgnoreCase);

        bool IsForMyTeam(WorkItem workItem)
        {
            return workItem.Teams.Contains(team, StringComparer.OrdinalIgnoreCase);
        }

        bool IsNotForMyTeam(WorkItem workItem)
        {
            return workItem.Teams.Any() && !workItem.Teams.Contains(team, StringComparer.OrdinalIgnoreCase);
        }

        var parentTest = IsForMyTeam;
        var childTest = IsNotForMyTeam;

        if (!dependencies)
            (parentTest, childTest) = (childTest, parentTest);

        foreach (var parent in workspace.WorkItems.Where(p => predicate(p)))
        {
            if (parentTest(parent))
            {
                foreach (var child in parent.Children.Where(c => predicate(c)))
                {
                    if (childTest(child))
                    {
                        var otherTeams = dependencies ? child.Teams : parent.Teams;

                        foreach (var otherTeam in otherTeams)
                        {
                            if (!includedWorkItemsByTeam.TryGetValue(otherTeam, out var workItems))
                            {
                                workItems = new();
                                includedWorkItemsByTeam.Add(otherTeam, workItems);
                            }

                            workItems.Add(child);
                            workItems.UnionWith(parent.AncestorsAndSelf());
                        }
                    }
                }
            }
        }

        var otherTeamNodes = new List<PageNode>();

        foreach (var (otherTeam, workItemSet) in includedWorkItemsByTeam.OrderBy(kv => kv.Key))
        {
            var teamTree = CreateTree(workspace, workItemSet.Contains);
            var teamNode = new PageNode(otherTeam, teamTree.Roots);
            otherTeamNodes.Add(teamNode);
        }

        return new PageTree(otherTeamNodes.ToArray());
    }

    private static bool IncludeAreaNode(AreaNode node, Predicate<WorkItem> predicate)
    {
        return node.WorkItems.Any(wi => predicate(wi)) ||
               node.Children.Any(c => IncludeAreaNode(c, predicate));
    }

    private static PageNode ToPageNode<T>(T data, Func<T, IEnumerable<T>> childrenSelector)
        where T : notnull
    {
        var children = childrenSelector(data).Select(c => ToPageNode(c, childrenSelector))
                                             .ToArray();
        return new PageNode(data, children);
    }

    private static PageNode ToPageNode(WorkItem workItem, Predicate<WorkItem> predicate)
    {
        return ToPageNode(workItem, c => c.Children.Where(c => predicate(c)));
    }

    private static PageNode ToPageNode(AreaNode areaNode, Predicate<WorkItem> predicate)
    {
        var areaNodeChildren = areaNode.Children.Where(n => IncludeAreaNode(n, predicate))
                                                .Select(n => ToPageNode(n, predicate));
        var workItemChildren = areaNode.WorkItems.Where(c => predicate(c))
                                                 .Select(wi => ToPageNode(wi, predicate));
        var children = areaNodeChildren.Concat(workItemChildren);
        return new PageNode(areaNode, children);
    }

    public PageTree Filter(Predicate<PageNode> predicate)
    {
        return Filter((p, n) => predicate(n));
    }

    public PageTree Filter(Func<PageNode?, PageNode, bool> predicate)
    {
        var roots = Filter(parent: null, Roots, predicate);

        return new PageTree(roots);
    }

    private static IReadOnlyList<PageNode> Filter(PageNode? parent, IReadOnlyList<PageNode> nodes, Func<PageNode?, PageNode, bool> predicate)
    {
        return nodes.Select(c => Filter(parent, c, predicate))
                   .Where(n => n is not null)
                   .Select(n => n!)
                   .ToArray();
    }

    private static PageNode? Filter(PageNode? parent, PageNode node, Func<PageNode?, PageNode, bool> predicate)
    {
        var children = Filter(node, node.Children, predicate);

        var includedDirectly = predicate(parent, node);
        var includedIndirectly = children.Any();

        if (!includedDirectly && !includedIndirectly)
            return null;

        return new PageNode(node.Data, children, isExcluded: !includedDirectly);
    }

    public void ApplyQuickFilter(Predicate<PageNode>? predicate)
    {
        foreach (var node in Roots)
        {
            if (predicate is null)
                ClearQuickFilter(node);
            else
                ApplyQuickFilter(predicate, node, parentIsIncluded: false);
        }
    }

    private void ClearQuickFilter(PageNode node)
    {
        node.IsMuted = node.IsExcluded;
        node.IsVisible = true;

        foreach (var child in node.Children)
            ClearQuickFilter(child);
    }

    private bool ApplyQuickFilter(Predicate<PageNode> predicate, PageNode node, bool parentIsIncluded)
    {
        var isIncluded = predicate(node);
        var anyChildrenVisible = false;
        var anyChildrenIncluded = false;

        foreach (var child in node.Children)
        {
            anyChildrenIncluded |= ApplyQuickFilter(predicate, child, isIncluded);
            anyChildrenVisible |= child.IsVisible;
        }

        node.IsVisible = parentIsIncluded ||
                         isIncluded ||
                         anyChildrenVisible;

        node.IsMuted = node.IsVisible && !isIncluded;
        node.IsExpanded = anyChildrenIncluded;

        return isIncluded || anyChildrenIncluded;
    }

    public void ExpandAll()
    {
        ExpandCollapseAll(true);
    }

    public void CollapseAll()
    {
        ExpandCollapseAll(false);
    }

    private void ExpandCollapseAll(bool expand)
    {
        foreach (var node in Roots)
            ExpandCollapseAll(node, expand);
    }

    private static void ExpandCollapseAll(PageNode node, bool expand)
    {
        node.IsExpanded = expand;

        foreach (var child in node.Children)
            ExpandCollapseAll(child, expand);
    }

    public void ExpandUnmatched()
    {
        foreach (var node in Roots)
            ExpandUnmatched(node);
    }

    private static void ExpandUnmatched(PageNode node)
    {
        node.IsExpanded = node.IsMuted;

        foreach (var child in node.Children)
            ExpandUnmatched(child);
    }
}
