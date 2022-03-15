
using ThemesOfDotNet.Indexing.AzureDevOps;

namespace ThemesOfDotNet.Indexing.GitHub;

public record struct GitHubWorkItemLink(GitHubIssueLinkType LinkType, AzureDevOpsWorkItemId LinkedId);
