using System.Text.Json.Serialization;

namespace ThemesOfDotNet.Indexing.Configuration;

public sealed class GitHubOrgConfiguration
{
    public GitHubOrgConfiguration(string name,
                                  GitHubIndexingMode indexingMode,
                                  string? defaultProduct,
                                  IReadOnlyList<string>? mappedProducts,
                                  IReadOnlyList<string>? includedProjects,
                                  IReadOnlyDictionary<string, GitHubRepoConfiguration>? repos)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
        IndexingMode = indexingMode;
        DefaultProduct = defaultProduct;
        MappedProducts = mappedProducts ?? Array.Empty<string>();
        IncludedProjects = includedProjects ?? Array.Empty<string>();
        Repos = repos ?? new Dictionary<string, GitHubRepoConfiguration>();
    }

    public string Name { get; }

    public GitHubIndexingMode IndexingMode { get; }

    public string? DefaultProduct { get; }

    public IReadOnlyList<string> MappedProducts { get; }

    public IReadOnlyList<string> IncludedProjects { get; }

    [JsonConverter(typeof(CaseInsensitiveDictionaryConverter))]
    public IReadOnlyDictionary<string, GitHubRepoConfiguration> Repos { get; }
}
