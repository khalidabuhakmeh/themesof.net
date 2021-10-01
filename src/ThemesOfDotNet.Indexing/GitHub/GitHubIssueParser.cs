using System.Text.RegularExpressions;

using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Parsers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ThemesOfDotNet.Indexing.GitHub;

public static class GitHubIssueParser
{
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseTaskLists()
        .UseAutoLinks()
        .Build();

    public static IEnumerable<GitHubIssueLink> ParseLinks(GitHubRepoId repoId, string? markdown)
    {
        var document = MarkdownParser.Parse(markdown ?? string.Empty, _pipeline);

        var parentLinks = document.Descendants<LinkInline>()
                                  .Where(l => !l.ContainsParentOfType<TaskList>())
                                  .Where(l => (l.FirstChild?.ToString()?.Trim() ?? string.Empty).StartsWith("Parent"))
                                  .ToArray();

        foreach (var parentLink in parentLinks)
        {
            if (GitHubIssueId.TryParse(parentLink.Url, out var id))
            {
                yield return new(GitHubIssueLinkType.Parent, id);
                break;
            }
        }

        var taskLinkItems = document.Descendants<TaskList>()
                                    .Select(t => t.Parent)
                                    .Where(t => t is not null)
                                    .Select(t => t!);

        foreach (var taskListItem in taskLinkItems)
        {
            var links = taskListItem.Descendants<LinkInline>();

            GitHubIssueId? id = null;

            foreach (var link in links)
            {
                if (GitHubIssueId.TryParse(link.Url, out var i))
                {
                    id = i;
                    break;
                }
            }

            if (id == null)
            {
                var autoLinks = taskListItem.Descendants<AutolinkInline>();

                foreach (var autoLink in autoLinks)
                {
                    if (GitHubIssueId.TryParse(autoLink.Url, out var i))
                    {
                        id = i;
                        break;
                    }
                }
            }

            if (id == null)
            {
                var literalInlines = taskListItem.Descendants<LiteralInline>()
                                                 .Where(i => !i.ContainsParentOfType<LinkInline>());

                foreach (var literalInline in literalInlines)
                {
                    if (id != null)
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
                            id = new GitHubIssueId(linkOwner, linkRepo, number);
                            break;
                        }
                    }
                }
            }

            if (id != null)
                yield return new(GitHubIssueLinkType.Child, id.Value);
        }
    }
}
