using Humanizer;

using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Indexing.Validation.Rules;

internal sealed class VR02_ThemesEpicsAndUserStoriesShouldBeAssigned : ValidationRule
{
    public override void Validate(ValidationContext context)
    {
        foreach (var workItem in context.Workspace.WorkItems)
        {
            if (workItem.IsClosed)
                continue;

            var kind = workItem.Kind;

            if (kind != WorkItemKind.Theme &&
                kind != WorkItemKind.Epic &&
                kind != WorkItemKind.UserStory)
                continue;

            if (workItem.Assignees.Any())
                continue;

            var urlText = workItem.ToMarkdownLink();
            var kindText = kind.Humanize();

            context.Report("VR02", $"{kindText} {urlText} should be assigned", workItem);
        }
    }
}
