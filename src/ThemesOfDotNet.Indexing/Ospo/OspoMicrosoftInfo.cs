namespace ThemesOfDotNet.Indexing.Ospo;

public sealed class OspoMicrosoftInfo
{
    public OspoMicrosoftInfo(string alias,
                             string preferredName,
                             string? userPrincipalName,
                             string? emailAddress,
                             string id)
    {
        ArgumentNullException.ThrowIfNull(preferredName);
        ArgumentNullException.ThrowIfNull(id);

        Alias = alias;
        PreferredName = preferredName;
        UserPrincipalName = userPrincipalName;
        EmailAddress = emailAddress;
        Id = id;
    }

    public string Alias { get; }

    public string PreferredName { get; }

    public string? UserPrincipalName { get; }

    public string? EmailAddress { get; }

    public string Id { get; }
}
