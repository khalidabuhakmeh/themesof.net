using Octokit;

namespace ThemesOfDotNet.Indexing.GitHub;

public sealed class GitHubIncrementalCrawler : GitHubCrawler
{
    private readonly GitHubCache _cache;
    private readonly Dictionary<GitHubRepoId, GitHubRepo> _repoById = new();
    private readonly Dictionary<long, GitHubProject> _projectById = new();
    private readonly Dictionary<GitHubIssueId, GitHubIssueId> _transferMap = new();
    private readonly HashSet<GitHubRepo> _reposThatNeedStoring = new();
    private readonly HashSet<GitHubRepo> _reposThatNeedDeleting = new();

    private bool _transferMapNeedsStoring = false;
    private bool _projectsNeedsStoring = false;

    public static async Task<GitHubIncrementalCrawler> CreateAsync(string appId, string privateKey, GitHubCache cache)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentNullException.ThrowIfNull(cache);

        var result = new GitHubIncrementalCrawler(appId, privateKey, cache);
        await result.InitializeAsync();
        return result;
    }

    private GitHubIncrementalCrawler(string appId, string privateKey, GitHubCache cache)
        : base(appId, privateKey)
    {
        _cache = cache;
    }

    private async Task InitializeAsync()
    {
        var repos = await _cache.LoadReposAsync();

        foreach (var repo in repos)
            _repoById.Add(repo.GetId(), repo);

        var projects = await _cache.LoadProjectsAsync();

        foreach (var project in projects)
            _projectById.Add(project.Id, project);

        foreach (var (from, to) in await _cache.LoadTransferMapAsync())
            _transferMap.Add(from, to);
    }

    public async Task<bool> UpdateRepoAsync(string owner, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(name);

        var repoId = new GitHubRepoId(owner, name);

        if (!_repoById.TryGetValue(repoId, out var crawledRepo))
            return false;

        Console.WriteLine($"Crawling repo {owner}/{name}...");

        var repo = await CallGitHub(c => c.Repository.Get(owner, name));
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

    public Task<bool> DeleteRepoAsync(string owner, string name)
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

    public async Task<bool> UpdateLabelAsync(string owner, string repo, long labelId, string labelName)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(labelName);

        var repoId = new GitHubRepoId(owner, repo);

        if (!_repoById.TryGetValue(repoId, out var crawledRepo))
            return false;

        var label = await CallGitHub(c => c.Issue.Labels.Get(owner, repo, labelName));

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

    public Task<bool> DeleteLabelAsync(string owner, string repo, long labelId)
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

    public async Task<bool> UpdateMilestoneAsync(string owner, string repo, long milestoneId, int milestoneNumber)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repo);

        var repoId = new GitHubRepoId(owner, repo);

        if (!_repoById.TryGetValue(repoId, out var crawledRepo))
            return false;

        var milestone = await CallGitHub(c => c.Issue.Milestone.Get(owner, repo, milestoneNumber));

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

    public Task<bool> DeleteMilestoneAsync(string owner, string repo, long milestoneId)
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

    public async Task<bool> UpdateIssueAsync(string owner, string repoName, int issueNumber, IEnumerable<string> labels)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repoName);
        ArgumentNullException.ThrowIfNull(labels);

        var isKnownIssue = FindCrawledIssue(owner, repoName, issueNumber) is not null;
        var hasRelevantLabels = labels.Any(l => Constants.LabelsForThemesEpicsAndUserStories.Contains(l, StringComparer.OrdinalIgnoreCase));
        if (!isKnownIssue && !hasRelevantLabels)
            return false;

        var rootIssue = await CrawlIssueAsync(owner, repoName, issueNumber);

        var remainingIssues = new Queue<GitHubIssue>();
        remainingIssues.Enqueue(rootIssue);

        while (remainingIssues.Count > 0)
        {
            var issue = remainingIssues.Dequeue();
            var issueId = issue.GetId();
            var repoId = issueId.RepoId;

            foreach (var issueLink in GitHubIssueParser.ParseLinks(repoId, issue.Body))
            {
                var linkedId = issueLink.LinkedId;

                if (_transferMap.TryGetValue(linkedId, out var transferredId))
                    linkedId = transferredId;

                var linkedIssue = FindCrawledIssue(linkedId.Owner, linkedId.Repo, linkedId.Number);
                if (linkedIssue is null)
                {
                    try
                    {
                        linkedIssue = await CrawlIssueAsync(linkedId.Owner, linkedId.Repo, linkedId.Number);
                    }
                    catch (NotFoundException)
                    {
                        Console.WriteLine($"Couldn't resolve issue {linkedId}.");
                        continue;
                    }
                }

                remainingIssues.Enqueue(linkedIssue);
            }
        }

        return true;
    }

    private GitHubIssue? FindCrawledIssue(string owner, string repoName, int issueNumber)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repoName);

        var repoId = new GitHubRepoId(owner, repoName);

        if (_repoById.TryGetValue(repoId, out var repo) && repo.Issues.TryGetValue(issueNumber, out var issue))
            return issue;

        return null;
    }

    private async Task<GitHubIssue> CrawlIssueAsync(string owner, string repoName, int issueNumber)
    {
        Console.WriteLine($"Crawling issue {owner}/{repoName}#{issueNumber}...");

        var issue = await CallGitHub(c => c.Issue.Get(owner, repoName, issueNumber));

        var id = new GitHubIssueId(owner, repoName, issueNumber);
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

        var timeline = await CallGitHub(c => c.Issue.Timeline.GetAllForIssue(owner, repoName, issueNumber));

        FillTimeline(crawledIssue, timeline);

        _reposThatNeedStoring.Add(crawledRepo);

        return crawledIssue;
    }

    public async Task<bool> TransferIssueAsync(string owner, string repoName, int issueNumber, string newOwner, string newRepoName, int newIssueNumber)
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

    public Task<bool> DeleteIssueAsync(string owner, string repoName, int issueNumber)
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
            var repo = await CallGitHub(c => c.Repository.Get(owner, name));

            var actualRepoId = new GitHubRepoId(owner, repo.Name);
            if (actualRepoId == repoId || !_repoById.TryGetValue(actualRepoId, out crawledRepo))
            {
                Console.WriteLine($"Crawling repo {owner}/{name}...");

                var labels = await CallGitHub(c => c.Issue.Labels.GetAllForRepository(owner, name));
                var milestones = await CallGitHub(c => c.Issue.Milestone.GetAllForRepository(owner, name, new MilestoneRequest { State = ItemStateFilter.All }));

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

    public async Task<bool> UpdateProjectAsync(long projectId)
    {
        if (!_projectById.TryGetValue(projectId, out var crawledProject))
            return false;

        var project = await CallGitHub(c => c.Repository.Project.Get((int)projectId));
        FillProject(crawledProject, project);

        _projectsNeedsStoring = true;
        return true;
    }

    public Task<bool> DeleteProjectAsync(long projectId)
    {
        if (!_projectById.Remove(projectId))
            return Task.FromResult(false);

        _projectsNeedsStoring = true;
        return Task.FromResult(true);
    }

    public async Task<bool> UpdateProjectColumnAsync(long projectId, int columnId)
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
        var column = await CallGitHub(c => c.Repository.Project.Column.Get(columnId));

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

    public Task<bool> DeleteProjectColumnAsync(long projectId, int columnId)
    {
        if (!_projectById.TryGetValue(projectId, out var crawledProject))
            return Task.FromResult(false);

        if (crawledProject.Columns.RemoveAll(c => c.Id == columnId) == 0)
            return Task.FromResult(false);

        _projectsNeedsStoring = true;
        return Task.FromResult(true);
    }

    public async Task<bool> UpdateProjectCardAsync(long projectId, int columnId, int cardId)
    {
        if (!_projectById.TryGetValue(projectId, out var crawledProject))
            return false;

        var card = await CallGitHub(c => c.Repository.Project.Card.Get(cardId));

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

    public Task<bool> DeleteProjectCardAsync(long projectId, int columnId, int cardId)
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

    public async Task<bool> StoreAsync()
    {
        var result = false;

        foreach (var repo in _reposThatNeedStoring)
        {
            Console.WriteLine($"Updating cached repo {repo.FullName}...");
            await _cache.StoreRepoAsync(repo);
            result = true;
        }

        _reposThatNeedStoring.Clear();

        foreach (var repo in _reposThatNeedDeleting)
        {
            Console.WriteLine($"Deleting cached repo {repo.FullName}...");
            await _cache.DeleteRepoAsync(repo.Owner, repo.Name);
            result = true;
        }

        _reposThatNeedDeleting.Clear();

        if (_transferMapNeedsStoring)
        {
            Console.WriteLine($"Updating cached transfer map...");
            await _cache.StoreTransferMapAsync(_transferMap);
            _transferMapNeedsStoring = false;
            result = true;
        }

        if (_projectsNeedsStoring)
        {
            Console.WriteLine($"Updating projects...");
            await _cache.StoreProjectsAsync(_projectById.Values.ToArray());
            _projectsNeedsStoring = false;
            result = true;
        }

        return result;
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
