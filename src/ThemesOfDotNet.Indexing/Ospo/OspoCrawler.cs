namespace ThemesOfDotNet.Indexing.Ospo;

public sealed class OspoCrawler
{
    private readonly string _token;
    private readonly OspoCache _cache;

    private readonly List<OspoLink> _links = new();

    public OspoCrawler(string token, OspoCache cache)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(cache);

        _token = token;
        _cache = cache;
    }

    public void LoadFromCache(IReadOnlyList<OspoLink> links)
    {
        ArgumentNullException.ThrowIfNull(links);

        _links.Clear();
        _links.AddRange(links);
    }

    public async Task CrawlAsync()
    {
        await UpdateAsync();
    }

    public async Task SaveAsync()
    {
        try
        {
            Console.WriteLine($"Caching {_links.Count:N0} links...");
            await _cache.StoreAsync(_links);
        }
        catch (Exception ex)
        {
            GitHubActions.Error("Can't download OSPO information:");
            GitHubActions.Error(ex);
        }
    }

    public async Task UpdateAsync()
    {
        Console.WriteLine("Crawling OSPO data...");
        try
        {
            using var client = new OspoClient(_token);
            var links = await client.GetAllAsync();

            var linksToBeCached = new List<OspoLink>();
            var seenGitHubUserNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var link in links)
            {
                // Alias should never be null, but the data we get does contain null values.
                if (link.MicrosoftInfo.Alias is null)
                    continue;

                // If we have either seen the GitHub login or the Microsoft alias, we
                // skip this entry.
                //
                // While the data shouldn't contain duplicates, it does.
                if (!seenGitHubUserNames.Add(link.GitHubInfo.Login) ||
                    !seenAliases.Add(link.MicrosoftInfo.Alias))
                    continue;

                linksToBeCached.Add(link);
            }

            _links.Clear();
            _links.AddRange(linksToBeCached);
        }
        catch (Exception ex)
        {
            GitHubActions.Error("Can't download OSPO information:");
            GitHubActions.Error(ex);
        }
    }

    public void GetSnapshot(out IReadOnlyList<OspoLink> links)
    {
        links = _links.ToArray();
    }
}
