using System.Text.Json;
using System.Text.Json.Serialization;

using Spectre.Console;

using ThemesOfDotNet.Indexing.Storage;

namespace ThemesOfDotNet.Indexing.GitHub;

public sealed class GitHubCache
{
    private readonly KeyValueStore _store;

    public GitHubCache(KeyValueStore store)
    {
        _store = store;
    }

    private static string TransferMapKey => "transfermap.json";

    private static string ProjectsKey => "projects.json";

    public Task ClearAsync()
    {
        return _store.ClearAsync();
    }

    public async Task<IReadOnlyDictionary<GitHubIssueId, GitHubIssueId>> LoadTransferMapAsync()
    {
        var json = await _store.LoadAsync(TransferMapKey);

        if (json is null)
            return new Dictionary<GitHubIssueId, GitHubIssueId>();

        return JsonToTransferMap(json);
    }

    public async Task StoreTransferMapAsync(IReadOnlyDictionary<GitHubIssueId, GitHubIssueId> map)
    {
        var json = TransferMapToJson(map);
        await _store.StoreAsync(TransferMapKey, json);
    }

    public async Task<IReadOnlyList<GitHubRepo>> LoadReposAsync()
    {
        var repos = new List<GitHubRepo>();

        await foreach (var key in _store.GetKeysAsync())
        {
            if (key == TransferMapKey || key == ProjectsKey)
                continue;

            var json = await _store.LoadAsync(key);
            var repo = JsonToRepo(json!);
            repos.Add(repo);
        }

        return repos.ToArray();
    }

    public async Task StoreRepoAsync(GitHubRepo repo)
    {
        try
        {
            var json = RepoToJson(repo);
            var (owner, repoName) = repo.GetId();
            var key = Path.Join(owner, $"{repoName}.json");
            await _store.StoreAsync(key, json);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]error: Can't save {repo.GetId()}:[/]");
            AnsiConsole.WriteException(ex);
        }
    }

    public Task DeleteRepoAsync(string owner, string repoName)
    {
        var key = Path.Join(owner, $"{repoName}.json");
        return _store.RemoveAsync(key);
    }

    public async Task<IReadOnlyList<GitHubProject>> LoadProjectsAsync()
    {
        var json = await _store.LoadAsync(ProjectsKey);

        if (json is null)
            return Array.Empty<GitHubProject>();

        return JsonToProjects(json);
    }

    public async Task StoreProjectsAsync(IReadOnlyList<GitHubProject> projects)
    {
        var json = ProjectsToJson(projects);
        await _store.StoreAsync(ProjectsKey, json);
    }

    private static Dictionary<GitHubIssueId, GitHubIssueId> JsonToTransferMap(string json)
    {
        return JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(json)!
                             .ToDictionary(kv => GitHubIssueId.Parse(kv.Key), kv => GitHubIssueId.Parse(kv.Value));
    }

    private static string TransferMapToJson(IReadOnlyDictionary<GitHubIssueId, GitHubIssueId> map)
    {
        var stringMap = map.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value.ToString());
        return JsonSerializer.Serialize(stringMap);
    }

    private static GitHubRepo JsonToRepo(string json)
    {
        var options = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.Preserve
        };

        return JsonSerializer.Deserialize<GitHubRepo>(json, options)!;
    }

    private static string RepoToJson(GitHubRepo repo)
    {
        var options = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.Preserve
        };

        return JsonSerializer.Serialize(repo, options);
    }

    private static IReadOnlyList<GitHubProject> JsonToProjects(string json)
    {
        return JsonSerializer.Deserialize<IReadOnlyList<GitHubProject>>(json)!;
    }

    private static string ProjectsToJson(IReadOnlyList<GitHubProject> projects)
    {
        return JsonSerializer.Serialize(projects);
    }
}
