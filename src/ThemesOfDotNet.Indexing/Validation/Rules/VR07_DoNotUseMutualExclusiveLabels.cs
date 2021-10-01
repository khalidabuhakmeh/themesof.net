using Humanizer;

using ThemesOfDotNet.Indexing.GitHub;
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Indexing.Validation.Rules;

internal sealed class VR07_DoNotUseMutualExclusiveLabels : ValidationRule
{
    public override void Validate(ValidationContext context)
    {
        foreach (var workItem in context.Workspace.WorkItems)
        {
            if (workItem.IsClosed)
                continue;

            if (workItem.Original is not GitHubIssue issue)
                continue;

            CheckForMultipleLabelsOf(context, workItem, issue, Constants.LabelsForThemesEpicsAndUserStories);
            CheckForMultipleLabelsWithPrefix(context, workItem, issue, Constants.LabelStatus + ":");
            CheckForMultipleLabelsWithPrefix(context, workItem, issue, Constants.LabelPriority + ":");
            CheckForMultipleLabelsWithPrefix(context, workItem, issue, Constants.LabelCost + ":");
        }
    }

    private static void CheckForMultipleLabelsOf(ValidationContext context, WorkItem workItem, GitHubIssue issue, IEnumerable<string> labelNames)
    {
        if (HasMultipleLabels(issue, l => labelNames.Contains(l.Name, StringComparer.OrdinalIgnoreCase)))
        {
            var urlText = workItem.ToMarkdownLink();
            var kindText = workItem.Kind.Humanize();

            context.Report("VR07", $"{kindText} {urlText} should have at most one of these labels: {string.Join(", ", labelNames)}", workItem);
        }
    }

    private static void CheckForMultipleLabelsWithPrefix(ValidationContext context, WorkItem workItem, GitHubIssue issue, string prefix)
    {
        if (HasMultipleLabels(issue, l => l.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var urlText = workItem.ToMarkdownLink();
            var kindText = workItem.Kind.Humanize();

            context.Report("VR07", $"{kindText} {urlText} should have at most one label with the prefix '{prefix}'", workItem);
        }
    }

    private static bool HasMultipleLabels(GitHubIssue issue, Func<GitHubLabel, bool> predicate)
    {
        return issue.Labels.Count(predicate) > 1;
    }
}
