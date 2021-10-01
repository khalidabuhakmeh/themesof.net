namespace ThemesOfDotNet.Indexing;

internal static class Constants
{
    public const string LabelTheme = "Theme";
    public const string LabelEpic = "Epic";
    public const string LabelUserStory = "User Story";
    public static readonly IReadOnlyList<string> LabelsForThemesEpicsAndUserStories = new[] { LabelTheme, LabelEpic, LabelUserStory };
    public const string LabelBottomUpWork = "Bottom Up Work";
    public const string LabelContinuousImprovement = "Continuous Improvement";

    public const string LabelPriority = "Priority";
    public const string LabelCost = "Cost";
    public const string LabelTeam = "Team";
    public const string LabelStatus = "Status";
}
