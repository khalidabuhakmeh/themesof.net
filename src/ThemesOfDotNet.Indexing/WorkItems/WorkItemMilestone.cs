namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed class WorkItemMilestone
{
    internal WorkItemMilestone(WorkItemProduct product,
                               WorkItemVersion version,
                               DateTimeOffset? releaseDate)
    {
        ArgumentNullException.ThrowIfNull(product);

        Product = product;
        Version = version;
        ReleaseDate = releaseDate;
    }

    public Workspace Workspace => Product.Workspace;

    public WorkItemProduct Product { get; }

    public WorkItemVersion Version { get; }

    public DateTimeOffset? ReleaseDate { get; }

    public override string ToString()
    {
        return $"{Product} {Version}";
    }
}
