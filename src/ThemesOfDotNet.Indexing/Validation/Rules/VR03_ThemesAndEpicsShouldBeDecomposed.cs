using Humanizer;

using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Indexing.Validation.Rules;

internal sealed class VR03_ThemesAndEpicsShouldBeDecomposed : ValidationRule
{
    public override void Validate(ValidationContext context)
    {
        foreach (var workItem in context.Workspace.WorkItems)
        {
            if (workItem.IsClosed)
                continue;

            var kind = workItem.Kind;

            if (kind != WorkItemKind.Theme &&
                kind != WorkItemKind.Epic)
                continue;

            if (workItem.IsBottomUp)
                continue;

            if (workItem.Children.Any())
                continue;

            var urlText = workItem.ToMarkdownLink();
            var kindText = kind.Humanize();

            var smallerKind = kind == WorkItemKind.Theme ? WorkItemKind.Epic : WorkItemKind.UserStory;
            var smallerKindText = smallerKind.Humanize(LetterCasing.LowerCase).Pluralize();

            context.Report("VR03", $"{kindText} {urlText} should be decomposed into {smallerKindText}", workItem);
        }
    }
}
