using System.Collections.Concurrent;

using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Indexing.Validation;

public sealed class ValidationContext
{
    private readonly ConcurrentBag<Diagnostic> _diagnostics = new();

    public ValidationContext(Workspace workspace)
    {
        Workspace = workspace;
    }

    public Workspace Workspace { get; }

    public void Report(string id, string title, WorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(workItem);

        Report(id, title, new[] { workItem });
    }

    public void Report(string id, string title, IReadOnlyList<WorkItem> workItems)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(workItems);

        var diagnostic = new Diagnostic(
            id,
            title,
            workItems
        );
        _diagnostics.Add(diagnostic);
    }

    public IReadOnlyList<Diagnostic> GetDiagnostics() => _diagnostics.ToArray();
}
