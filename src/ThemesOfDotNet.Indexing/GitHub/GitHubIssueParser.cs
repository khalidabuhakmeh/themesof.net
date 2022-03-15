using System.Text.RegularExpressions;

using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Parsers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using ThemesOfDotNet.Indexing.AzureDevOps;

namespace ThemesOfDotNet.Indexing.GitHub;

public static class GitHubIssueParser
{
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseTaskLists()
        .UseAutoLinks()
        .Build();

    public static GitHubIssueLinkage ParseLinkage(GitHubRepoId repoId, string? markdown)
    {
        var document = MarkdownParser.Parse(markdown ?? string.Empty, _pipeline);

        var parentLinks = document.Descendants<LinkInline>()
                                  .Where(l => !l.ContainsParentOfType<TaskList>())
                                  .Where(l => (l.FirstChild?.ToString()?.Trim() ?? string.Empty).StartsWith("Parent"))
                                  .ToArray();

        var issueLinks = new List<GitHubIssueLink>();
        var workItemLinks = new List<GitHubWorkItemLink>();
        var queryIds = new List<AzureDevOpsQueryId>();

        foreach (var parentLink in parentLinks)
        {
            if (ProcessParentUrl(parentLink.Url))
                break;
        }

        var taskListItems = document.Descendants<TaskList>()
                                    .Select(t => t.Parent)
                                    .Where(t => t is not null)
                                    .Select(t => t!);

        foreach (var taskListItem in taskListItems)
        {
            var links = taskListItem.Descendants<LinkInline>();

            var found = false;

            foreach (var link in links)
            {
                if (ProcessChildUrl(link.Url))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                var autoLinks = taskListItem.Descendants<AutolinkInline>();

                foreach (var autoLink in autoLinks)
                {
                    if (ProcessChildUrl(autoLink.Url))
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                var literalInlines = taskListItem.Descendants<LiteralInline>()
                                                 .Where(i => !i.ContainsParentOfType<LinkInline>());

                foreach (var literalInline in literalInlines)
                {
                    if (found)
                        break;

                    foreach (Match match in Regex.Matches(literalInline.Content.ToString(), @"((?<owner>[a-zA-Z0-9._-]+)/(?<repo>[a-zA-Z0-9._-]+))?#(?<number>[0-9]+)"))
                    {
                        var linkOwner = match.Groups["owner"].Value;
                        var linkRepo = match.Groups["repo"].Value;
                        var numberText = match.Groups["number"].Value;

                        if (string.IsNullOrEmpty(linkOwner))
                        {
                            linkOwner = repoId.Owner;
                            linkRepo = repoId.Name;
                        }

                        if (int.TryParse(numberText, out var number))
                        {
                            var issueId = new GitHubIssueId(linkOwner, linkRepo, number);
                            issueLinks.Add(new GitHubIssueLink(GitHubIssueLinkType.Child, issueId));
                            found = true;
                            break;
                        }
                    }
                }
            }
        }

        return new GitHubIssueLinkage(issueLinks.ToArray(),
                                      workItemLinks.ToArray(),
                                      queryIds.ToArray());

        bool ProcessParentUrl(string? url)
        {
            if (url is not null)
            {
                if (GitHubIssueId.TryParse(url, out var issueId))
                {
                    issueLinks.Add(new GitHubIssueLink(GitHubIssueLinkType.Parent, issueId));
                    return true;
                }
                else if (AzureDevOpsWorkItemId.TryParse(url, out var workItemId))
                {
                    workItemLinks.Add(new GitHubWorkItemLink(GitHubIssueLinkType.Parent, workItemId));
                    return true;
                }
            }

            return false;
        }

        bool ProcessChildUrl(string? url)
        {
            if (url is not null)
            {
                if (GitHubIssueId.TryParse(url, out var issueId))
                {
                    issueLinks.Add(new GitHubIssueLink(GitHubIssueLinkType.Child, issueId));
                    return true;
                }
                else if (AzureDevOpsWorkItemId.TryParse(url, out var workItemId))
                {
                    workItemLinks.Add(new GitHubWorkItemLink(GitHubIssueLinkType.Child, workItemId));
                    return true;
                }
                else if (AzureDevOpsQueryId.TryParse(url, out var queryId))
                {
                    queryIds.Add(queryId);
                    return true;
                }
            }

            return false;
        }
    }
}
