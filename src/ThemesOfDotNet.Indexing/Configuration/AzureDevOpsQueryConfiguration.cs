namespace ThemesOfDotNet.Indexing.Configuration;

public sealed class AzureDevOpsQueryConfiguration
{
    public AzureDevOpsQueryConfiguration(string url,
                                         string htmlUrl,
                                         string queryId,
                                         string themeTitle,
                                         IReadOnlyList<string>? themeAssignees,
                                         string area,
                                         string? defaultProduct,
                                         IReadOnlyList<string> mappedProducts)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(htmlUrl);
        ArgumentNullException.ThrowIfNull(queryId);
        ArgumentNullException.ThrowIfNull(themeTitle);
        ArgumentNullException.ThrowIfNull(area);

        Url = url;
        HtmlUrl = htmlUrl;
        QueryId = queryId;
        ThemeTitle = themeTitle;
        ThemeAssignees = themeAssignees ?? Array.Empty<string>();
        Area = area;
        DefaultProduct = defaultProduct;
        MappedProducts = mappedProducts ?? Array.Empty<string>();
    }

    public string Url { get; }
    public string HtmlUrl { get; }
    public string QueryId { get; }
    public string ThemeTitle { get; }
    public IReadOnlyList<string> ThemeAssignees { get; set; }
    public string Area { get; }
    public string? DefaultProduct { get; }
    public IReadOnlyList<string> MappedProducts { get; }
}
