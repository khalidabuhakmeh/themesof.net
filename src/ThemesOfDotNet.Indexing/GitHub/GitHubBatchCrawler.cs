using System.Diagnostics;

using Octokit;

using ThemesOfDotNet.Indexing.Configuration;

namespace ThemesOfDotNet.Indexing.GitHub;

public sealed class GitHubBatchCrawler : GitHubCrawler
{
    private readonly GitHubCache _cache;

    public GitHubBatchCrawler(string appId, string privateKey, GitHubCache cache)
        : base(appId, privateKey)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    public async Task CrawlAsync(IReadOnlyList<GitHubOrgConfiguration> orgs)
    {
        var repos = new List<GitHubRepo>();
        var repoById = new Dictionary<GitHubRepoId, GitHubRepo>();

        var projects = new List<GitHubProject>();

        foreach (var org in orgs)
        {
            if (org.IndexingMode == GitHubIndexingMode.NoRepos)
                continue;

            Console.WriteLine($"Fetching repos for {org.Name}...");

            foreach (var repo in await GetReposAsync(org))
            {
                Debug.Assert(repo is not null);

                if (!repo.HasIssues)
                {
                    Console.WriteLine($"Skipped {repo.FullName} because issues are disabled.");
                    continue;
                }

                var fetchedIssues = false;

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
                    fetchedIssues = true;

                    var convertedIssues = issuesAndPullRequests.Select(i => ConvertIssue(convertedRepo, i));
                    foreach (var convertedIssue in convertedIssues)
                    {
                        if (!convertedRepo.Issues.Contains(convertedIssue.Number))
                            convertedRepo.Issues.Add(convertedIssue);
                    }
                }

                if (!fetchedIssues)
                {
                    Console.WriteLine($"Skipped {repo.FullName} because it doesn't define any of the standard labels");
                }
                else
                {
                    repos.Add(convertedRepo);
                    repoById.Add(convertedRepo.GetId(), convertedRepo);

                    Console.WriteLine($"Fetched {convertedRepo.Issues.Count:N0} issues from {repo.FullName}");
                }
            }

            Console.WriteLine($"Fetching projects for {org.Name}...");

            foreach (var project in await GetProjectsAsync(org))
            {
                var convertedProject = new GitHubProject();
                FillProject(convertedProject, project);

                projects.Add(convertedProject);

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

                Console.WriteLine($"Fetched {convertedProject.Columns.Sum(c => c.Cards.Count):N0} cards for project {convertedProject.Name}");
            }
        }

        var issueById = new Dictionary<GitHubIssueId, GitHubIssue>();
        foreach (var issue in repos.SelectMany(r => r.Issues))
            issueById.Add(issue.GetId(), issue);

        var transferMap = new Dictionary<GitHubIssueId, GitHubIssueId>();

        var queue = new Queue<GitHubIssue>(issueById.Values);

        while (queue.Count > 0)
        {
            var issue = queue.Dequeue();

            foreach (var issueLink in GitHubIssueParser.ParseLinks(issue.Repo.GetId(), issue.Body))
            {
                var linkedId = issueLink.LinkedId;

                if (!issueById.ContainsKey(linkedId))
                {
                    Console.WriteLine($"Fetching issue {linkedId}...");
                    try
                    {
                        var (owner, repo, number) = linkedId;
                        var linkedIssue = await GetIssueAsync(owner, repo, number);
                        var actualId = linkedIssue.GetId();
                        if (actualId != linkedId)
                        {
                            Console.WriteLine($"Recording transfer {linkedId} -> {actualId}...");
                            transferMap[linkedId] = actualId;
                        }

                        if (issueById.ContainsKey(actualId))
                        {
                            issueById[linkedId] = issueById[actualId];
                        }
                        else
                        {
                            if (!repoById.TryGetValue(actualId.RepoId, out var linkedRepo))
                            {
                                Console.WriteLine($"Fetching repo {actualId.RepoId}...");
                                linkedRepo = await ConvertRepoAsync(actualId.RepoId.Owner, actualId.RepoId.Name);
                                repoById.Add(actualId.RepoId, linkedRepo);
                            }

                            var convertedLinkedIssue = ConvertIssue(linkedRepo, linkedIssue);
                            linkedRepo.Issues.Add(convertedLinkedIssue);
                            issueById[linkedId] = issueById[actualId] = convertedLinkedIssue;
                            queue.Enqueue(convertedLinkedIssue);
                        }
                    }
                    catch (NotFoundException)
                    {
                        GitHubActions.Warning($"Can't find {linkedId}, linked from {issue.GetId()}");
                    }
                    catch (Exception ex)
                    {
                        GitHubActions.Error($"Can't fetch {linkedId}, linked from {issue.GetId()}");
                        GitHubActions.Error(ex);
                    }
                }
            }
        }

        Console.WriteLine("Fetching issue events...");

        foreach (var issue in repos.SelectMany(r => r.Issues))
        {
            var issueId = issue.GetId();
            Console.WriteLine($"Fetching timeline for {issueId}...");

            try
            {
                var (owner, repo, number) = issueId;
                var timelineEvents = await GetIssueTimelineAsync(owner, repo, number);
                FillTimeline(issue, timelineEvents);
            }
            catch (Exception ex)
            {
                GitHubActions.Error("Can't fetch timeline:");
                GitHubActions.Error(ex);
            }
        }

        await _cache.ClearAsync();

        Console.WriteLine($"Caching {transferMap.Count:N0} issue transers...");
        await _cache.StoreTransferMapAsync(transferMap);

        Console.WriteLine($"Caching {repoById.Count:N0} issues...");
        foreach (var repo in repoById.Values)
            await _cache.StoreRepoAsync(repo);

        Console.WriteLine($"Caching {projects.Count:N0} projects...");
        await _cache.StoreProjectsAsync(projects);
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

    private Task<IReadOnlyList<Label>> GetLabelsAsync(string owner, string repo)
    {
        return CallGitHub(c => c.Issue.Labels.GetAllForRepository(owner, repo));
    }

    private Task<IReadOnlyList<Issue>> GetIssuesAsync(string owner, string repo, RepositoryIssueRequest request)
    {
        return CallGitHub(c => c.Issue.GetAllForRepository(owner, repo, request));
    }

    private Task<Issue> GetIssueAsync(string owner, string repo, int number)
    {
        return CallGitHub(c => c.Issue.Get(owner, repo, number));
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

    private Task<IReadOnlyList<ProjectColumn>> GetProjectColumnsAsync(Project project)
    {
        return CallGitHub(c => c.Repository.Project.Column.GetAll(project.Id));
    }

    private Task<IReadOnlyList<ProjectCard>> GetProjectCardsAsync(ProjectColumn column)
    {
        return CallGitHub(c => c.Repository.Project.Card.GetAll(column.Id));
    }

    private Task<IReadOnlyList<TimelineEventInfo>> GetIssueTimelineAsync(string owner, string repo, int number)
    {
        return CallGitHub(c => c.Issue.Timeline.GetAllForIssue(owner, repo, number));
    }

    private async Task<GitHubRepo> ConvertRepoAsync(string owner, string repoName)
    {
        var repo = await GetRepoAsync(owner, repoName);
        return await ConvertRepoAsync(owner, repo);
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
}
