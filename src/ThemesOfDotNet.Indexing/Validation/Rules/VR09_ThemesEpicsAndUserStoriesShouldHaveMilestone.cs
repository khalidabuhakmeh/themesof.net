using Humanizer;
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Indexing.Validation.Rules;

internal sealed class VR09_ThemesEpicsAndUserStoriesShouldHaveMilestone : ValidationRule
{
    public override void Validate(ValidationContext context)
    {
        foreach (var workItem in context.Workspace.WorkItems)
        {
            if (workItem.IsClosed)
                continue;

            if (workItem.Milestone is not null)
                continue;

            var kind = workItem.Kind;

            if (kind != WorkItemKind.Theme &&
                kind != WorkItemKind.Epic &&
                kind != WorkItemKind.UserStory)
                continue;

            var urlText = workItem.ToMarkdownLink();
            var kindText = workItem.Kind.Humanize();

            context.Report("VR09", $"The open {kindText} {urlText} should have a milestone.", workItem);
        }
    }
}
