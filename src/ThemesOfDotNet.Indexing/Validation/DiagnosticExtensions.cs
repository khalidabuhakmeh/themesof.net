using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Indexing.Validation;

public static class DiagnosticExtensions
{
    public static void Report(this List<Diagnostic> bag,
                              string id,
                              string message,
                              WorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(workItem);

        bag.Report(id, message, new[] { workItem });
    }

    public static void Report(this List<Diagnostic> bag,
                              string id,
                              string message,
                              IReadOnlyList<WorkItem> workItems)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(workItems);

        bag.Add(new Diagnostic(id, message, workItems));
    }
}
