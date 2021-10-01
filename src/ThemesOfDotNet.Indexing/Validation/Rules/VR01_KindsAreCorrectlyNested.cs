using Humanizer;

namespace ThemesOfDotNet.Indexing.Validation.Rules;

internal sealed class VR01_KindsAreCorrectlyNested : ValidationRule
{
    public override void Validate(ValidationContext context)
    {
        foreach (var parent in context.Workspace.WorkItems)
        {
            foreach (var child in parent.Children)
            {
                var parentKind = parent.Kind;
                var childKind = child.Kind;
                var isValid = parentKind <= childKind;
                if (isValid)
                    continue;

                var parentUrlText = parent.ToMarkdownLink();
                var childUrlText = child.ToMarkdownLink();

                context.Report(
                    id: "VR01",
                    title: $"{childUrlText} shouldn't be nested under {parentUrlText} because {childKind.Humanize(LetterCasing.LowerCase).Pluralize()} shouldn't be nested under {parentKind.Humanize(LetterCasing.LowerCase).Pluralize()}",
                    workItems: new[] { parent, child }
                );
            }
        }
    }
}
