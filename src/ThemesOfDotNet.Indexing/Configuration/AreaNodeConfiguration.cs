namespace ThemesOfDotNet.Indexing.Configuration;

public sealed class AreaNodeConfiguration
{
    public AreaNodeConfiguration(string title,
                                 IReadOnlyList<string>? areas,
                                 IReadOnlyList<AreaNodeConfiguration>? children)
    {
        ArgumentNullException.ThrowIfNull(title);

        Title = title;
        Areas = areas ?? Array.Empty<string>();
        Children = children ?? Array.Empty<AreaNodeConfiguration>();
    }

    public string Title { get; }
    public IReadOnlyList<string> Areas { get; }
    public IReadOnlyList<AreaNodeConfiguration> Children { get; }
}
