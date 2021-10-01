namespace ThemesOfDotNet.Indexing.Ospo;

public sealed class OspoGitHubInfo
{
    public OspoGitHubInfo(int id,
                          string login,
                          IReadOnlyList<string> organizations)
    {
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(organizations);

        Id = id;
        Login = login;
        Organizations = organizations;
    }

    public int Id { get; }

    public string Login { get; }

    public IReadOnlyList<string> Organizations { get; }
}
