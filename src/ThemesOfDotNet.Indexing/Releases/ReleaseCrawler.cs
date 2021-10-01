using Microsoft.Deployment.DotNet.Releases;

namespace ThemesOfDotNet.Indexing.Releases;

public sealed class ReleaseCrawler
{
    private readonly ReleaseCache _cache;
    private readonly TimeZoneInfo _pst = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

    public ReleaseCrawler(ReleaseCache cache)
    {
        _cache = cache;
    }

    public async Task CrawlAsync()
    {
        var dotnetReleases = await CrawlDotnetReleasesAsync();
        var vsReleases = await CrawlVisualStudioReleasesAsync();

        var releases = new List<ReleaseInfo>(dotnetReleases.Count + vsReleases.Count);
        releases.AddRange(dotnetReleases);
        releases.AddRange(vsReleases);

        await _cache.StoreAsync(releases);
    }

    private async Task<IReadOnlyList<ReleaseInfo>> CrawlDotnetReleasesAsync()
    {
        Console.WriteLine("Crawling .NET releases...");

        try
        {
            var result = new List<ReleaseInfo>();

            foreach (var product in await ProductCollection.GetAsync())
            {
                foreach (var release in await product.GetReleasesAsync())
                {
                    var version = release.Version.ToString()
                                                 .Replace("preview.", "P", StringComparison.OrdinalIgnoreCase)
                                                 .Replace("preview", "P", StringComparison.OrdinalIgnoreCase)
                                                 .Replace("rc.", "RC", StringComparison.OrdinalIgnoreCase)
                                                 .Replace("rc", "RC", StringComparison.OrdinalIgnoreCase)
                                                 .Trim();
                    var date = TimeZoneInfo.ConvertTimeToUtc(release.ReleaseDate, _pst);
                    var info = new ReleaseInfo(".NET", version, date);
                    result.Add(info);
                }
            }

            return result.ToArray();
        }
        catch (Exception ex)
        {
            GitHubActions.Error("Can't crawl .NET releases:");
            GitHubActions.Error(ex);
            return Array.Empty<ReleaseInfo>();
        }
    }

    private async Task<IReadOnlyList<ReleaseInfo>> CrawlVisualStudioReleasesAsync()
    {
        Console.WriteLine("Crawling Visual Studio releases...");
        try
        {
            var result = new List<ReleaseInfo>();

            var url = "https://raw.githubusercontent.com/MicrosoftDocs/visualstudio-docs/master/docs/install/visual-studio-build-numbers-and-release-dates.md";
            var client = new HttpClient();
            using (var stream = await client.GetStreamAsync(url))
            using (var reader = new StreamReader(stream))
            {
                while (reader.ReadLine() is string line)
                {
                    var parts = line.Split('|', StringSplitOptions.TrimEntries |
                                                StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 4)
                    {
                        var version = parts[0];
                        var channel = parts[1];
                        var dateText = parts[2];

                        if (channel != "Release" && channel != "Preview")
                            version += " " + channel;

                        version = version.Replace(" Svc1", "", StringComparison.OrdinalIgnoreCase)
                                         .Replace(" (RC.4)", "", StringComparison.OrdinalIgnoreCase)
                                         .Replace(" (RC.3)", "", StringComparison.OrdinalIgnoreCase)
                                         .Replace(" (RC.2)", "", StringComparison.OrdinalIgnoreCase)
                                         .Replace(" (RC.1)", "", StringComparison.OrdinalIgnoreCase)
                                         .Replace(" (RC)", "", StringComparison.OrdinalIgnoreCase)
                                         .Replace("Release Candidate ", "RC", StringComparison.OrdinalIgnoreCase)
                                         .Replace("Release Candidate", "RC", StringComparison.OrdinalIgnoreCase)
                                         .Replace("Preview ", "P", StringComparison.OrdinalIgnoreCase)
                                         .Trim();

                        if (DateTime.TryParse(dateText, out var date))
                        {
                            date = TimeZoneInfo.ConvertTimeToUtc(date, _pst);

                            var info = new ReleaseInfo("VS", version, date);
                            result.Add(info);
                        }
                    }
                }
            }

            return result.ToArray();
        }
        catch (Exception ex)
        {
            GitHubActions.Error("Can't crawl VS releases:");
            GitHubActions.Error(ex);
            return Array.Empty<ReleaseInfo>();
        }
    }
}
