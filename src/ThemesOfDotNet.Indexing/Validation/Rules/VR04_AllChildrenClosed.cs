using Humanizer;

namespace ThemesOfDotNet.Indexing.Validation.Rules;

internal sealed class VR04_AllChildrenClosed : ValidationRule
{
    public override void Validate(ValidationContext context)
    {
        foreach (var workItem in context.Workspace.WorkItems)
        {
            if (workItem.IsClosed)
                continue;

            var allChildrenAreClosed = workItem.Children.Any() &&
                                       workItem.Children.All(c => c.IsClosed);
            if (!allChildrenAreClosed)
                continue;

            var urlText = workItem.ToMarkdownLink();
            var kindText = workItem.Kind.Humanize();

            context.Report("VR04", $"{kindText} {urlText} is still open but all its children are already closed", workItem);
        }
    }
}
