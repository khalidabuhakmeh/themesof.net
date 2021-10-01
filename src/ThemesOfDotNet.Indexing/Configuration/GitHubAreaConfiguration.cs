namespace ThemesOfDotNet.Indexing.Configuration;

public sealed class GitHubAreaConfiguration
{
    public GitHubAreaConfiguration(string area,
                                   IReadOnlyList<string>? includedPaths,
                                   IReadOnlyList<string>? includedLabels,
                                   IReadOnlyList<int>? issues)
    {
        ArgumentNullException.ThrowIfNull(area);

        Area = area;
        IncludedPaths = includedPaths ?? Array.Empty<string>();
        IncludedLabels = includedLabels ?? Array.Empty<string>();
        Issues = issues ?? Array.Empty<int>();
    }

    public string Area { get; }

    public IReadOnlyList<string> IncludedPaths { get; }

    public IReadOnlyList<string> IncludedLabels { get; }

    public IReadOnlyList<int> Issues { get; }
}
