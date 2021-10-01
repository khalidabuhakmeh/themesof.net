using System.Text.Json.Serialization;

namespace ThemesOfDotNet.Indexing.Ospo;

public sealed class OspoLink
{
    public OspoLink(OspoGitHubInfo gitHubInfo,
                    OspoMicrosoftInfo microsoftInfo)
    {
        ArgumentNullException.ThrowIfNull(gitHubInfo);
        ArgumentNullException.ThrowIfNull(microsoftInfo);

        GitHubInfo = gitHubInfo;
        MicrosoftInfo = microsoftInfo;
    }

    [JsonPropertyName("github")]
    public OspoGitHubInfo GitHubInfo { get; }

    [JsonPropertyName("aad")]
    public OspoMicrosoftInfo MicrosoftInfo { get; }
}
