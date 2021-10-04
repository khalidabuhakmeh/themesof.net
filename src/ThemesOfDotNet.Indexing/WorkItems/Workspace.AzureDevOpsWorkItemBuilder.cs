﻿using ThemesOfDotNet.Indexing.AzureDevOps;
using ThemesOfDotNet.Indexing.Configuration;

namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed partial class Workspace
{
    private sealed class AzureDevOpsWorkItemBuilder
    {
        private readonly Workspace _workspace;
        private readonly WorkItemBuilder _workItemBuilder;
        private readonly WorkItemUserBuilder _userBuilder;
        private readonly WorkItemMilestoneBuilder _milestoneBuilder;

        public AzureDevOpsWorkItemBuilder(Workspace workspace,
                                          WorkItemBuilder workItemBuilder,
                                          WorkItemUserBuilder userBuilder,
                                          WorkItemMilestoneBuilder milestoneBuilder)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(workItemBuilder);
            ArgumentNullException.ThrowIfNull(userBuilder);
            ArgumentNullException.ThrowIfNull(milestoneBuilder);

            _workspace = workspace;
            _workItemBuilder = workItemBuilder;
            _userBuilder = userBuilder;
            _milestoneBuilder = milestoneBuilder;
        }

        public void Build(SubscriptionConfiguration configuration, IReadOnlyList<AzureDevOpsWorkItem> azureDevOpsWorkItems)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(azureDevOpsWorkItems);

            var workItemsByQueryId = azureDevOpsWorkItems.ToLookup(wi => wi.QueryId);

            foreach (var query in configuration.AzureDevOpsQueries)
            {
                var workItems = workItemsByQueryId[query.QueryId];
                var childIdSet = workItems.SelectMany(wi => wi.ChildIds).ToHashSet();
                var topLevelItems = workItems.Where(wi => !childIdSet.Contains(wi.Id));

                var firstWorkItem = workItems.MinBy(wi => wi.CreatedAt);

                var id = GetQueryId(query);

                foreach (var topLevelItem in topLevelItems)
                    _workItemBuilder.AddChild(id, GetWorkItemId(topLevelItem.Id));

                var url = query.HtmlUrl;
                var isPrivate = true;
                var isBottomUp = false;
                var state = WorkItemState.Proposed;
                var kind = WorkItemKind.Theme;
                var title = query.ThemeTitle;
                var milestone = (WorkItemMilestone?)null;
                var priority = (int?)null;
                var cost = (WorkItemCost?)null;
                var createdAt = firstWorkItem?.CreatedAt ?? DateTime.Now;
                var createdBy = firstWorkItem?.CreatedBy is not null
                    ? _userBuilder.GetUserForMicrosoftAlias(firstWorkItem.CreatedBy)
                    : _userBuilder.GetUserForGitHubLogin("dotnet-bot");
                var assignees = query.ThemeAssignees.Select(a => _userBuilder.GetUserForMicrosoftAlias(a)).ToArray();
                var areas = query.Area is null
                    ? Array.Empty<string>()
                    : new[] { query.Area };
                var teams = Array.Empty<string>();
                var changes = Array.Empty<WorkItemChange>();

                var workItem = new WorkItem(_workspace,
                                            query,
                                            id,
                                            url,
                                            isPrivate,
                                            isBottomUp,
                                            state,
                                            kind,
                                            title,
                                            priority,
                                            cost,
                                            createdAt,
                                            createdBy,
                                            milestone,
                                            assignees,
                                            areas,
                                            teams,
                                            changes);
                _workItemBuilder.AddWorkItem(workItem);
            }

            foreach (var azureDevOpsWorkItem in azureDevOpsWorkItems)
            {
                var parentId = azureDevOpsWorkItem.Id;

                foreach (var childId in azureDevOpsWorkItem.ChildIds)
                    _workItemBuilder.AddChild(GetWorkItemId(parentId), GetWorkItemId(childId));
            }

            foreach (var azureDevOpsWorkItem in azureDevOpsWorkItems)
            {
                var query = configuration.AzureDevOpsQueryById[azureDevOpsWorkItem.QueryId];

                var id = GetId(azureDevOpsWorkItem);
                var url = azureDevOpsWorkItem.Url;
                var isPrivate = true;
                var isBottomUp = IsBottomUp(azureDevOpsWorkItem.Tags);
                var state = ConvertState(azureDevOpsWorkItem.State);
                var kind = ConvertKind(azureDevOpsWorkItem.Type);
                var title = azureDevOpsWorkItem.Title;
                var milestone = ConvertMilestone(azureDevOpsWorkItem.Milestone, azureDevOpsWorkItem.Target);
                var priority = ConvertPriority(azureDevOpsWorkItem.Priority);
                var cost = ConvertCost(azureDevOpsWorkItem.Cost);
                var createdAt = azureDevOpsWorkItem.CreatedAt;
                var createdBy = _userBuilder.GetUserForMicrosoftAlias(azureDevOpsWorkItem.CreatedBy);
                var assignees = string.IsNullOrEmpty(azureDevOpsWorkItem.AssignedTo)
                    ? Array.Empty<WorkItemUser>()
                    : new[] { _userBuilder.GetUserForMicrosoftAlias(azureDevOpsWorkItem.AssignedTo) };
                var areas = query.Area is null
                    ? Array.Empty<string>()
                    : new[] { query.Area };
                var teams = ConvertTeams(configuration, areas, azureDevOpsWorkItem.Tags);
                var changes = ConvertChanges(azureDevOpsWorkItem);

                var workItem = new WorkItem(_workspace,
                                            azureDevOpsWorkItem,
                                            id,
                                            url,
                                            isPrivate,
                                            isBottomUp,
                                            state,
                                            kind,
                                            title,
                                            priority,
                                            cost,
                                            createdAt,
                                            createdBy,
                                            milestone,
                                            assignees,
                                            areas,
                                            teams,
                                            changes);
                _workItemBuilder.AddWorkItem(workItem);
            }
        }

        private static string GetQueryId(AzureDevOpsQueryConfiguration query)
        {
            return "azdo-" + query.QueryId;
        }

        private static string GetId(AzureDevOpsWorkItem workItem)
        {
            return GetWorkItemId(workItem.Id);
        }

        private static string GetWorkItemId(int id)
        {
            return $"azdo#{id}";
        }

        private static bool IsBottomUp(IEnumerable<string> tags)
        {
            return tags.Any(t => string.Equals(t, Constants.LabelBottomUpWork, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(t, Constants.LabelContinuousImprovement, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsOpen(AzureDevOpsWorkItem item)
        {
            var state = ConvertState(item.State);
            return state switch
            {
                WorkItemState.Proposed or
                WorkItemState.Committed or
                WorkItemState.InProgress => true,
                WorkItemState.Completed or
                WorkItemState.Cut => false,
                _ => throw new Exception($"Unexpected state: {state}")
            };
        }

        private static WorkItemState ConvertState(string state)
        {
            if (string.Equals(state, "Proposed", StringComparison.OrdinalIgnoreCase))
                return WorkItemState.Proposed;

            if (string.Equals(state, "Committed", StringComparison.OrdinalIgnoreCase))
                return WorkItemState.Committed;

            if (string.Equals(state, "In Progress", StringComparison.OrdinalIgnoreCase))
                return WorkItemState.InProgress;

            if (string.Equals(state, "Cut", StringComparison.OrdinalIgnoreCase))
                return WorkItemState.Cut;

            if (string.Equals(state, "Completed", StringComparison.OrdinalIgnoreCase))
                return WorkItemState.Completed;

            return WorkItemState.Proposed;
        }

        private static WorkItemKind ConvertKind(string type)
        {
            if (string.Equals(type, "Scenario", StringComparison.OrdinalIgnoreCase))
                return WorkItemKind.Epic;
            else if (string.Equals(type, "Experience", StringComparison.OrdinalIgnoreCase))
                return WorkItemKind.UserStory;
            else
                return WorkItemKind.Task;
        }

        private static int? ConvertPriority(long? priority)
        {
            if (priority >= 0 && priority <= 3)
                return (int)priority;

            return null;
        }

        private static WorkItemCost? ConvertCost(string? cost)
        {
            if (string.Equals(cost, "S", StringComparison.OrdinalIgnoreCase))
                return WorkItemCost.Small;
            else if (string.Equals(cost, "M", StringComparison.OrdinalIgnoreCase))
                return WorkItemCost.Medium;
            else if (string.Equals(cost, "L", StringComparison.OrdinalIgnoreCase))
                return WorkItemCost.Large;
            else if (string.Equals(cost, "XL", StringComparison.OrdinalIgnoreCase))
                return WorkItemCost.ExtraLarge;
            else
                return null;
        }

        private static IReadOnlyList<string> ConvertTeams(SubscriptionConfiguration configuration, IReadOnlyList<string> areas, IReadOnlyList<string> tags)
        {
            var result = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var tag in tags)
            {
                var parts = tag.Split(':');
                if (parts.Length != 2)
                    continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                if (!string.Equals(key, "Team", StringComparison.OrdinalIgnoreCase))
                    continue;

                result.Add(value);
            }

            foreach (var area in areas)
            {
                foreach (var (team, areaConfigurations) in configuration.Teams)
                {
                    foreach (var areaConfiguration in areaConfigurations)
                    {
                        if (areaConfiguration.IncludedAreas.Contains(area, StringComparer.OrdinalIgnoreCase))
                            result.Add(team);
                    }
                }
            }

            return result.ToArray();
        }

        private WorkItemMilestone? ConvertMilestone(string? milestone, string? target)
        {
            return ConvertMilestone(_milestoneBuilder, milestone, target);
        }

        private static WorkItemMilestone? ConvertMilestone(WorkItemMilestoneBuilder milestoneBuilder, string? milestone, string? target)
        {
            var milestoneOrTarget = GetMilestone(milestone, target);
            return milestoneBuilder.MapAzureDevOpsMilestone(milestoneOrTarget);
        }

        private static string? GetMilestone(string? milestone, string? target)
        {
            // Milestone    : 16.8
            // Target       : Preview 2
            //
            // --->
            //
            // 16.8 P2

            if (target != null)
            {
                target = target.Replace(".NET", "", StringComparison.OrdinalIgnoreCase)
                               .Replace("Preview ", "P", StringComparison.OrdinalIgnoreCase)
                               .Replace("Preview", "P", StringComparison.OrdinalIgnoreCase);
            }

            var result = string.Join(" ", milestone, target).Trim();
            if (result.Length == 0)
                return null;

            return result;
        }

        private IReadOnlyList<WorkItemChange> ConvertChanges(AzureDevOpsWorkItem workItem)
        {
            var tracker = new AzureDevOpsChangeTracker(_userBuilder, _milestoneBuilder, workItem);

            var result = new List<WorkItemChange>();

            var nextIsBottomUp = tracker.IsBottomUp();
            var nextTitle = tracker.GetTitle();
            var nextAssignedTo = tracker.GetAssignedTo();
            var nextState = tracker.GetState();
            var nextKind = tracker.GetKind();
            var nextPriority = tracker.GetPriority();
            var nextCost = tracker.GetCost();
            var nextMilestone = tracker.GetMilestone();

            for (var i = workItem.Changes.Count - 1; i >= 0; i--)
            {
                var change = workItem.Changes[i];
                tracker.ReverseApply(change);

                var actor = _userBuilder.GetUserForMicrosoftAlias(change.Actor);
                var when = change.When;

                var previousIsBottomUp = tracker.IsBottomUp();
                if (previousIsBottomUp != nextIsBottomUp)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.KindChanged, nextIsBottomUp, previousIsBottomUp));
                    nextIsBottomUp = previousIsBottomUp;
                }

                var previousTitle = tracker.GetTitle();
                if (previousTitle != nextTitle)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.TitleChanged, nextTitle, previousTitle));
                    nextTitle = previousTitle;
                }

                var previousAssignedTo = tracker.GetAssignedTo();
                if (previousAssignedTo != nextAssignedTo)
                {
                    if (previousAssignedTo is not null)
                        result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.AssigneeRemoved, previousAssignedTo, null));

                    if (nextAssignedTo is not null)
                        result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.AssigneeAdded, nextAssignedTo, null));

                    nextAssignedTo = previousAssignedTo;
                }

                var previousState = tracker.GetState();
                if (previousState != nextState)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.StateChanged, nextState, previousState));
                    nextState = previousState;
                }

                var previousKind = tracker.GetKind();
                if (previousKind != nextKind)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.KindChanged, nextKind, previousKind));
                    nextKind = previousKind;
                }

                var previousPriority = tracker.GetPriority();
                if (previousPriority != nextPriority)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.PriorityChanged, nextPriority, previousPriority));
                    nextPriority = previousPriority;
                }

                var previousCost = tracker.GetCost();
                if (previousCost != nextCost)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.CostChanged, nextCost, previousCost));
                    nextCost = previousCost;
                }

                var previousMilestone = tracker.GetMilestone();
                if (previousMilestone != nextMilestone)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.MilestoneChanged, nextMilestone, previousMilestone));
                    nextMilestone = previousMilestone;
                }
            }

            result.Reverse();

            return result.ToArray();
        }

        private sealed class AzureDevOpsChangeTracker
        {
            private readonly WorkItemUserBuilder _userBuilder;
            private readonly WorkItemMilestoneBuilder _milestoneBuilder;
            private string _type;
            private string _title;
            private string _state;
            private long? _priority;
            private string? _cost;
            private string? _milestone;
            private string? _target;
            private string? _assignedTo;
            private IReadOnlyList<string> _tags;

            public AzureDevOpsChangeTracker(WorkItemUserBuilder userBuilder,
                                            WorkItemMilestoneBuilder milestoneBuilder,
                                            AzureDevOpsWorkItem workItem)
            {
                _type = workItem.Type;
                _title = workItem.Title;
                _state = workItem.State;
                _priority = workItem.Priority;
                _cost = workItem.Cost;
                _milestone = workItem.Milestone;
                _target = workItem.Target;
                _assignedTo = workItem.AssignedTo;
                _tags = workItem.Tags;
                _userBuilder = userBuilder;
                _milestoneBuilder = milestoneBuilder;
            }

            public void ReverseApply(AzureDevOpsFieldChange change)
            {
                switch (change.Field)
                {
                    case AzureDevOpsField.Type:
                        _type = (string)change.From!;
                        break;
                    case AzureDevOpsField.Title:
                        _title = (string)change.From!;
                        break;
                    case AzureDevOpsField.State:
                        _state = (string)change.From!;
                        break;
                    case AzureDevOpsField.Priority:
                        _priority = change.From as long?;
                        break;
                    case AzureDevOpsField.Cost:
                        _cost = (string?)change.From;
                        break;
                    case AzureDevOpsField.Release:
                        // Don't care
                        break;
                    case AzureDevOpsField.Target:
                        _target = (string?)change.From;
                        break;
                    case AzureDevOpsField.Milestone:
                        _milestone = (string?)change.From;
                        break;
                    case AzureDevOpsField.AssignedTo:
                        _assignedTo = (string?)change.From;
                        break;
                    case AzureDevOpsField.Tags:
                        _tags = (string[])change.From!;
                        break;
                    default:
                        throw new Exception($"Unexpected field {change.Field}");
                }
            }

            public bool IsBottomUp()
            {
                return AzureDevOpsWorkItemBuilder.IsBottomUp(_tags);
            }

            public string GetTitle() => _title;

            public WorkItemUser? GetAssignedTo()
            {
                if (_assignedTo is null)
                    return null;

                return _userBuilder.GetUserForMicrosoftAlias(_assignedTo);
            }

            public WorkItemState GetState()
            {
                return ConvertState(_state);
            }

            public WorkItemKind GetKind()
            {
                return ConvertKind(_type);
            }

            public int? GetPriority()
            {
                return ConvertPriority(_priority);
            }

            public WorkItemCost? GetCost()
            {
                return ConvertCost(_cost);
            }

            public WorkItemMilestone? GetMilestone()
            {
                return ConvertMilestone(_milestoneBuilder, _milestone, _target);
            }
        }
    }
}