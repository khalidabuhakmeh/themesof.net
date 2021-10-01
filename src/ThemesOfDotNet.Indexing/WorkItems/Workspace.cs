using ThemesOfDotNet.Indexing.Configuration;
using ThemesOfDotNet.Indexing.Storage;
using ThemesOfDotNet.Indexing.Validation;

namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed partial class Workspace
{
    public static Workspace Empty { get; } = new();

    private readonly IReadOnlyDictionary<WorkItem, IReadOnlyList<WorkItem>> _parentsByChild;
    private readonly IReadOnlyDictionary<WorkItem, IReadOnlyList<WorkItem>> _childrenByParent;

    private Workspace()
    {
        Configuration = SubscriptionConfiguration.Empty;
        WorkItems = Array.Empty<WorkItem>();
        RootWorkItems = Array.Empty<WorkItem>();
        AreaNodes = Array.Empty<AreaNode>();
        Users = Array.Empty<WorkItemUser>();
        Products = Array.Empty<WorkItemProduct>();
        Milestones = Array.Empty<WorkItemMilestone>();
        ConstructionDiagnostics = Array.Empty<Diagnostic>();
        _parentsByChild = new Dictionary<WorkItem, IReadOnlyList<WorkItem>>();
        _childrenByParent = new Dictionary<WorkItem, IReadOnlyList<WorkItem>>();
    }

    private Workspace(WorkspaceData workspaceData)
    {
        var workItemBuilder = new WorkItemBuilder();
        var userBuilder = new WorkItemUserBuilder(this, workspaceData.OspoLinks);
        var milestoneBuilder = new WorkItemMilestoneBuilder(this, workspaceData.Releases, workspaceData.Configuration);

        var gitHub = new GitHubWorkItemBuilder(this, workItemBuilder, userBuilder, milestoneBuilder);
        gitHub.Build(workspaceData.Configuration,
                     workspaceData.GitHubIssues,
                     workspaceData.GitHubTransferMap,
                     workspaceData.GitHubProjects);

        var azureDevOps = new AzureDevOpsWorkItemBuilder(this, workItemBuilder, userBuilder, milestoneBuilder);
        azureDevOps.Build(workspaceData.Configuration,
                          workspaceData.AzureDevOpsWorkItems);

        workItemBuilder.Build(out var workItems,
                              out var parentsByChild,
                              out var childrenByParent,
                              out var constructionDiagnostics);

        Configuration = workspaceData.Configuration;
        WorkItems = workItems;
        RootWorkItems = workItems.Where(wi => !parentsByChild.ContainsKey(wi)).OrderBy(wi => wi).ToArray();
        AreaNodes = AreaNode.CreateTree(this, workspaceData.Configuration.Tree);
        Users = userBuilder.Users.OrderBy(u => u.DisplayName).ToArray();
        Products = milestoneBuilder.GetProducts();
        Milestones = milestoneBuilder.GetMilestones();
        ConstructionDiagnostics = constructionDiagnostics;

        _parentsByChild = parentsByChild;
        _childrenByParent = childrenByParent;
    }

    public SubscriptionConfiguration Configuration { get; }

    public IReadOnlyList<WorkItem> WorkItems { get; }

    public IReadOnlyList<WorkItem> RootWorkItems { get; }

    public IReadOnlyList<AreaNode> AreaNodes { get; }

    public IReadOnlyList<WorkItemUser> Users { get; }

    public IReadOnlyList<WorkItemProduct> Products { get; }

    public IReadOnlyList<WorkItemMilestone> Milestones { get; }

    internal IReadOnlyList<Diagnostic> ConstructionDiagnostics { get; }

    public static Workspace Create(WorkspaceData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new Workspace(data);
    }

    public static async Task<Workspace> LoadFromDirectoryAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var workspaceDataStore = new FileSystemStore(path);
        var workspaceDataCache = new WorkspaceDataCache(workspaceDataStore);
        var workspaceData = await workspaceDataCache.LoadAsync();
        return Create(workspaceData);
    }

    internal IReadOnlyList<WorkItem> GetParents(WorkItem workItem)
    {
        return _parentsByChild.GetValueOrDefault(workItem, Array.Empty<WorkItem>());
    }

    internal IReadOnlyList<WorkItem> GetChildren(WorkItem workItem)
    {
        return _childrenByParent.GetValueOrDefault(workItem, Array.Empty<WorkItem>());
    }

    public IReadOnlyList<Diagnostic> GetDiagnostics()
    {
        return ValidationEngine.Run(this);
    }
}
