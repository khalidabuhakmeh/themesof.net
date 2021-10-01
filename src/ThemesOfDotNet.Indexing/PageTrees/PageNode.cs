namespace ThemesOfDotNet.Indexing.PageTrees;

public sealed class PageNode
{
    public PageNode(object data,
                    IEnumerable<PageNode> children,
                    bool isExcluded = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(children);

        Data = data;
        Children = children.ToArray();
        IsExcluded = isExcluded;
        IsMuted = isExcluded;
        IsVisible = true;
    }

    public object Data { get; }

    public IReadOnlyList<PageNode> Children { get; }

    public bool IsExcluded { get; }

    public bool IsMuted { get; set; }

    public bool IsVisible { get; set; }

    public bool IsExpanded { get; set; }
}
