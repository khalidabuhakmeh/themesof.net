namespace ThemesOfDotNet.Services;

public sealed class OspoCrawlerService : TimerService
{
    private readonly WorkspaceService _workspaceService;

    public OspoCrawlerService(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    protected override TimeSpan RefreshInterval => TimeSpan.FromHours(2);

    protected override Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    protected override Task RefreshAsync()
    {
        _workspaceService.UpdateOspo();
        return Task.CompletedTask;
    }
}
