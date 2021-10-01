namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed class WorkItemProduct
{
    private IReadOnlyList<WorkItemMilestone>? _milestones;

    internal WorkItemProduct(Workspace workspace,
                           string name)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(name);
        Workspace = workspace;
        Name = name;
    }

    public Workspace Workspace { get; }

    public string Name { get; }

    public IReadOnlyCollection<WorkItemMilestone> Milestones
    {
        get
        {
            if (_milestones is null)
            {
                var milestones = Workspace.Milestones.Where(m => m.Product == this)
                                                     .ToArray();
                Interlocked.CompareExchange(ref _milestones, milestones, null);
            }

            return _milestones;
        }
    }

    public override string ToString()
    {
        return Name;
    }
}
