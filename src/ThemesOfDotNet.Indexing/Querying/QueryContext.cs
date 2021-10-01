namespace ThemesOfDotNet.Indexing.Querying;

public sealed class QueryContext
{
    public static QueryContext Empty { get; } = new QueryContext();

    public QueryContext(string? userName = null)
    {
        UserName = userName;
    }

    public string? UserName { get; }
}
