namespace ThemesOfDotNet.Indexing.Querying;

public abstract class Query
{
    protected Query(QueryContext context, string text)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(text);
        Context = context;
        Text = text;
    }

    public QueryContext Context { get; }

    public string Text { get; }

    public abstract IEnumerable<string> GetSyntaxHelp();

    [AttributeUsage(AttributeTargets.Method)]
    protected sealed class QueryHandlerAttribute : Attribute
    {
        public QueryHandlerAttribute(params string[] pairs)
        {
            Pairs = pairs;
        }

        public string[] Pairs { get; }
    }
}
