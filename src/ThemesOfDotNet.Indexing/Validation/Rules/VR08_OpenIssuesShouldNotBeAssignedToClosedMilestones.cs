namespace ThemesOfDotNet.Indexing.Validation.Rules;

internal sealed class VR08_OpenIssuesShouldNotBeAssignedToClosedMilestones : ValidationRule
{
    public override void Validate(ValidationContext context)
    {
        foreach (var workItem in context.Workspace.WorkItems)
        {
            if (workItem.IsClosed)
                continue;

            if (workItem.Milestone?.ReleaseDate is null)
                continue;

            var urlText = workItem.ToMarkdownLink();
            var milestone = workItem.Milestone.ToString();

            context.Report("VR08", $"The issue {urlText} is open but assigned to the completed milestone {milestone}. It should either be closed or moved to a different milestone.", workItem);
        }
    }
}
