using Humanizer;

namespace ThemesOfDotNet.Indexing.Validation.Rules;

internal sealed class VR05_OpenChildrenUnderClosedParentsShouldBeMoved : ValidationRule
{
    public override void Validate(ValidationContext context)
    {
        foreach (var workItem in context.Workspace.WorkItems)
        {
            if (workItem.IsOpen)
                continue;

            if (!workItem.Children.Any(c => c.IsOpen && !c.Parents.Any(p => p.IsOpen)))
                continue;

            var urlText = workItem.ToMarkdownLink();
            var kindText = workItem.Kind.Humanize();

            context.Report("VR05", $"{kindText} {urlText} is closed but has open children. Those children should be moved to a new {kindText}.", workItem);
        }
    }
}
