namespace ThemesOfDotNet.Indexing.Configuration;

public sealed class GitHubRepoConfiguration
{
    public GitHubRepoConfiguration(string? defaultProduct,
                                   IReadOnlyList<string>? mappedProducts,
                                   IReadOnlyList<GitHubAreaConfiguration>? areas)
    {
        DefaultProduct = defaultProduct;
        MappedProducts = mappedProducts ?? Array.Empty<string>();
        Areas = areas ?? Array.Empty<GitHubAreaConfiguration>();
    }

    public string? DefaultProduct { get; }

    public IReadOnlyList<string> MappedProducts { get; }

    public IReadOnlyList<GitHubAreaConfiguration> Areas { get; }
}
