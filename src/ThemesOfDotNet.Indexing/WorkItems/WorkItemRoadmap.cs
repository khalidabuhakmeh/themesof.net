using System.Runtime.CompilerServices;

using ThemesOfDotNet.Indexing.AzureDevOps;
using ThemesOfDotNet.Indexing.GitHub;

namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed class WorkItemRoadmap
{
    public static WorkItemRoadmap Empty { get; } = new();

    private readonly IReadOnlyDictionary<WorkItem, WorkItemRoadmapEntry> _map;

    private WorkItemRoadmap()
    {
        _map = new Dictionary<WorkItem, WorkItemRoadmapEntry>();
        Workspace = Workspace.Empty;
        Milestones = Array.Empty<WorkItemMilestone>();
    }

    private WorkItemRoadmap(Workspace workspace,
                            WorkItemProduct product,
                            Func<WorkItemMilestone, bool> milestoneFilter)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(milestoneFilter);

        var allMilestones = new HashSet<WorkItemMilestone>();
        allMilestones.UnionWith(product.Milestones.Where(m => m.Version.Build == 0));

        var selectedMilestones = allMilestones.OrderBy(m => m.Version).ToArray();

        var entries = new Dictionary<WorkItem, WorkItemRoadmapEntry>();

        var states = new List<(WorkItemMilestone, WorkItemState)>();

        foreach (var workItem in workspace.WorkItems)
        {
            states.Clear();

            var relevantProduct = GetRelevantProduct(workItem);
            if (relevantProduct != product)
                continue;

            var nextMilestone = (WorkItemMilestone?)null;
            var nextState = (WorkItemState?)null;

            for (var i = workItem.Changes.Count - 1; i >= 0; i--)
            {
                var stateChange = workItem.Changes[i];

                if (stateChange.Kind != WorkItemChangeKind.StateChanged)
                    continue;

                if (stateChange.Value is not WorkItemState previousState)
                    continue;

                if (nextState is not null)
                {
                    if (IsHigherOrEqual(previousState, nextState.Value))
                    {
                        // That means a later state "corrected" an earlier state.
                        // Let's ignore the earlier state then.
                        continue;
                    }
                }

                var previousMilestone = GetMilestone(selectedMilestones, stateChange.When);
                if (previousMilestone is null)
                    continue;

                if (nextMilestone is not null)
                {
                    if (previousMilestone.Version >= nextMilestone.Version)
                    {
                        // That means a later state "corrected" an earlier state.
                        // Let's ignore the earlier state then.
                        continue;
                    }
                }

                states.Add((previousMilestone, previousState));
                nextMilestone = previousMilestone;
                nextState = previousState;
            }

            if (states.Count == 0)
            {
                if (workItem.Milestone is not null)
                {
                    states.Add((workItem.Milestone, workItem.State));
                    nextState = workItem.State;
                }
            }

            if (nextState != WorkItemState.Proposed)
            {
                var proposedMilestone = GetMilestone(selectedMilestones, workItem.CreatedAt);
                if (proposedMilestone is not null)
                    states.Add((proposedMilestone, WorkItemState.Proposed));
            }

            static bool IsHigherOrEqual(WorkItemState left, WorkItemState right)
            {
                return Rank(left) >= Rank(right);

                static int Rank(WorkItemState state)
                {
                    return state switch
                    {
                        WorkItemState.Proposed => 0,
                        WorkItemState.Committed => 1,
                        WorkItemState.InProgress => 2,
                        WorkItemState.Cut or WorkItemState.Completed => 3,
                        _ => throw new SwitchExpressionException(state)
                    };
                }
            }

            if (states.Count > 0)
            {
                states.Reverse();

                var data = new WorkItemRoadmapEntry(this, workItem, states.ToArray());
                entries.Add(workItem, data);
            }
        }

        Workspace = workspace;
        Milestones = selectedMilestones.Where(milestoneFilter).ToArray();
        _map = entries;
    }

    private WorkItemProduct? GetRelevantProduct(WorkItem workItem)
    {
        if (workItem.Milestone is not null)
            return workItem.Milestone.Product;

        for (var i = workItem.Changes.Count - 1; i >= 0; i--)
        {
            var change = workItem.Changes[i];
            var milestone = change.Value as WorkItemMilestone;
            if (milestone is not null)
                return milestone.Product;
        }

        var products = workItem.Workspace.Products;
        var config = workItem.Workspace.Configuration;
        var defaultProductName = (string?)null;

        if (workItem.Original is AzureDevOpsWorkItem item)
        {
            if (config.AzureDevOpsQueryById.TryGetValue(item.QueryId, out var queryConfiguration))
                defaultProductName = queryConfiguration.DefaultProduct;
        }

        if (workItem.Original is GitHubIssue issue)
        {
            var org = issue.Repo.Owner;
            var repo = issue.Repo.Name;

            if (config.GitHubOrgByName.TryGetValue(org, out var orgConfiguration))
            {
                if (orgConfiguration.Repos.TryGetValue(repo, out var repoConfiguration) && repoConfiguration.DefaultProduct is not null)
                    defaultProductName = repoConfiguration.DefaultProduct;
                else
                    defaultProductName = orgConfiguration.DefaultProduct;
            }
        }

        return products.FirstOrDefault(p => string.Equals(p.Name, defaultProductName, StringComparison.OrdinalIgnoreCase));
    }

    public Workspace Workspace { get; }

    public IReadOnlyList<WorkItemMilestone> Milestones { get; }

    public WorkItemRoadmapEntry? GetEntry(WorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        return _map.GetValueOrDefault(workItem);
    }

    public static WorkItemRoadmap Create(Workspace workspace,
                                         WorkItemProduct product,
                                         Func<WorkItemMilestone, bool> milestoneFilter)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(product);

        return new WorkItemRoadmap(workspace, product, milestoneFilter);
    }

    private static WorkItemMilestone? GetMilestone(IReadOnlyList<WorkItemMilestone> milestones, DateTimeOffset dateTime)
    {
        // TODO: Use a binary search

        foreach (var m in milestones.Where(m => m.ReleaseDate is not null))
        {
            var r = m.ReleaseDate!.Value;
            if (r >= dateTime)
                return m;
        }

        return null;
    }
}
