using System.Collections.Concurrent;

using Terrajobst.GitHubEvents;

using ThemesOfDotNet.Indexing.AzureDevOps;
using ThemesOfDotNet.Indexing.GitHub;
using ThemesOfDotNet.Indexing.Ospo;
using ThemesOfDotNet.Indexing.Releases;
using ThemesOfDotNet.Indexing.Storage;
using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet.Services;

public sealed class WorkspaceService : IHostedService
{
    private readonly ILogger<WorkspaceService> _logger;
    private readonly WorkspaceCrawler _workspaceCrawler;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentQueue<WorkspaceMessage> _messages = new();
    private readonly AutoResetEvent _dataAvailable = new(false);
    private Task? _workerTask;

    public WorkspaceService(ILogger<WorkspaceService> logger,
                            IConfiguration configuration,
                            IWebHostEnvironment environment)
    {
        var gitHubAppId = configuration["GitHubAppId"];
        var gitHubAppPrivateKey = configuration["GitHubAppPrivateKey"];
        var azureDevOpsToken = configuration["AzureDevOpsToken"];
        var ospoToken = configuration["OspoToken"];

        var connectionString = configuration["BlobConnectionString"];
        var workspaceDataStore = (KeyValueStore) new AzureBlobStorageStore(connectionString, "cache");

        if (environment.IsDevelopment())
        {
            var directoryPath = Path.Join(Path.GetDirectoryName(Environment.ProcessPath), "cache");
            workspaceDataStore = new FallbackStore(new FileSystemStore(directoryPath), workspaceDataStore);
        }

        var workspaceDataCache = new WorkspaceDataCache(workspaceDataStore);

        var gitHubCrawler = new GitHubCrawler(gitHubAppId, gitHubAppPrivateKey, workspaceDataCache.GitHubCache);
        var azureDevOpsCrawler = new AzureDevOpsCrawler(azureDevOpsToken, workspaceDataCache.AzureDevOpsCache);
        var ospoCrawler = new OspoCrawler(ospoToken, workspaceDataCache.OspoCache);
        var releaseCrawler = new ReleaseCrawler(workspaceDataCache.ReleaseCache);

        _workspaceCrawler = new WorkspaceCrawler(workspaceDataCache, releaseCrawler, gitHubCrawler, azureDevOpsCrawler, ospoCrawler);
        _logger = logger;
    }

    public Workspace Workspace { get; private set; } = Workspace.Empty;

    public event EventHandler? WorkspaceChanged;

    public async Task InitializeAsync()
    {
        await _workspaceCrawler.LoadFromCacheAsync();

        UpdateWorkspace();
    }

    public void UpdateGitHub(GitHubEventMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Post(new UpdateGitHubWorkspaceMessage(message));
    }

    public void UpdateAzureDevOps()
    {
        Post(new UpdateAzureDevOpsWorkspaceMessage());
    }

    public void UpdateOspo()
    {
        Post(new UpdateOspoWorkspaceMessage());
    }

    private void UpdateWorkspace()
    {
        var snapshot = _workspaceCrawler.GetSnapshot();
        Workspace = Workspace.Create(snapshot);
        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _workerTask = Task.Run(async () =>
        {
            _logger.LogInformation("Workspace event processing started");
            try
            {
                await RunAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Workspace event processing was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in workspace event processing");
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        return _workerTask ?? Task.CompletedTask;
    }

    private void Post(WorkspaceMessage message)
    {
        _messages.Enqueue(message);
        _dataAvailable.Set();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var waitHandles = new[]
        {
            _dataAvailable,
            cancellationToken.WaitHandle
        };

        while (true)
        {
            WaitHandle.WaitAny(waitHandles);
            cancellationToken.ThrowIfCancellationRequested();

            while (_messages.TryDequeue(out var message))
            {
                _logger.LogInformation("Processing message {message}", message);
                try
                {
                    switch (message)
                    {
                        case UpdateGitHubWorkspaceMessage updateGitHub:
                        {
                            var handled = await _workspaceCrawler.UpdateGitHubAsync(updateGitHub.Message);
                            if (!handled)
                                _logger.LogInformation("Unhandled message {message}", message);
                            UpdateWorkspace();
                            break;
                        }
                        case UpdateAzureDevOpsWorkspaceMessage:
                        {
                            await _workspaceCrawler.UpdateAzureDevOpsAsync();
                            UpdateWorkspace();
                            break;
                        }
                        case UpdateOspoWorkspaceMessage:
                        {
                            await _workspaceCrawler.UpdateOspoAsync();
                            UpdateWorkspace();
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message {message}", message);
                }
            }
        }
    }

    private abstract class WorkspaceMessage
    {
        public abstract override string ToString();
    }

    private sealed class UpdateGitHubWorkspaceMessage : WorkspaceMessage
    {
        public UpdateGitHubWorkspaceMessage(GitHubEventMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            Message = message;
        }

        public GitHubEventMessage Message { get; }

        public override string ToString()
        {
            return Message.ToString();
        }
    }

    private sealed class UpdateAzureDevOpsWorkspaceMessage : WorkspaceMessage
    {
        public override string ToString()
        {
            return "UpdateAzureDevOps";
        }
    }

    private sealed class UpdateOspoWorkspaceMessage : WorkspaceMessage
    {
        public override string ToString()
        {
            return "UpdateOspo";
        }
    }
}
