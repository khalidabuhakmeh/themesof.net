using System.Collections.Concurrent;

using Terrajobst.GitHubEvents;

namespace ThemesOfDotNet.Services;

public sealed class GitHubEventProcessingService : IGitHubEventProcessor, IHostedService
{
    private readonly ILogger<GitHubEventProcessingService> _logger;
    private readonly WorkspaceService _workspaceService;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentQueue<GitHubEventMessage> _messages = new();
    private readonly AutoResetEvent _dataAvailable = new(false);

    private Task? _workerTask;

    public GitHubEventProcessingService(ILogger<GitHubEventProcessingService> logger,
                                        WorkspaceService workspaceService)
    {
        _logger = logger;
        _workspaceService = workspaceService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _workerTask = Task.Run(async () =>
        {
            _logger.LogInformation("GitHub event processing started");
            try
            {
                await RunAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("GitHub event processing was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GitHub event processing");
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        return _workerTask ?? Task.CompletedTask;
    }

    public void Process(GitHubEventMessage message)
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
                await _workspaceService.UpdateGitHubAsync(message);
        }
    }
}
