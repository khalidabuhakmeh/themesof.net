using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThemesOfDotNet.Indexing.Configuration;

public sealed class SubscriptionConfiguration
{
    public static SubscriptionConfiguration Empty { get; } = new();

    public static Task<SubscriptionConfiguration> LoadAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var stream = File.OpenRead(path);
        return LoadAsync(stream);
    }

    public static async Task<SubscriptionConfiguration> LoadAsync(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        return (await JsonSerializer.DeserializeAsync<SubscriptionConfiguration>(stream, options))!;
    }

    public async Task SaveAsync(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        await JsonSerializer.SerializeAsync(stream, this, options)!;
    }

    private SubscriptionConfiguration()
    {
        GitHubOrgs = Array.Empty<GitHubOrgConfiguration>();
        AzureDevOpsQueries = Array.Empty<AzureDevOpsQueryConfiguration>();
        Milestones = MilestoneConfiguration.Empty;
        Teams = new Dictionary<string, IReadOnlyList<TeamConfiguration>>();
        Tree = Array.Empty<AreaNodeConfiguration>();
        GitHubOrgByName = new Dictionary<string, GitHubOrgConfiguration>();
        AzureDevOpsQueryById = new Dictionary<string, AzureDevOpsQueryConfiguration>();
    }

    public SubscriptionConfiguration(IReadOnlyList<GitHubOrgConfiguration>? gitHubOrgs,
                                     IReadOnlyList<AzureDevOpsQueryConfiguration>? azureDevOpsQueries,
                                     MilestoneConfiguration milestones,
                                     IReadOnlyDictionary<string, IReadOnlyList<TeamConfiguration>> teams,
                                     IReadOnlyList<AreaNodeConfiguration>? tree)
    {
        GitHubOrgs = gitHubOrgs ?? Array.Empty<GitHubOrgConfiguration>();
        AzureDevOpsQueries = azureDevOpsQueries ?? Array.Empty<AzureDevOpsQueryConfiguration>();
        Milestones = milestones;
        Teams = teams ?? new Dictionary<string, IReadOnlyList<TeamConfiguration>>();
        Tree = tree ?? Array.Empty<AreaNodeConfiguration>();

        var orgByName = new Dictionary<string, GitHubOrgConfiguration>(StringComparer.OrdinalIgnoreCase);

        foreach (var org in GitHubOrgs)
        {
            if (!orgByName.TryAdd(org.Name, org))
                throw new ArgumentException($"The org '{org.Name}' is configured more than once", nameof(gitHubOrgs));
        }

        var queryById = new Dictionary<string, AzureDevOpsQueryConfiguration>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in AzureDevOpsQueries)
        {
            if (!queryById.TryAdd(query.QueryId, query))
                throw new ArgumentException($"The Azure DevOps query '{query.QueryId}' is configured more than once", nameof(azureDevOpsQueries));
        }

        GitHubOrgByName = orgByName;
        AzureDevOpsQueryById = queryById;
    }

    public IReadOnlyList<GitHubOrgConfiguration> GitHubOrgs { get; }

    [JsonIgnore]
    public IReadOnlyDictionary<string, GitHubOrgConfiguration> GitHubOrgByName { get; }

    public IReadOnlyList<AzureDevOpsQueryConfiguration> AzureDevOpsQueries { get; }

    [JsonIgnore]
    public IReadOnlyDictionary<string, AzureDevOpsQueryConfiguration> AzureDevOpsQueryById { get; }

    public MilestoneConfiguration Milestones { get; }

    [JsonConverter(typeof(CaseInsensitiveDictionaryConverter))]
    public IReadOnlyDictionary<string, IReadOnlyList<TeamConfiguration>> Teams { get; }

    public IReadOnlyList<AreaNodeConfiguration> Tree { get; }
}
