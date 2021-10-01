namespace ThemesOfDotNet.Indexing.Configuration;

public sealed class TeamConfiguration
{
    public TeamConfiguration(IReadOnlyList<string> includedAreas)
    {
        IncludedAreas = includedAreas ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> IncludedAreas { get; }
}
