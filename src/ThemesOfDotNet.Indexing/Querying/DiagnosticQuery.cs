using ThemesOfDotNet.Indexing.Validation;

namespace ThemesOfDotNet.Indexing.Querying;

public sealed class DiagnosticQuery : Query<Diagnostic>
{
    private static readonly QueryHandlers _handlers = CreateHandlers(typeof(DiagnosticQuery));

    public static DiagnosticQuery Empty { get; } = new DiagnosticQuery(QueryContext.Empty, string.Empty);

    public DiagnosticQuery(QueryContext context, string text)
        : base(context, text)
    {
    }

    protected override QueryHandlers GetHandlers()
    {
        return _handlers;
    }

    protected override bool ContainsText(Diagnostic value, string text)
    {
        return value.Message.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    [QueryHandler("code")]
    private static bool HasCode(Diagnostic diagnostic, string value)
    {
        return string.Equals(diagnostic.Id, value, StringComparison.OrdinalIgnoreCase);
    }

    [QueryHandler("assignee")]
    private static bool HasAssignee(Diagnostic diagnostic, string value)
    {
        return diagnostic.Assignees.Any(u => u.Matches(value));
    }
}
