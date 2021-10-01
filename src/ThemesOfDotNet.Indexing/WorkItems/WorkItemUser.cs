namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed class WorkItemUser
{
    internal WorkItemUser(Workspace workspace,
                          string displayName,
                          string? gitHubLogin,
                          string? microsoftAlias)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(displayName);

        Workspace = workspace;
        DisplayName = displayName;
        GitHubLogin = gitHubLogin;
        MicrosoftAlias = microsoftAlias;
    }

    public Workspace Workspace { get; }

    public string DisplayName { get; }

    public string? GitHubLogin { get; }

    public string? MicrosoftAlias { get; }

    public bool Matches(string value)
    {
        return string.Equals(DisplayName, value, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(GitHubLogin, value, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(MicrosoftAlias, value, StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString()
    {
        return DisplayName;
    }
}
