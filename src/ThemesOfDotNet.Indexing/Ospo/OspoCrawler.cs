namespace ThemesOfDotNet.Indexing.Ospo;

public sealed class OspoCrawler
{
    private readonly string _token;
    private readonly OspoCache _cache;

    public OspoCrawler(string token, OspoCache cache)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(cache);

        _token = token;
        _cache = cache;
    }

    public async Task CrawlAsync(IEnumerable<string> gitHubUserNames,
                                 IEnumerable<string> microsoftAliases)
    {
        ArgumentNullException.ThrowIfNull(gitHubUserNames);
        ArgumentNullException.ThrowIfNull(microsoftAliases);

        var gitHubUserNameSet = gitHubUserNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var microsoftAliasSet = microsoftAliases.ToHashSet(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine("Fetching OSPO data...");

        try
        {
            using var client = new OspoClient(_token);
            var links = await client.GetAllAsync();

            var linksToBeCached = new List<OspoLink>();

            foreach (var link in links)
            {
                var shouldBeCached = gitHubUserNameSet.Contains(link.GitHubInfo.Login) ||
                                     microsoftAliasSet.Contains(link.MicrosoftInfo.Alias);
                if (shouldBeCached)
                    linksToBeCached.Add(link);
            }

            Console.WriteLine($"Caching {linksToBeCached.Count:N0} links...");
            await _cache.StoreAsync(linksToBeCached);
        }
        catch (Exception ex)
        {
            GitHubActions.Error("Can't download OSPO information:");
            GitHubActions.Error(ex);
        }
    }
}
