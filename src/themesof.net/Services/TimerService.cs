namespace ThemesOfDotNet.Services;

public abstract class TimerService : IHostedService, IDisposable
{
    private Timer? _timer;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync();
        var interval = RefreshInterval;
        _timer = new Timer(Refresh, null, interval, interval);
    }

    private async void Refresh(object? state)
    {
        await Task.Run(async () => await RefreshAsync());
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    protected abstract TimeSpan RefreshInterval { get; }

    protected virtual Task InitializeAsync()
    {
        return RefreshAsync();
    }

    protected abstract Task RefreshAsync();

    public void Dispose()
    {
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
