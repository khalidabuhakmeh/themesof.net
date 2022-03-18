using System.Diagnostics;
using System.Text.Json;

using Microsoft.Extensions.Configuration;

using Mono.Options;

using ThemesOfDotNet.Indexing;
using ThemesOfDotNet.Indexing.AzureDevOps;
using ThemesOfDotNet.Indexing.Configuration;
using ThemesOfDotNet.Indexing.GitHub;
using ThemesOfDotNet.Indexing.Ospo;
using ThemesOfDotNet.Indexing.Releases;
using ThemesOfDotNet.Indexing.Storage;
using ThemesOfDotNet.Indexing.WorkItems;

try
{
    return await RunAsync(args);
}
catch (Exception ex) when (!Debugger.IsAttached)
{
    GitHubActions.Error("Unhandled exception:");
    Console.WriteException(ex);
    return 1;
}

static async Task<int> RunAsync(string[] args)
{
    var exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath);
    var help = false;
    var validateOnly = false;
    var subscriptionJsonPath = (string?)null;

    var options = new OptionSet
    {
        $"usage: {exeName} <path-to-subscription-json> [OPTIONS]+",
        { "v|validate-only", "Instead of crawling just validates the configuration", v => validateOnly = true },
        { "h|?|help", null, v => help = true, true }
    };

    try
    {
        var parameters = options.Parse(args).ToArray();

        if (help)
        {
            options.WriteOptionDescriptions(System.Console.Error);
            return 0;
        }

        if (parameters.Length >= 1)
        {
            subscriptionJsonPath = parameters[0];
        }
        else
        {
            Console.MarkupLine("[red]error: need provide path to subscriptions.json[/]");
            return 1;
        }

        var unprocessed = parameters.Skip(1);

        if (unprocessed.Any())
        {
            foreach (var option in unprocessed)
                Console.MarkupLine($"[red]error: unrecognized argument {option}[/]");
            return 1;
        }
    }
    catch (Exception ex)
    {
        Console.WriteException(ex);
        return 1;
    }

    try
    {
        var subscriptionConfiguration = await SubscriptionConfiguration.LoadAsync(subscriptionJsonPath);

        if (!validateOnly)
            return await CrawlAsync(subscriptionConfiguration);
    }
    catch (JsonException ex)
    {
        if (GitHubActions.IsRunningInside)
        {
            Console.WriteLine($"LineNumber: {ex.LineNumber}");
            GitHubActions.Error(ex.Message, subscriptionJsonPath, (int?)ex.LineNumber + 1);
        }
        else
        {
            var fileName = Path.GetFileName(subscriptionJsonPath);
            Console.MarkupLine($"[red]error: {fileName} is malformed on line {ex.LineNumber}:[/]");
            Console.WriteLine(ex.Message);
        }
        return 1;
    }

    return GitHubActions.SeenErrors ? 1 : 0;
}

static async Task<int> CrawlAsync(SubscriptionConfiguration subscriptionConfiguration)
{
    var config = new ConfigurationBuilder()
        .AddUserSecrets<Program>(optional: true)
        .AddEnvironmentVariables()
        .Build();

    var gitHubAppId = GetRequiredValue(config, "GitHubAppId");
    var gitHubAppPrivateKey = GetRequiredValue(config, "GitHubAppPrivateKey");
    var azureDevOpsToken = GetRequiredValue(config, "AzureDevOpsToken");
    var ospoToken = GetRequiredValue(config, "OspoToken");
    var blobConnectionString = GetRequiredValue(config, "BlobConnectionString");

    if (gitHubAppId is null || gitHubAppPrivateKey is null || azureDevOpsToken is null || ospoToken is null || blobConnectionString is null)
        return 1;

    var workspaceDataCache = new WorkspaceDataCache(new AzureBlobStorageStore(blobConnectionString, "cache"));
    var releaseCache = workspaceDataCache.ReleaseCache;
    var configurationCache = workspaceDataCache.ConfigurationCache;
    var gitHubCache = workspaceDataCache.GitHubCache;
    var azureDevOpsCache = workspaceDataCache.AzureDevOpsCache;
    var ospoCache = workspaceDataCache.OspoCache;

    await configurationCache.StoreAsync(subscriptionConfiguration);

    var releaseCrawler = new ReleaseCrawler(releaseCache);
    var gitHubCrawler = new GitHubCrawler(gitHubAppId, gitHubAppPrivateKey, gitHubCache);
    var azureDevOpsCrawler = new AzureDevOpsCrawler(azureDevOpsToken, azureDevOpsCache);
    var ospoCrawler = new OspoCrawler(ospoToken, ospoCache);

    var workspaceCrawler = new WorkspaceCrawler(workspaceDataCache, releaseCrawler, gitHubCrawler, azureDevOpsCrawler, ospoCrawler);
    await workspaceCrawler.CrawlAsync(subscriptionConfiguration);

    return GitHubActions.SeenErrors ? 1 : 0;
}

static string? GetRequiredValue(IConfiguration configuration, string key)
{
    var result = configuration[key];
    if (!string.IsNullOrEmpty(result))
        return result;

    GitHubActions.Error($"required environment variable '{key}' isn't configured");
    return null;
}
