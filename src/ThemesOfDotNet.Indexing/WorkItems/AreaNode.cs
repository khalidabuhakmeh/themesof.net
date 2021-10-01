using ThemesOfDotNet.Indexing.Configuration;

namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed class AreaNode
{
    private IReadOnlyList<WorkItem>? _workItems;

    private AreaNode(Workspace workspace,
                     AreaNode? parent,
                     AreaNodeConfiguration nodeConfiguration)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(nodeConfiguration);

        Workspace = workspace;
        Parent = parent;
        Title = nodeConfiguration.Title;
        Areas = nodeConfiguration.Areas;
        Children = nodeConfiguration.Children.Select(nc => new AreaNode(workspace, this, nc))
                                             .ToArray();
    }

    internal static IReadOnlyList<AreaNode> CreateTree(Workspace workspace,
                                                       IReadOnlyList<AreaNodeConfiguration> areaTree)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(areaTree);

        return areaTree.Select(nc => new AreaNode(workspace, null, nc))
                       .ToArray();
    }

    public Workspace Workspace { get; }

    public string Title { get; }

    public IReadOnlyList<string> Areas { get; }

    public AreaNode? Parent { get; }

    public IReadOnlyList<AreaNode> Children { get; }

    public IReadOnlyList<WorkItem> WorkItems
    {
        get
        {
            if (_workItems is null)
            {
                var areas = Areas.ToHashSet();
                var workItems = new HashSet<WorkItem>();

                foreach (var workItem in Workspace.WorkItems)
                {
                    if (areas.Overlaps(workItem.Areas))
                        workItems.Add(workItem);
                }

                workItems.RemoveWhere(wi => wi.Ancestors().Any(a => workItems.Contains(a)));

                var sortedWorkItems = workItems.OrderBy(wi => wi).ToArray();
                Interlocked.CompareExchange(ref _workItems, sortedWorkItems, null);
            }

            return _workItems;
        }
    }
}
