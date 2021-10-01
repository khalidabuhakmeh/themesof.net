using GitHubJwt;

using Octokit;

using Spectre.Console;

namespace ThemesOfDotNet.Indexing.GitHub;

internal sealed class GitHubAppClient : GitHubClient
{
    private readonly int _appId;
    private readonly string _privateKey;

    public GitHubAppClient(ProductHeaderValue productInformation, string appId, string privateKey)
        : base(productInformation)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(privateKey);

        _appId = Convert.ToInt32(appId);
        _privateKey = privateKey;
    }

    public async Task GenerateTokenAsync()
    {
        // See: https://octokitnet.readthedocs.io/en/latest/github-apps/ for details.

        var privateKeySource = new PlainStringPrivateKeySource(_privateKey);
        var generator = new GitHubJwtFactory(
            privateKeySource,
            new GitHubJwtFactoryOptions
            {
                AppIntegrationId = _appId,
                ExpirationSeconds = 8 * 60 // 600 is apparently too high
            });

        var token = generator.CreateEncodedJwtToken();

        Credentials = new Credentials(token, AuthenticationType.Bearer);

        var installations = await GitHubApps.GetAllInstallationsForCurrent();
        var installation = installations.First();
        var installationTokenResult = await GitHubApps.CreateInstallationToken(installation.Id);

        Credentials = new Credentials(installationTokenResult.Token, AuthenticationType.Oauth);
    }

    public Task InvokeAsync(Func<GitHubClient, Task> operation)
    {
        return InvokeAsync<object?>(async c =>
        {
            await operation(c);
            return null;
        });
    }

    public async Task<T> InvokeAsync<T>(Func<GitHubClient, Task<T>> operation)
    {
        if (Credentials is null || Credentials.AuthenticationType == AuthenticationType.Anonymous)
        {
            AnsiConsole.MarkupLine($"[yellow]Acquiring GitHub app token...[/]");
            await GenerateTokenAsync();
        }

        var remainingRetries = 3;

        while (true)
        {
            try
            {
                return await operation(this);
            }
            catch (RateLimitExceededException ex) when (remainingRetries > 0)
            {
                var delay = ex.GetRetryAfterTimeSpan()
                            .Add(TimeSpan.FromSeconds(15)); // Add some buffer
                var until = DateTime.Now.Add(delay);

                AnsiConsole.MarkupLine($"[yellow]Rate limit exceeded.[/] Waiting for {delay.TotalMinutes:N1} mins until {until}.");
                await Task.Delay(delay);
            }
            catch (AuthorizationException ex) when (remainingRetries > 0)
            {
                AnsiConsole.MarkupLine($"[red]Authorization error: {ex.Message}.[/] Refreshing token...");
                await GenerateTokenAsync();
            }
            catch (OperationCanceledException) when (remainingRetries > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Operation canceled.[/] Assuming this means a token refresh is needed...");
                await GenerateTokenAsync();
            }
        }
    }

    public sealed class PlainStringPrivateKeySource : IPrivateKeySource
    {
        private readonly string _key;

        public PlainStringPrivateKeySource(string key)
        {
            _key = key;
        }

        public TextReader GetPrivateKeyReader()
        {
            return new StringReader(_key);
        }
    }
}
