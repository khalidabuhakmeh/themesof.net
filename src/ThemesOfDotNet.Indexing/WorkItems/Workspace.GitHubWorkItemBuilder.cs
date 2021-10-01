using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using ThemesOfDotNet.Indexing.Configuration;
using ThemesOfDotNet.Indexing.GitHub;

namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed partial class Workspace
{
    private sealed class GitHubWorkItemBuilder
    {
        private readonly Workspace _workspace;
        private readonly WorkItemBuilder _workItemBuilder;
        private readonly WorkItemUserBuilder _userBuilder;
        private readonly WorkItemMilestoneBuilder _milestoneBuilder;

        public GitHubWorkItemBuilder(Workspace workspace,
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

        public void Build(SubscriptionConfiguration configuration,
                          IReadOnlyList<GitHubIssue> gitHubIssues,
                          IReadOnlyDictionary<GitHubIssueId, GitHubIssueId> gitHubTransferMap,
                          IReadOnlyList<GitHubProject> gitHubProjects)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(gitHubIssues);
            ArgumentNullException.ThrowIfNull(gitHubTransferMap);

            // Resolve links to parents and children

            foreach (var issue in gitHubIssues)
            {
                foreach (var link in GitHubIssueParser.ParseLinks(issue.Repo.GetId(), issue.Body))
                {
                    var parentId = issue.GetId();
                    var childId = link.LinkedId;

                    if (gitHubTransferMap.TryGetValue(childId, out var transferredId))
                        childId = transferredId;

                    if (link.LinkType == GitHubIssueLinkType.Parent)
                        (parentId, childId) = (childId, parentId);

                    _workItemBuilder.AddChild(parentId.ToString(), childId.ToString());
                }
            }

            // Map project cards

            var projectById = gitHubProjects.ToDictionary(p => p.Id);
            var projectCardsByIssueId = gitHubProjects.SelectMany(p => p.Columns, (p, c) => (Project: p, Column: c))
                                                      .SelectMany(t => t.Column.Cards, (t, c) => (t.Project, t.Column, Card: c))
                                                      .Where(t => t.Card.IssueId is not null)
                                                      .ToLookup(t => GitHubIssueId.Parse(t.Card.IssueId!));

            // Create work items

            foreach (var issue in gitHubIssues)
            {
                var issueId = issue.GetId();

                var issueCards = projectCardsByIssueId[issueId].ToArray();

                var id = issueId.ToString();
                var url = issue.HtmlUrl;
                var isPrivate = !issue.Repo.IsPublic;
                var isBottomUp = IsBottomUp(issue);
                var state = GetState(issue, issueCards);
                var kind = GetKind(issue);
                var title = issue.FixedTitle();
                var priority = GetPriority(issue);
                var cost = GetCost(issue);
                var createdAt = issue.CreatedAt;
                var createdBy = _userBuilder.GetUserForGitHubLogin(issue.CreatedBy);
                var milestone = GetMilestone(issue.Repo.GetId(), issue.Milestone, projectById, issueCards);
                var assignees = issue.Assignees.Select(a => _userBuilder.GetUserForGitHubLogin(a))
                                               .ToArray();
                var areas = GetAreas(configuration, issueId, issue.Labels.Select(l => l.Name).ToArray());
                var teams = GetTeams(configuration, areas, issue);
                var changes = GetChanges(issue, projectById, projectCardsByIssueId);

                var workItem = new WorkItem(_workspace,
                                            issue,
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

        private static bool IsBottomUp(GitHubIssue issue)
        {
            foreach (var label in issue.Labels)
            {
                if (string.Equals(label.Name, Constants.LabelContinuousImprovement) ||
                    string.Equals(label.Name, Constants.LabelBottomUpWork))
                    return true;
            }

            return false;
        }

        private static WorkItemState GetState(GitHubIssue issue, IReadOnlyList<(GitHubProject Project, GitHubProjectColumn Column, GitHubCard Card)> issueCards)
        {
            return GetState(issue.IsOpen, issue.Labels.Select(l => l.Name), issueCards.Select(c => c.Column.Name));
        }

        private static WorkItemState GetState(bool isOpen, IEnumerable<string> labels, IEnumerable<string> issueCardColumns)
        {
            // First let's try the labels

            var labelState = GetLabelValue<WorkItemState>(labels, Constants.LabelStatus, TryParseState, (x, y) => (WorkItemState)Math.Max((int)x, (int)y));
            if (labelState is not null)
                return labelState.Value;

            // If we can't find any, let's try project boards

            foreach (var column in issueCardColumns)
            {
                if (TryParseState(column, out var state))
                    return state;
            }

            // Default

            return isOpen
                    ? WorkItemState.Proposed
                    : WorkItemState.Completed;
        }

        private static WorkItemKind GetKind(GitHubIssue issue)
        {
            return GetKind(issue.Labels.Select(l => l.Name));
        }

        private static WorkItemKind GetKind(IEnumerable<string> labels)
        {
            var isTheme = false;
            var isEpic = false;
            var isUserStory = false;

            foreach (var label in labels)
            {
                if (string.Equals(label, Constants.LabelTheme, StringComparison.OrdinalIgnoreCase))
                    isTheme = true;
                else if (string.Equals(label, Constants.LabelEpic, StringComparison.OrdinalIgnoreCase))
                    isEpic = true;
                else if (string.Equals(label, Constants.LabelUserStory, StringComparison.OrdinalIgnoreCase))
                    isUserStory = true;
            }

            if (isTheme)
                return WorkItemKind.Theme;
            else if (isEpic)
                return WorkItemKind.Epic;
            else if (isUserStory)
                return WorkItemKind.UserStory;
            else
                return WorkItemKind.Task;
        }

        public static int? GetPriority(GitHubIssue issue)
        {
            return GetPriority(issue.Labels.Select(l => l.Name));
        }

        public static int? GetPriority(IEnumerable<string> labels)
        {
            return GetLabelValue<int>(labels, Constants.LabelPriority, int.TryParse, Math.Min);
        }

        public static WorkItemCost? GetCost(GitHubIssue issue)
        {
            return GetCost(issue.Labels.Select(l => l.Name));
        }

        public static WorkItemCost? GetCost(IEnumerable<string> labels)
        {
            return GetLabelValue<WorkItemCost>(labels, Constants.LabelCost, TryParseCost, (x, y) => (WorkItemCost)Math.Max((int)x, (int)y));
        }

        public static IReadOnlyList<string> GetTeams(SubscriptionConfiguration configuration, IReadOnlyList<string> areas, GitHubIssue issue)
        {
            var result = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var label in issue.Labels)
            {
                if (TryParseNamedValue(label.Name, Constants.LabelTeam, out var value))
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

        private IReadOnlyList<WorkItemChange> GetChanges(GitHubIssue issue,
                                                         IReadOnlyDictionary<long, GitHubProject> projectById,
                                                         ILookup<GitHubIssueId, (GitHubProject Project, GitHubProjectColumn Column, GitHubCard Card)> projectCardsByIssueId)
        {
            var tracker = new GitHubChangeTracker(_milestoneBuilder, projectById, projectCardsByIssueId, issue);

            var result = new List<WorkItemChange>();

            var nextTitle = tracker.GetTitle();
            var nextKind = tracker.GetKind();
            var nextState = tracker.GetState();
            var nextIsBottomUp = tracker.IsBottomUp();
            var nextPriority = tracker.GetPriority();
            var nextCost = tracker.GetCost();
            var nextMilestone = tracker.GetMilestone();

            for (var i = issue.Events.Count - 1; i >= 0; i--)
            {
                var e = issue.Events[i];
                tracker.ReverseApply(e);

                var actor = _userBuilder.GetUserForGitHubLogin(e.Actor);
                var when = e.CreatedAt;

                var previousTitle = tracker.GetTitle();
                if (nextTitle != previousTitle)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.TitleChanged, nextTitle, previousTitle));
                    nextTitle = previousTitle;
                }

                var previousKind = tracker.GetKind();
                if (nextKind != previousKind)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.KindChanged, nextKind, previousKind));
                    nextKind = previousKind;
                }

                var previousState = tracker.GetState();
                if (nextState != previousState)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.StateChanged, nextState, previousState));
                    nextState = previousState;
                }

                var previousIsBottomUp = tracker.IsBottomUp();
                if (nextIsBottomUp != previousIsBottomUp)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.IsBottomUpChanged, nextIsBottomUp, previousIsBottomUp));
                    nextIsBottomUp = previousIsBottomUp;
                }

                var previousPriority = tracker.GetPriority();
                if (nextPriority != previousPriority)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.PriorityChanged, nextPriority, previousPriority));
                    nextPriority = previousPriority;
                }

                var previousCost = tracker.GetCost();
                if (nextCost != previousCost)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.CostChanged, nextCost, previousCost));
                    nextCost = previousCost;
                }

                var previousMilestone = tracker.GetMilestone();
                if (nextMilestone != previousMilestone)
                {
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.MilestoneChanged, nextMilestone, previousMilestone));
                    nextMilestone = previousMilestone;
                }

                if (e.Assignee is not null && string.Equals(e.Event, "assigned", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Assert(e.Assignee is not null);
                    var assignee = _userBuilder.GetUserForGitHubLogin(e.Assignee);
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.AssigneeAdded, assignee, null));
                }

                if (e.Assignee is not null && string.Equals(e.Event, "unassigned", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Assert(e.Assignee is not null);
                    var assignee = _userBuilder.GetUserForGitHubLogin(e.Assignee);
                    result.Add(new WorkItemChange(actor, when, WorkItemChangeKind.AssigneeRemoved, assignee, null));
                }
            }

            result.Reverse();

            // Compact events
            //
            // Due to the way GitHub's timeline data is structured it's prone to many subsequent events.
            // For example, say I change the milestone from x to y. Typically, the timeline will have two
            // events:
            //
            // - demilestoned x
            // - milestoned y
            //
            // We'd translate these to two changes, clearing milestone and settting milestone. Instead, it's
            // nicer to have a single change that updates the milestone from x to y.

            for (var i = result.Count - 1; i > 0; i--)
            {
                var from = result[i - 1];
                var to = result[i];
                var delta = to.When - from.When;
                if (delta < TimeSpan.Zero)
                    delta = -delta;

                if (from.Kind == to.Kind &&
                    from.Actor == to.Actor &&
                    from.Value is null &&
                    to.PreviousValue is null &&
                    delta <= TimeSpan.FromSeconds(3))
                {
                    result[i - 1] = new WorkItemChange(from.Actor, from.When, from.Kind, to.Value, from.PreviousValue);
                    result.RemoveAt(i);
                }
            }

            return result.ToArray();
        }

        private static bool TryParseState(string? text, out WorkItemState result)
        {
            if (!string.IsNullOrEmpty(text))
            {
                switch (text.ToLowerInvariant())
                {
                    case "proposed":
                        result = WorkItemState.Proposed;
                        return true;
                    case "committed":
                        result = WorkItemState.Committed;
                        return true;
                    case "in progress":
                        result = WorkItemState.InProgress;
                        return true;
                    case "cut":
                        result = WorkItemState.Cut;
                        return true;
                    case "completed":
                        result = WorkItemState.Completed;
                        return true;
                }
            }

            result = default;
            return false;
        }

        private static bool TryParseCost(string? text, out WorkItemCost result)
        {
            if (!string.IsNullOrEmpty(text))
            {
                switch (text.ToLower())
                {
                    case "s":
                        result = WorkItemCost.Small;
                        return true;
                    case "m":
                        result = WorkItemCost.Medium;
                        return true;
                    case "l":
                        result = WorkItemCost.Large;
                        return true;
                    case "xl":
                        result = WorkItemCost.ExtraLarge;
                        return true;
                }
            }

            result = default;
            return false;
        }

        private static T? GetLabelValue<T>(IEnumerable<string> labels, string labelName, TryParseHandler<T> parser, Func<T, T, T> combiner)
            where T : struct
        {
            var result = (T?)null;

            foreach (var label in labels)
            {
                if (TryParseNamedValue(label, labelName, out var textValue) &&
                    parser(textValue, out var value))
                {
                    if (result is null)
                        result = value;
                    else
                        result = combiner(result.Value, value);
                }
            }

            return result;
        }

        private static bool TryParseNamedValue(string text, string name, [NotNullWhen(true)] out string value)
        {
            value = default!;

            var parts = text.Split(':');
            if (parts.Length != 2)
                return false;

            var n = parts[0].Trim();
            var v = parts[1].Trim();

            if (!string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
                return false;

            value = v;
            return true;
        }

        private delegate bool TryParseHandler<T>(string text, out T result);

        private WorkItemMilestone? GetMilestone(GitHubRepoId repoId,
                                          GitHubMilestone? milestone,
                                          IReadOnlyDictionary<long, GitHubProject> projectById,
                                          IReadOnlyList<(GitHubProject Project, GitHubProjectColumn Column, GitHubCard Card)> issueCards)
        {
            var projectIds = issueCards.Select(t => t.Project.Id);
            return GetMilestone(_milestoneBuilder, repoId, milestone?.Title, projectById, projectIds);
        }

        private static WorkItemMilestone? GetMilestone(WorkItemMilestoneBuilder milestoneBuilder,
                                                       GitHubRepoId repoId,
                                                       string? milestone,
                                                       IReadOnlyDictionary<long, GitHubProject> projectById,
                                                       IEnumerable<long> issueProjectIds)
        {
            var projectName = issueProjectIds.Where(i => projectById.ContainsKey(i))
                                             .Select(i => projectById[i].Name)
                                             .FirstOrDefault();

            if (projectName is not null)
                milestone = projectName;

            return milestoneBuilder.MapGitHubMilestone(repoId.Owner, repoId.Name, milestone);
        }

        private static IReadOnlyList<string> GetAreas(SubscriptionConfiguration configuration, GitHubIssueId issueId, IReadOnlyList<string> labels)
        {
            var areaPaths = labels.Select(l =>
                                   {
                                       var result = GitHubAreaPath.TryParse(l, out var areaPath);
                                       return (IsSuccess: result, Path: areaPath);
                                   })
                                  .Where(t => t.IsSuccess)
                                  .Select(t => t.Path)
                                  .ToArray();

            var (org, repo) = issueId.RepoId;

            if (configuration.GitHubOrgByName.TryGetValue(org, out var orgConfiguration))
            {
                if (orgConfiguration.Repos.TryGetValue(repo, out var repoConfiguration))
                {
                    var result = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var area in repoConfiguration.Areas)
                    {
                        var isCatchAll = !area.IncludedPaths.Any() &&
                                         !area.IncludedLabels.Any() &&
                                         !area.Issues.Any();
                        if (isCatchAll && result.Any())
                            continue;

                        if (area.Issues.Any() && !area.Issues.Contains(issueId.Number))
                            continue;

                        if (area.IncludedPaths.Any() && !IsMatch(area.IncludedPaths, areaPaths))
                            continue;

                        if (area.IncludedLabels.Any() && !IsMatch(area.IncludedLabels, labels))
                            continue;

                        result.Add(area.Area);
                    }

                    if (result.Any())
                        return result.ToArray();
                }
            }

            return Array.Empty<string>();
        }

        private static bool IsMatch(IReadOnlyList<string> includedPaths, IEnumerable<GitHubAreaPath> areaPaths)
        {
            foreach (var areaPath in areaPaths)
            {
                if (IsMatch(includedPaths, areaPath))
                    return true;
            }

            return false;
        }

        private static bool IsMatch(IReadOnlyList<string> includedPaths, GitHubAreaPath areaPath)
        {
            foreach (var includedPath in includedPaths)
            {
                if (IsMatch(includedPath, areaPath))
                    return true;
            }

            return false;
        }

        private static bool IsMatch(string areaPathExpresion, GitHubAreaPath areaPath)
        {
            var expression = new GitHubAreaPathExpression(areaPathExpresion);
            return expression.IsMatch(areaPath);
        }

        private static bool IsMatch(IReadOnlyList<string> includedLables, IReadOnlyList<string> issueLabels)
        {
            foreach (var issueLabel in issueLabels)
            {
                if (includedLables.Contains(issueLabel, StringComparer.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private sealed class GitHubChangeTracker
        {
            private readonly WorkItemMilestoneBuilder _milestoneBuilder;
            private readonly GitHubRepoId _repoId;
            private readonly IReadOnlyDictionary<long, GitHubProject> _projectById;
            private readonly ILookup<GitHubIssueId, (GitHubProject Project, GitHubProjectColumn Column, GitHubCard Card)> _projectCardsByIssueId;

            private bool _isOpen;
            private string _title;
            private readonly HashSet<string> _assignees = new(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> _labels = new(StringComparer.OrdinalIgnoreCase);
            private string? _milestone;
            private readonly Dictionary<long, string> _columnNameByProjectId = new();

            public GitHubChangeTracker(WorkItemMilestoneBuilder milestoneBuilder,
                                       IReadOnlyDictionary<long, GitHubProject> projectById,
                                       ILookup<GitHubIssueId, (GitHubProject Project, GitHubProjectColumn Column, GitHubCard Card)> projectCardsByIssueId,
                                       GitHubIssue issue)
            {
                _milestoneBuilder = milestoneBuilder;
                _repoId = issue.Repo.GetId();
                _projectById = projectById;
                _projectCardsByIssueId = projectCardsByIssueId;

                _isOpen = issue.IsOpen;
                _title = issue.Title;
                _assignees.UnionWith(issue.Assignees);
                _labels.UnionWith(issue.Labels.Select(l => l.Name));
                _milestone = issue.Milestone?.Title;

                foreach (var (project, column, card) in _projectCardsByIssueId[issue.GetId()])
                    _columnNameByProjectId[project.Id] = column.Name;
            }

            public void ReverseApply(GitHubIssueEvent issueEvent)
            {
                switch (issueEvent.Event.ToLowerInvariant())
                {
                    case "labeled":
                        Debug.Assert(issueEvent.Label is not null);
                        _labels.Remove(issueEvent.Label);
                        break;
                    case "unlabeled":
                        Debug.Assert(issueEvent.Label is not null);
                        _labels.Add(issueEvent.Label);
                        break;
                    case "milestoned":
                        _milestone = null;
                        break;
                    case "demilestoned":
                        Debug.Assert(issueEvent.Milestone is not null);
                        _milestone = issueEvent.Milestone;
                        break;
                    case "assigned":
                        if (issueEvent.Assignee is not null)
                            _assignees.Remove(issueEvent.Assignee);
                        break;
                    case "unassigned":
                        if (issueEvent.Assignee is not null)
                            _assignees.Add(issueEvent.Assignee);
                        break;
                    case "closed":
                        _isOpen = true;
                        break;
                    case "renamed":
                        if (issueEvent.Rename?.From is not null)
                            _title = issueEvent.Rename.From;
                        break;
                    case "reopened":
                        _isOpen = false;
                        break;
                    case "added_to_project":
                        Debug.Assert(issueEvent.Card is not null);
                        _columnNameByProjectId.Remove(issueEvent.Card.ProjectId);
                        break;
                    case "moved_columns_in_project":
                        Debug.Assert(issueEvent.Card is not null);
                        Debug.Assert(issueEvent.Card.PreviousColumnName is not null);
                        _columnNameByProjectId[issueEvent.Card.ProjectId] = issueEvent.Card.PreviousColumnName;
                        break;
                    case "removed_from_project":
                        Debug.Assert(issueEvent.Card is not null);
                        Debug.Assert(issueEvent.Card.ColumnName is not null);
                        _columnNameByProjectId.Add(issueEvent.Card.ProjectId, issueEvent.Card.ColumnName);
                        break;
                    default:
                        throw new Exception($"Unexpected event '{issueEvent}'");
                }
            }

            public string GetTitle()
            {
                return _title;
            }

            public WorkItemKind GetKind()
            {
                return GitHubWorkItemBuilder.GetKind(_labels);
            }

            public WorkItemState GetState()
            {
                var columnNames = _columnNameByProjectId.Select(kv => kv.Value);
                return GitHubWorkItemBuilder.GetState(_isOpen, _labels, columnNames);
            }

            public bool IsBottomUp()
            {
                foreach (var label in _labels)
                {
                    if (string.Equals(label, Constants.LabelContinuousImprovement) ||
                        string.Equals(label, Constants.LabelBottomUpWork))
                        return true;
                }

                return false;
            }

            public int? GetPriority()
            {
                return GitHubWorkItemBuilder.GetPriority(_labels);
            }

            public WorkItemCost? GetCost()
            {
                return GitHubWorkItemBuilder.GetCost(_labels);
            }

            public WorkItemMilestone? GetMilestone()
            {
                return GitHubWorkItemBuilder.GetMilestone(_milestoneBuilder, _repoId, _milestone, _projectById, _columnNameByProjectId.Keys);
            }
        }
    }
}
