using Humanizer;

using ThemesOfDotNet.Indexing.GitHub;

namespace ThemesOfDotNet.Indexing.Validation.Rules;

internal sealed class VR06_CompletedIssuesAreClosed : ValidationRule
{
    public override void Validate(ValidationContext context)
    {
        foreach (var workItem in context.Workspace.WorkItems)
        {
            if (workItem.Original is not GitHubIssue issue)
                continue;

            var isConsistent = workItem.IsOpen == issue.IsOpen;
            if (isConsistent)
                continue;

            var urlText = workItem.ToMarkdownLink();
            var statusText = workItem.State.Humanize();

            if (issue.IsOpen)
                context.Report("VR06", $"The issue {urlText} is marked as {statusText}; it should be closed.", workItem);
            else
                context.Report("VR06", $"The issue {urlText} is closed. It should be be marked as 'Completed' or 'Cut'.", workItem);
        }
    }
}
