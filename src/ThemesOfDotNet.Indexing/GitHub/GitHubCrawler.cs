using System.Diagnostics;

using Octokit;

using Spectre.Console;

using Terrajobst.GitHubEvents;

using ThemesOfDotNet.Indexing.Configuration;
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Indexing.GitHub;

public sealed class GitHubCrawler
{
    private static readonly HashSet<string> _relevantEventNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "labeled",
        "unlabeled",
        "milestoned",
        "demilestoned",
        "assigned",
        "unassigned",
        "closed",
        "renamed",
        "reopened",
        "added_to_project",
        "moved_columns_in_project",
        "removed_from_project"
    };

    private readonly GitHubAppClient _client;
    private readonly GitHubCache _cache;
    private bool _initializedFromCache;

    private readonly Dictionary<GitHubRepoId, GitHubRepo> _repoById = new();
    private readonly Dictionary<long, GitHubProject> _projectById = new();
    private readonly Dictionary<GitHubIssueId, GitHubIssueId> _transferMap = new();

    private readonly HashSet<GitHubIssueId> _pendingIssues = new();

    private readonly HashSet<GitHubRepo> _reposThatNeedStoring = new();
    private readonly HashSet<GitHubRepo> _reposThatNeedDeleting = new();

    private bool _transferMapNeedsStoring = false;
    private bool _projectsNeedsStoring = false;

    public GitHubCrawler(string appId, string privateKey, GitHubCache cache)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentNullException.ThrowIfNull(cache);

        _client = new GitHubAppClient(new ProductHeaderValue("themesofdotnet"), appId, privateKey);
        _cache = cache;
    }

    public bool HasPendingWork => _pendingIssues.Count > 0;

    public void Enqueue(GitHubIssueId issueId)
    {
        if (FindCrawledIssue(issueId) is null)
            _pendingIssues.Add(issueId);
    }

    private static void EnqueueReferencedItems(IEnumerable<GitHubIssue> issues, IWorkspaceCrawlerQueue queue)
    {
        foreach (var issue in issues)
        {
            var repoId = issue.Repo.GetId();
            var issueBody = issue.Body;
            var linkage = GitHubIssueParser.ParseLinkage(repoId, issueBody);

            foreach (var link in linkage.IssueLinks)
                queue.Enqueue(link.LinkedId);

            foreach (var link in linkage.WorkItemLinks)
                queue.Enqueue(link.LinkedId);

            foreach (var queryId in linkage.QueryIds)
                queue.Enqueue(queryId);
        }
    }

    public async Task CrawlRootsAsync(IReadOnlyList<GitHubOrgConfiguration> orgs,
                                      IWorkspaceCrawlerQueue queue)
    {
        ArgumentNullException.ThrowIfNull(orgs);
        ArgumentNullException.ThrowIfNull(queue);

        foreach (var org in orgs.OrderBy(o => o.Name))
        {
            if (org.IndexingMode == GitHubIndexingMode.NoRepos)
                continue;

            Console.WriteLine($"Crawling repos for {org.Name}...");

            foreach (var repo in (await GetReposAsync(org)).OrderBy(r => r.Name))
            {
                Debug.Assert(repo is not null);

                if (!repo.HasIssues)
                {
                    Console.WriteLine($"Skipped {repo.FullName} because issues are disabled.");
                    continue;
                }

                var crawledIssues = false;

                var convertedRepo = await ConvertRepoAsync(org.Name, repo);

                foreach (var label in Constants.LabelsForThemesEpicsAndUserStories)
                {
                    // NOTE: Check that label exists in repo. There is a GitHub issue where if
                    //       a label doesn't exist, the label query below will return ALL issues.

                    var repoHasLabel = convertedRepo.Labels.Any(l => string.Equals(l.Name, label, StringComparison.OrdinalIgnoreCase));
                    if (!repoHasLabel)
                        continue;

                    var request = new RepositoryIssueRequest
                    {
                        Filter = IssueFilter.All,
                        State = ItemStateFilter.All,
                        Labels = { label }
                    };

                    var issuesAndPullRequests = await GetIssuesAsync(org.Name, repo.Name, request);
                    crawledIssues = true;

                    var convertedIssues = issuesAndPullRequests.Select(i => ConvertIssue(convertedRepo, i));
                    foreach (var convertedIssue in convertedIssues)
                    {
                        await CrawlTimelineAsync(convertedIssue);

                        if (!convertedRepo.Issues.Contains(convertedIssue.Number))
                            convertedRepo.Issues.Add(convertedIssue);
                    }
                }

                if (!crawledIssues)
                {
                    Console.WriteLine($"Skipped {repo.FullName} because it doesn't define any of the standard labels");
                }
                else
                {
                    _repoById.Add(convertedRepo.GetId(), convertedRepo);
                    _reposThatNeedStoring.Add(convertedRepo);

                    Console.WriteLine($"Crawled {convertedRepo.Issues.Count:N0} issues from {repo.FullName}");
                }
            }

            Console.WriteLine($"Crawling projects for {org.Name}...");

            foreach (var project in (await GetProjectsAsync(org)).OrderBy(p => p.Name))
            {
                var convertedProject = new GitHubProject();
                FillProject(convertedProject, project);
                _projectById.Add(convertedProject.Id, convertedProject);
                _projectsNeedsStoring = true;

                var columns = await GetProjectColumnsAsync(project);

                foreach (var column in columns)
                {
                    var convertedColumn = new GitHubProjectColumn();
                    FillProjectColumn(convertedColumn, column);

                    convertedProject.Columns.Add(convertedColumn);

                    var cards = await GetProjectCardsAsync(column);

                    foreach (var card in cards)
                    {
                        var convertedCard = new GitHubCard();

                        FillProjectCard(convertedCard, card);

                        convertedColumn.Cards.Add(convertedCard);
                    }
                }

                Console.WriteLine($"Crawled {convertedProject.Columns.Sum(c => c.Cards.Count):N0} cards for project {convertedProject.Name}");
            }
        }

        var issues = _repoById.Values.SelectMany(r => r.Issues);
        EnqueueReferencedItems(issues, queue);
    }

    public async Task CrawlPendingAsync(IWorkspaceCrawlerQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        var pendingIssues = _pendingIssues.ToArray();
        Array.Sort(pendingIssues);
        _pendingIssues.Clear();

        var issues = new List<GitHubIssue>();

        foreach (var issueId in pendingIssues)
        {
            if (FindCrawledIssue(issueId) is not null)
                continue;

            try
            {
                var convertedIssue = await CrawlIssueAsync(issueId);
                issues.Add(convertedIssue);
            }
            catch (NotFoundException)
            {
                GitHubActions.Warning($"Can't find {issueId}");
            }
            catch (Exception ex)
            {
                GitHubActions.Warning($"Can't crawl {issueId}");
                GitHubActions.Warning(ex);
            }
        }

        EnqueueReferencedItems(issues, queue);
    }

    private async Task CrawlTimelineAsync(GitHubIssue issue)
    {
        var issueId = issue.GetId();

        try
        {
            var (owner, repo, number) = issueId;
            var timelineEvents = await GetIssueTimelineAsync(owner, repo, number);
            FillTimeline(issue, timelineEvents);
        }
        catch (Exception ex)
        {
            GitHubActions.Warning("Can't crawl timeline:");
            GitHubActions.Warning(ex);
        }
    }

    public async Task SaveAsync()
    {
        if (!_initializedFromCache)
        {
            await _cache.ClearAsync();

            Console.WriteLine($"Caching {_transferMap.Count:N0} issue transers...");
            await _cache.StoreTransferMapAsync(_transferMap);

            var issueCount = _repoById.Values.Sum(r => r.Issues.Count);
            var repoCount = _repoById.Count;

            Console.WriteLine($"Caching {issueCount:N0} issues in {repoCount:N0} repos...");
            foreach (var repo in _repoById.Values)
                await _cache.StoreRepoAsync(repo);

            Console.WriteLine($"Caching {_projectById.Count:N0} projects...");
            await _cache.StoreProjectsAsync(_projectById.Values.ToArray());
        }
        else
        {
            if (_transferMapNeedsStoring)
            {
                Console.WriteLine($"Updating cached transfer map...");
                await _cache.StoreTransferMapAsync(_transferMap);
            }

            foreach (var repo in _reposThatNeedStoring)
            {
                Console.WriteLine($"Updating cached repo {repo.FullName}...");
                await _cache.StoreRepoAsync(repo);
            }

            foreach (var repo in _reposThatNeedDeleting)
            {
                Console.WriteLine($"Deleting cached repo {repo.FullName}...");
                await _cache.DeleteRepoAsync(repo.Owner, repo.Name);
            }

            if (_projectsNeedsStoring)
            {
                Console.WriteLine($"Updating projects...");
                await _cache.StoreProjectsAsync(_projectById.Values.ToArray());
            }
        }

        _transferMapNeedsStoring = false;
        _reposThatNeedStoring.Clear();
        _reposThatNeedDeleting.Clear();
        _projectsNeedsStoring = false;
    }

    // Incremental updates

    public void LoadFromCache(IReadOnlyList<GitHubIssue> issues,
                              IReadOnlyList<GitHubProject> projects,
                              IReadOnlyDictionary<GitHubIssueId, GitHubIssueId> transferMap)
    {
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(transferMap);

        _repoById.Clear();
        _projectById.Clear();
        _transferMap.Clear();
        _pendingIssues.Clear();
        _reposThatNeedStoring.Clear();
        _reposThatNeedDeleting.Clear();
        _transferMapNeedsStoring = false;
        _projectsNeedsStoring = false;

        var repos = issues.Select(i => i.Repo).Distinct();

        foreach (var repo in repos)
            _repoById.Add(repo.GetId(), repo);

        foreach (var project in projects)
            _projectById.Add(project.Id, project);

        foreach (var (from, to) in transferMap)
            _transferMap.Add(from, to);

        _initializedFromCache = true;
    }

    public Task<bool> UpdateAsync(GitHubEventMessage message, IWorkspaceCrawlerQueue queue)
    {
        if (message.Kind.IsEvent(GitHubEventMessageKind.EventRepository))
        {
            var owner = message.Body.Organization.Login;
            var repoName = message.Body.Repository.Name;

            switch (message.Kind)
            {
                case GitHubEventMessageKind.RepositoryCreated:
                    // Don't care
                    break;
                case GitHubEventMessageKind.RepositoryDeleted:
                    return DeleteRepoAsync(owner, repoName);
                case GitHubEventMessageKind.RepositoryArchived:
                case GitHubEventMessageKind.RepositoryUnarchived:
                case GitHubEventMessageKind.RepositoryPublicized:
                case GitHubEventMessageKind.RepositoryPrivatized:
                    return UpdateRepoAsync(owner, repoName);
                case GitHubEventMessageKind.RepositoryRenamed:
                    var fromRepoName = message.Body.Changes.AdditionalData["repository"]!["name"]!.Value<string>("from")!;
                    return UpdateRepoAsync(owner, fromRepoName);
            }
        }

        if (message.Kind.IsEvent(GitHubEventMessageKind.EventLabel))
        {
            var owner = message.Body.Organization.Login;
            var repoName = message.Body.Repository.Name;
            var labelId = message.Body.Label.Id;
            var labelName = message.Body.Label.Name;

            switch (message.Kind)
            {
                case GitHubEventMessageKind.LabelCreated:
                case GitHubEventMessageKind.LabelEdited:
                    return UpdateLabelAsync(owner, repoName, labelId, labelName);
                case GitHubEventMessageKind.LabelDeleted:
                    return DeleteLabelAsync(owner, repoName, labelId);
            }
        }

        if (message.Kind.IsEvent(GitHubEventMessageKind.EventMilestone))
        {
            var owner = message.Body.Organization.Login;
            var repoName = message.Body.Repository.Name;
            var milestoneId = message.Body.Milestone.Id;
            var milestoneNumber = message.Body.Milestone.Number;

            switch (message.Kind)
            {
                case GitHubEventMessageKind.MilestoneCreated:
                case GitHubEventMessageKind.MilestoneEdited:
                case GitHubEventMessageKind.MilestoneClosed:
                case GitHubEventMessageKind.MilestoneOpened:
                    return UpdateMilestoneAsync(owner, repoName, milestoneId, milestoneNumber);
                case GitHubEventMessageKind.MilestoneDeleted:
                    return DeleteMilestoneAsync(owner, repoName, milestoneId);
            }
        }

        if (message.Kind.IsEvent(GitHubEventMessageKind.EventIssue))
        {
            var owner = message.Body.Organization.Login;
            var repoName = message.Body.Repository.Name;
            var issueNumber = message.Body.Issue.Number;
            var labels = message.Body.Issue.Labels.Select(l => l.Name);

            switch (message.Kind)
            {
                case GitHubEventMessageKind.IssueOpened:
                case GitHubEventMessageKind.IssueReopened:
                case GitHubEventMessageKind.IssueClosed:
                case GitHubEventMessageKind.IssueEdited:
                case GitHubEventMessageKind.IssueAssigned:
                case GitHubEventMessageKind.IssueUnassigned:
                case GitHubEventMessageKind.IssueLabeled:
                case GitHubEventMessageKind.IssueUnlabeled:
                case GitHubEventMessageKind.IssueMilestoned:
                case GitHubEventMessageKind.IssueDemilestoned:
                case GitHubEventMessageKind.IssueLocked:
                case GitHubEventMessageKind.IssueUnlocked:
                    return UpdateIssueAsync(owner, repoName, issueNumber, labels, queue);
                case GitHubEventMessageKind.IssueTransferred:
                    var newOwner = message.Body.Changes.NewRepository.Owner.Login;
                    var newRepoName = message.Body.Changes.NewRepository.Name;
                    var newIssueNumber = message.Body.Changes.NewIssue.Number;
                    return TransferIssueAsync(owner, repoName, issueNumber,
                                              newOwner, newRepoName, newIssueNumber);
                case GitHubEventMessageKind.IssueDeleted:
                    return DeleteIssueAsync(owner, repoName, issueNumber);
            }
        }

        if (message.Kind.IsEvent(GitHubEventMessageKind.EventPullRequest))
        {
            var owner = message.Body.Organization.Login;
            var repoName = message.Body.Repository.Name;
            var issueNumber = message.Body.PullRequest.Number;
            var labels = message.Body.PullRequest.Labels.Select(l => l.Name);

            switch (message.Kind)
            {
                case GitHubEventMessageKind.PullRequestOpened:
                case GitHubEventMessageKind.PullRequestReopened:
                case GitHubEventMessageKind.PullRequestClosed:
                case GitHubEventMessageKind.PullRequestEdited:
                case GitHubEventMessageKind.PullRequestAssigned:
                case GitHubEventMessageKind.PullRequestUnassigned:
                case GitHubEventMessageKind.PullRequestLabeled:
                case GitHubEventMessageKind.PullRequestUnlabeled:
                case GitHubEventMessageKind.PullRequestLocked:
                case GitHubEventMessageKind.PullRequestUnlocked:
                case GitHubEventMessageKind.PullRequestReadyForReview:
                case GitHubEventMessageKind.PullRequestConvertedToDraft:
                    return UpdateIssueAsync(owner, repoName, issueNumber, labels, queue);
            }
        }

        if (message.Kind.IsEvent(GitHubEventMessageKind.EventProject))
        {
            var projectId = message.Body.Project.Id;

            switch (message.Kind)
            {
                case GitHubEventMessageKind.ProjectCreated:
                case GitHubEventMessageKind.ProjectEdited:
                case GitHubEventMessageKind.ProjectReopened:
                    return UpdateProjectAsync(projectId);
                case GitHubEventMessageKind.ProjectDeleted:
                    return DeleteProjectAsync(projectId);
            }
        }

        if (message.Kind.IsEvent(GitHubEventMessageKind.EventProjectColumn))
        {
            var projectId = GetProjectIdFromUrl(message.Body.ProjectColumn.ProjectUrl);
            var columnId = message.Body.ProjectColumn.Id;

            if (projectId is not null)
            {
                switch (message.Kind)
                {
                    case GitHubEventMessageKind.ProjectColumnCreated:
                    case GitHubEventMessageKind.ProjectColumnEdited:
                        return UpdateProjectColumnAsync(projectId.Value, columnId);
                    case GitHubEventMessageKind.ProjectColumnDeleted:
                        return DeleteProjectColumnAsync(projectId.Value, columnId);
                }
            }
        }

        if (message.Kind.IsEvent(GitHubEventMessageKind.EventProjectCard))
        {
            var projectId = GetProjectIdFromUrl(message.Body.ProjectCard.ProjectUrl);
            var columnId = message.Body.ProjectCard.ColumnId;
            var cardId = message.Body.ProjectCard.Id;

            if (projectId is not null)
            {
                switch (message.Kind)
                {
                    case GitHubEventMessageKind.ProjectCardCreated:
                    case GitHubEventMessageKind.ProjectCardMoved:
                        return UpdateProjectCardAsync(projectId.Value, columnId, cardId);
                    case GitHubEventMessageKind.ProjectCardDeleted:
                        return DeleteProjectCardAsync(projectId.Value, columnId, cardId);
                }
            }
        }

        return Task.FromResult(false);
    }

    private static long? GetProjectIdFromUrl(string? projectUrl)
    {
        if (projectUrl is null)
            return null;

        var prefix = @"https://api.github.com/projects/";
        if (!projectUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var remainder = projectUrl.Substring(prefix.Length);
        if (!long.TryParse(remainder, out var result))
            return null;

        return result;
    }

    private async Task<bool> UpdateRepoAsync(string owner, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(name);

        var repoId = new GitHubRepoId(owner, name);

        if (!_repoById.TryGetValue(repoId, out var crawledRepo))
            return false;

        Console.WriteLine($"Crawling repo {owner}/{name}...");

        var repo = await GetRepoAsync(owner, name);
        FillRepo(crawledRepo, owner, repo);

        // Handle the case where a repo was renamed

        var actualRepoId = crawledRepo.GetId();
        if (actualRepoId != repoId)
        {
            Console.WriteLine($"Repo rename detected: {repoId} -> {actualRepoId}");

            _repoById.Remove(repoId);
            _repoById.Add(actualRepoId, crawledRepo);

            await _cache.DeleteRepoAsync(repoId.Owner, repoId.Name);
        }

        _reposThatNeedStoring.Add(crawledRepo);

        return true;
    }

    private Task<bool> DeleteRepoAsync(string owner, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(name);

        var repoId = new GitHubRepoId(owner, name);

        if (!_repoById.TryGetValue(repoId, out var crawledRepo))
            return Task.FromResult(false);

        Console.WriteLine($"Deleting repo {repoId}");

        _repoById.Remove(repoId);

        foreach (var (from, to) in _transferMap.ToArray())
        {
            if (from.RepoId == repoId || to.RepoId == repoId)
            {
                _transferMap.Remove(from);
                _transferMapNeedsStoring = true;
            }
        }

        _reposThatNeedStoring.Remove(crawledRepo);
        _reposThatNeedDeleting.Add(crawledRepo);

        return Task.FromResult(true);
    }

    private async Task<bool> UpdateLabelAsync(string owner, string repo, long labelId, string labelName)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(labelName);

        var repoId = new GitHubRepoId(owner, repo);

        if (!_repoById.TryGetValue(repoId, out var crawledRepo))
            return false;

        var label = await GetLabelAsync(owner, repo, labelName);

        if (!crawledRepo.Labels.TryGetValue(labelId, out var crawledLabel))
        {
            crawledLabel = new GitHubLabel
            {
                Id = labelId
            };
            crawledRepo.Labels.Add(crawledLabel);
        }

        FillLabel(crawledLabel, label);

        _reposThatNeedStoring.Add(crawledRepo);

        return true;
    }

    private Task<bool> DeleteLabelAsync(string owner, string repo, long labelId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repo);

        var repoId = new GitHubRepoId(owner, repo);

        if (!_repoById.TryGetValue(repoId, out var crawledRepo))
            return Task.FromResult(false);

        if (!crawledRepo.Labels.TryGetValue(labelId, out var label))
            return Task.FromResult(false);

        crawledRepo.Labels.Remove(label);

        foreach (var issue in crawledRepo.Issues)
        {
            if (issue.Labels.Contains(label))
                issue.Labels = issue.Labels.Where(l => l != label).ToArray();
        }

        _reposThatNeedStoring.Add(crawledRepo);

        return Task.FromResult(true);
    }

    private async Task<bool> UpdateMilestoneAsync(string owner, string repo, long milestoneId, int milestoneNumber)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repo);

        var repoId = new GitHubRepoId(owner, repo);

        if (!_repoById.TryGetValue(repoId, out var crawledRepo))
            return false;

        var milestone = await GetMilestoneAsync(owner, repo, milestoneNumber);

        if (!crawledRepo.Milestones.TryGetValue(milestoneId, out var crawledMilestone))
        {
            crawledMilestone = new GitHubMilestone
            {
                Id = milestoneId
            };
            crawledRepo.Milestones.Add(crawledMilestone);
        }

        FillMilestone(crawledMilestone, milestone);

        _reposThatNeedStoring.Add(crawledRepo);

        return true;
    }

    private Task<bool> DeleteMilestoneAsync(string owner, string repo, long milestoneId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repo);

        var repoId = new GitHubRepoId(owner, repo);

        if (!_repoById.TryGetValue(repoId, out var crawledRepo))
            return Task.FromResult(false);

        if (!crawledRepo.Milestones.TryGetValue(milestoneId, out var milestone))
            return Task.FromResult(false);

        crawledRepo.Milestones.Remove(milestone);

        foreach (var issue in crawledRepo.Issues)
        {
            if (issue.Milestone == milestone)
                issue.Milestone = null;
        }

        _reposThatNeedStoring.Add(crawledRepo);

        return Task.FromResult(true);
    }

    private async Task<bool> UpdateIssueAsync(string owner, string repoName, int issueNumber, IEnumerable<string> labels, IWorkspaceCrawlerQueue queue)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repoName);
        ArgumentNullException.ThrowIfNull(labels);

        var isKnownIssue = FindCrawledIssue(owner, repoName, issueNumber) is not null;
        var hasRelevantLabels = labels.Any(l => Constants.LabelsForThemesEpicsAndUserStories.Contains(l, StringComparer.OrdinalIgnoreCase));
        if (!isKnownIssue && !hasRelevantLabels)
            return false;

        var rootIssue = await CrawlIssueAsync(owner, repoName, issueNumber);

        EnqueueReferencedItems(new[] { rootIssue }, queue);
        return true;
    }

    private GitHubIssue? FindCrawledIssue(string owner, string repoName, int issueNumber)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repoName);

        var id = new GitHubIssueId(owner, repoName, issueNumber);
        return FindCrawledIssue(id);
    }

    private GitHubIssue? FindCrawledIssue(GitHubIssueId id)
    {
        while (_transferMap.TryGetValue(id, out var transferredId))
            id = transferredId;

        if (_repoById.TryGetValue(id.RepoId, out var repo) && repo.Issues.TryGetValue(id.Number, out var issue))
            return issue;

        return null;
    }

    private Task<GitHubIssue> CrawlIssueAsync(string owner, string repoName, int issueNumber)
    {
        var id = new GitHubIssueId(owner, repoName, issueNumber);
        return CrawlIssueAsync(id);
    }

    private async Task<GitHubIssue> CrawlIssueAsync(GitHubIssueId id)
    {
        var (owner, repoName, issueNumber) = id;

        Console.WriteLine($"Crawling issue {owner}/{repoName}#{issueNumber}...");

        var issue = await GetIssueAsync(owner, repoName, issueNumber);

        var actualId = issue.GetId();

        if (actualId != id)
        {
            Console.WriteLine($"Issue transfer detected: {id} -> {actualId}");

            _transferMap[id] = actualId;
            _transferMapNeedsStoring = true;

            if (_repoById.TryGetValue(id.RepoId, out var existingRepo))
            {
                if (existingRepo.Issues.Remove(issueNumber))
                    _reposThatNeedStoring.Add(existingRepo);
            }
        }

        (owner, repoName, issueNumber) = actualId;

        var crawledRepo = await FindOrCrawlRepoAsync(owner, repoName);

        if (!crawledRepo.Issues.TryGetValue(issueNumber, out var crawledIssue))
        {
            crawledIssue = new GitHubIssue()
            {
                Number = issueNumber
            };

            crawledRepo.Issues.Add(crawledIssue);
        }

        FillIssue(crawledIssue, crawledRepo, issue);

        var timeline = await GetIssueTimelineAsync(owner, repoName, issueNumber);

        FillTimeline(crawledIssue, timeline);

        _reposThatNeedStoring.Add(crawledRepo);

        return crawledIssue;
    }

    private async Task<bool> TransferIssueAsync(string owner, string repoName, int issueNumber, string newOwner, string newRepoName, int newIssueNumber)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repoName);

        ArgumentNullException.ThrowIfNull(newOwner);
        ArgumentNullException.ThrowIfNull(newRepoName);

        var from = new GitHubIssueId(owner, repoName, issueNumber);
        var to = new GitHubIssueId(newOwner, newRepoName, newIssueNumber);

        var fromRepoId = from.RepoId;
        if (!_repoById.TryGetValue(fromRepoId, out var fromRepo))
            return false;

        _reposThatNeedStoring.Add(fromRepo);

        fromRepo.Issues.Remove(issueNumber);
        await CrawlIssueAsync(newOwner, newRepoName, newIssueNumber);

        _transferMap[from] = to;
        _transferMapNeedsStoring = true;

        return true;
    }

    private Task<bool> DeleteIssueAsync(string owner, string repoName, int issueNumber)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repoName);

        var repoId = new GitHubRepoId(owner, repoName);

        if (!_repoById.TryGetValue(repoId, out var repo))
            return Task.FromResult(false);

        if (repo.Issues.Remove(issueNumber))
            return Task.FromResult(false);

        _reposThatNeedStoring.Add(repo);

        return Task.FromResult(true);
    }

    private async Task<GitHubRepo> FindOrCrawlRepoAsync(string owner, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(name);

        var repoId = new GitHubRepoId(owner, name);

        if (!_repoById.TryGetValue(repoId, out var crawledRepo))
        {
            var repo = await GetRepoAsync(owner, name);

            var actualRepoId = new GitHubRepoId(owner, repo.Name);
            if (actualRepoId == repoId || !_repoById.TryGetValue(actualRepoId, out crawledRepo))
            {
                Console.WriteLine($"Crawling repo {owner}/{name}...");

                var labels = await GetLabelsAsync(owner, name);
                var milestones = await GetMilestonesAsync(owner, name);

                crawledRepo = new GitHubRepo();

                FillRepo(crawledRepo, owner, repo);
                FillLabels(crawledRepo, labels);
                FillMilestones(crawledRepo, milestones);

                _repoById.Add(crawledRepo.GetId(), crawledRepo);
                _reposThatNeedStoring.Add(crawledRepo);
            }
        }

        return crawledRepo;
    }

    private async Task<bool> UpdateProjectAsync(long projectId)
    {
        if (!_projectById.TryGetValue(projectId, out var crawledProject))
            return false;

        var project = await GetProjectAsync(projectId);
        FillProject(crawledProject, project);

        _projectsNeedsStoring = true;
        return true;
    }

    private Task<bool> DeleteProjectAsync(long projectId)
    {
        if (!_projectById.Remove(projectId))
            return Task.FromResult(false);

        _projectsNeedsStoring = true;
        return Task.FromResult(true);
    }

    private async Task<bool> UpdateProjectColumnAsync(long projectId, int columnId)
    {
        if (!_projectById.TryGetValue(projectId, out var crawledProject))
            return false;

        await CrawlProjectColumnAsync(crawledProject, columnId);

        _projectsNeedsStoring = true;
        return true;
    }

    private async Task<GitHubProjectColumn> FindOrCrawlProjectColumnAsync(GitHubProject crawledProject, int columnId)
    {
        var crawledColumn = crawledProject.Columns.SingleOrDefault(c => c.Id == columnId);
        if (crawledColumn is not null)
            return crawledColumn;

        return await CrawlProjectColumnAsync(crawledProject, columnId);
    }

    private async Task<GitHubProjectColumn> CrawlProjectColumnAsync(GitHubProject crawledProject, int columnId)
    {
        var column = await GetProjectColumnAsync(columnId);

        var crawledColumn = crawledProject.Columns.SingleOrDefault(c => c.Id == columnId);
        if (crawledColumn is null)
        {
            crawledColumn = new GitHubProjectColumn();
            crawledProject.Columns.Add(crawledColumn);
        }

        FillProjectColumn(crawledColumn, column);

        _projectsNeedsStoring = true;
        return crawledColumn;
    }

    private Task<bool> DeleteProjectColumnAsync(long projectId, int columnId)
    {
        if (!_projectById.TryGetValue(projectId, out var crawledProject))
            return Task.FromResult(false);

        if (crawledProject.Columns.RemoveAll(c => c.Id == columnId) == 0)
            return Task.FromResult(false);

        _projectsNeedsStoring = true;
        return Task.FromResult(true);
    }

    private async Task<bool> UpdateProjectCardAsync(long projectId, int columnId, int cardId)
    {
        if (!_projectById.TryGetValue(projectId, out var crawledProject))
            return false;

        var card = await GetProjectCardAsync(cardId);

        var crawledCard = (GitHubCard?)null;
        var crawledColumn = (GitHubProjectColumn?)null;

        foreach (var col in crawledProject.Columns)
        {
            foreach (var c in col.Cards)
            {
                if (c.Id == cardId)
                {
                    crawledColumn = col;
                    crawledCard = c;
                    break;
                }
            }
        }

        if (crawledCard is null)
            crawledCard = new GitHubCard();

        FillProjectCard(crawledCard, card);

        if (crawledColumn is not null && crawledColumn.Id != columnId)
        {
            crawledColumn.Cards.Remove(crawledCard);
            crawledColumn = null;
        }

        if (crawledColumn is null)
        {
            crawledColumn = await FindOrCrawlProjectColumnAsync(crawledProject, columnId);
            crawledColumn.Cards.Add(crawledCard);
        }

        _projectsNeedsStoring = true;
        return true;
    }

    private Task<bool> DeleteProjectCardAsync(long projectId, int columnId, int cardId)
    {
        if (!_projectById.TryGetValue(projectId, out var crawledProject))
            return Task.FromResult(false);

        var crawledColumn = crawledProject.Columns.SingleOrDefault(c => c.Id == columnId);
        if (crawledColumn is null)
            return Task.FromResult(false);

        if (crawledColumn.Cards.RemoveAll(c => c.Id == cardId) == 0)
            return Task.FromResult(false);

        _projectsNeedsStoring = true;
        return Task.FromResult(true);
    }

    // Reading from GitHub

    private async Task<T> CallGitHub<T>(Func<GitHubClient, Task<T>> operation)
    {
        return await _client.InvokeAsync(operation);
    }

    private Task<IReadOnlyList<Repository>> GetReposAsync(GitHubOrgConfiguration org)
    {
        if (org.IndexingMode == GitHubIndexingMode.ConfiguredRepos)
            return GetReposAsync(org.Name, org.Repos.Select(r => r.Key));
        else
            return GetReposAsync(org.Name);
    }

    private async Task<IReadOnlyList<Repository>> GetReposAsync(string org, IEnumerable<string> repoNames)
    {
        var result = new List<Repository>();

        foreach (var repoName in repoNames)
        {
            var repo = await GetRepoAsync(org, repoName);
            result.Add(repo);
        }

        return result.ToArray();
    }

    private Task<Repository> GetRepoAsync(string org, string repoName)
    {
        return CallGitHub(c => c.Repository.Get(org, repoName));
    }

    private Task<IReadOnlyList<Repository>> GetReposAsync(string org)
    {
        return CallGitHub(c => c.Repository.GetAllForOrg(org));
    }

    private Task<IReadOnlyList<Milestone>> GetMilestonesAsync(string owner, string repo)
    {
        var request = new MilestoneRequest
        {
            State = ItemStateFilter.All
        };

        return CallGitHub(c => c.Issue.Milestone.GetAllForRepository(owner, repo, request));
    }

    private Task<Milestone> GetMilestoneAsync(string owner, string repo, int milestoneNumber)
    {
        return CallGitHub(c => c.Issue.Milestone.Get(owner, repo, milestoneNumber));
    }

    private Task<IReadOnlyList<Label>> GetLabelsAsync(string owner, string repo)
    {
        return CallGitHub(c => c.Issue.Labels.GetAllForRepository(owner, repo));
    }

    private Task<Label> GetLabelAsync(string owner, string repo, string labelName)
    {
        return CallGitHub(c => c.Issue.Labels.Get(owner, repo, labelName));
    }

    private Task<IReadOnlyList<Issue>> GetIssuesAsync(string owner, string repo, RepositoryIssueRequest request)
    {
        return CallGitHub(c => c.Issue.GetAllForRepository(owner, repo, request));
    }

    private Task<Issue> GetIssueAsync(string owner, string repoName, int issueNumber)
    {
        return CallGitHub(c => c.Issue.Get(owner, repoName, issueNumber));
    }

    private async Task<IReadOnlyList<Project>> GetProjectsAsync(GitHubOrgConfiguration org)
    {
        if (!org.IncludedProjects.Any())
            return Array.Empty<Project>();

        var projects = await GetProjectsAsync(org.Name);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        names.UnionWith(org.IncludedProjects);

        return projects.Where(p => names.Contains(p.Name))
                       .ToArray();
    }

    private Task<IReadOnlyList<Project>> GetProjectsAsync(string org)
    {
        var projectRequest = new ProjectRequest(ItemStateFilter.Open);
        return CallGitHub(c => c.Repository.Project.GetAllForOrganization(org, projectRequest));
    }

    private Task<Project> GetProjectAsync(long projectId)
    {
        return CallGitHub(c => c.Repository.Project.Get((int)projectId));
    }

    private Task<IReadOnlyList<ProjectColumn>> GetProjectColumnsAsync(Project project)
    {
        return CallGitHub(c => c.Repository.Project.Column.GetAll(project.Id));
    }

    private Task<ProjectColumn> GetProjectColumnAsync(int columnId)
    {
        return CallGitHub(c => c.Repository.Project.Column.Get(columnId));
    }

    private Task<IReadOnlyList<ProjectCard>> GetProjectCardsAsync(ProjectColumn column)
    {
        return CallGitHub(c => c.Repository.Project.Card.GetAll(column.Id));
    }

    private Task<ProjectCard> GetProjectCardAsync(int cardId)
    {
        return CallGitHub(c => c.Repository.Project.Card.Get(cardId));
    }

    private Task<IReadOnlyList<TimelineEventInfo>> GetIssueTimelineAsync(string owner, string repo, int number)
    {
        return CallGitHub(c => c.Issue.Timeline.GetAllForIssue(owner, repo, number));
    }

    private async Task<GitHubRepo> ConvertRepoAsync(string owner, Repository repo)
    {
        var labels = await GetLabelsAsync(owner, repo.Name);
        var milestones = await GetMilestonesAsync(owner, repo.Name);

        var result = new GitHubRepo();
        FillRepo(result, owner, repo);
        FillLabels(result, labels);
        FillMilestones(result, milestones);

        return result;
    }

    private static GitHubIssue ConvertIssue(GitHubRepo repo, Issue issue)
    {
        var result = new GitHubIssue();
        FillIssue(result, repo, issue);
        return result;
    }

    private static GitHubLabel ConvertLabel(Label label)
    {
        var result = new GitHubLabel();
        FillLabel(result, label);
        return result;
    }

    private static GitHubMilestone ConvertMilestone(Milestone milestone)
    {
        var result = new GitHubMilestone();
        FillMilestone(result, milestone);
        return result;
    }

    private static void FillRepo(GitHubRepo target, string owner, Repository repo)
    {
        target.Id = repo.Id;
        target.NodeId = repo.NodeId;
        target.Owner = owner;
        target.Name = repo.Name;
        target.IsPublic = !repo.Private;
    }

    private static void FillMilestones(GitHubRepo target, IReadOnlyList<Milestone> milestones)
    {
        foreach (var milestone in milestones)
        {
            var convertedMilestone = ConvertMilestone(milestone);
            target.Milestones.Add(convertedMilestone);
        }
    }

    private static void FillLabels(GitHubRepo target, IReadOnlyList<Label> labels)
    {
        foreach (var label in labels)
        {
            var convertedLabel = ConvertLabel(label);
            if (target.Labels.Contains(label.Id))
            {
                var existingLabel = target.Labels[label.Id];
                AnsiConsole.MarkupLine($"[yellow]warning: repo {target.FullName} has a duplicated label with ID {label.Id}. New: '{label.Name}'. Existing: '{existingLabel.Name}'[/]");
            }
            else
            {
                target.Labels.Add(convertedLabel);
            }
        }
    }

    private static void FillLabel(GitHubLabel target, Label label)
    {
        target.Id = label.Id;
        target.NodeId = label.NodeId;
        target.Name = label.Name;
        target.Description = label.Description;
        target.Color = label.Color;
    }

    private static void FillMilestone(GitHubMilestone target, Milestone milestone)
    {
        target.Id = milestone.Id;
        target.NodeId = milestone.NodeId;
        target.IsOpen = milestone.State.Value == ItemState.Open;
        target.Title = milestone.Title;
        target.Description = milestone.Description;
    }

    private static void FillIssue(GitHubIssue target, GitHubRepo repo, Issue issue)
    {
        var id = issue.Id;
        var nodeId = issue.NodeId;
        var number = issue.Number;
        var isOpen = issue.State.Value == ItemState.Open;
        var title = issue.Title;
        var body = issue.Body ?? string.Empty;
        var assignees = issue.Assignees.Select(a => a.Login).ToArray();
        var labels = issue.Labels.Select(l => repo.Labels.GetValueOrDefault(l.Id))
                                 .Where(l => l is not null)
                                 .Select(l => l!)
                                 .ToArray();
        var milestone = issue.Milestone is null
                            ? null
                            : repo.Milestones.GetValueOrDefault(issue.Milestone.Id);
        var createdAt = issue.CreatedAt;
        var createdBy = issue.User.Login;
        var updatedAt = issue.UpdatedAt;
        var closedAt = issue.ClosedAt;
        var closedBy = issue.ClosedBy?.Login;

        target.Repo = repo;
        target.Id = id;
        target.NodeId = nodeId;
        target.Number = number;
        target.IsOpen = isOpen;
        target.Title = title;
        target.Body = body;
        target.Assignees = assignees;
        target.Labels = labels;
        target.Milestone = milestone;
        target.CreatedAt = createdAt;
        target.CreatedBy = createdBy;
        target.UpdatedAt = updatedAt;
        target.ClosedAt = closedAt;
        target.ClosedBy = closedBy;
    }

    private static void FillTimeline(GitHubIssue issue, IReadOnlyList<TimelineEventInfo> timelineEvents)
    {
        var events = new List<GitHubIssueEvent>(timelineEvents.Count);

        foreach (var timelineEvent in timelineEvents)
        {
            if (!_relevantEventNames.Contains(timelineEvent.Event.StringValue))
                continue;

            var crawledEvent = new GitHubIssueEvent
            {
                Id = timelineEvent.Id,
                NodeId = timelineEvent.NodeId,
                Event = timelineEvent.Event.StringValue,
                Actor = timelineEvent.Actor?.Login,
                CreatedAt = timelineEvent.CreatedAt,
                CommitId = timelineEvent.CommitId,
                Assignee = timelineEvent.Assignee?.Login,
                Label = timelineEvent.Label?.Name,
                Milestone = timelineEvent.Milestone?.Title,
            };
            events.Add(crawledEvent);

            if (timelineEvent.Rename is not null)
            {
                crawledEvent.Rename = new GitHubRenameEvent
                {
                    From = timelineEvent.Rename.From,
                    To = timelineEvent.Rename.To
                };
            }

            if (timelineEvent.ProjectCard is not null)
            {
                crawledEvent.Card = new GitHubCardEvent
                {
                    CardId = timelineEvent.ProjectCard.Id,
                    ProjectId = timelineEvent.ProjectCard.ProjectId,
                    ColumnName = timelineEvent.ProjectCard.ColumnName,
                    PreviousColumnName = timelineEvent.ProjectCard.PreviousColumnName
                };
            }
        }

        issue.Events = events.ToArray();
    }

    private static void FillProject(GitHubProject target, Project project)
    {
        target.Id = project.Id;
        target.NodeId = project.NodeId;
        target.Name = project.Name;
        target.Number = project.Number;
        target.CreatedAt = project.CreatedAt;
        target.CreatedBy = project.Creator.Login;
        target.UpdatedAt = project.UpdatedAt;
        target.Url = project.HtmlUrl;
    }

    private static void FillProjectColumn(GitHubProjectColumn target, ProjectColumn column)
    {
        target.Id = column.Id;
        target.NodeId = column.NodeId;
        target.Name = column.Name;
        target.CreatedAt = column.CreatedAt;
        target.UpdatedAt = column.UpdatedAt;
    }

    private static void FillProjectCard(GitHubCard target, ProjectCard card)
    {
        var issueIdText = (string?)null;

        if (GitHubIssueId.TryParse(card.ContentUrl, out var issueId) ||
            GitHubIssueId.TryParse(card.Note, out issueId))
        {
            issueIdText = issueId.ToString();
        }

        target.Id = card.Id;
        target.NodeId = card.NodeId;
        target.CreatedAt = card.CreatedAt;
        target.CreatedBy = card.Creator.Login;
        target.Note = card.Note;
        target.UpdatedAt = card.UpdatedAt;
        target.IssueId = issueIdText;
    }

    public void GetSnapshot(out IReadOnlyList<GitHubIssue> gitHubIssues,
                        out IReadOnlyDictionary<GitHubIssueId, GitHubIssueId> gitHubTransferMap,
                        out IReadOnlyList<GitHubProject> gitHubProjects)
    {
        gitHubIssues = _repoById.Values.SelectMany(r => r.Issues).ToArray();
        gitHubTransferMap = _transferMap.ToDictionary(kv => kv.Key, kv => kv.Value);
        gitHubProjects = _projectById.Values.ToArray();
    }
}
