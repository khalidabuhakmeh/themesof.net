using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Indexing.Validation;

internal static class ValidationEngine
{
    public static IReadOnlyList<Diagnostic> Run(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var rules = GetRules();
        return Run(workspace, rules);
    }

    public static IReadOnlyList<Diagnostic> Run(Workspace workspace, IEnumerable<ValidationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(rules);

        var context = new ValidationContext(workspace);

        foreach (var rule in rules)
            rule.Validate(context);

        return workspace.ConstructionDiagnostics.Concat(context.GetDiagnostics())
                                    .OrderBy(d => d.Id)
                                    .ThenBy(d => d.Message)
                                    .ToArray();
    }

    private static IEnumerable<ValidationRule> GetRules()
    {
        return typeof(ValidationEngine).Assembly
                                       .GetTypes()
                                       .Where(t => !t.IsAbstract && typeof(ValidationRule).IsAssignableFrom(t))
                                       .Select(t => (ValidationRule?)Activator.CreateInstance(t))
                                       .Where(r => r is not null)
                                       .Select(r => r!)
                                       .ToArray();
    }
}


