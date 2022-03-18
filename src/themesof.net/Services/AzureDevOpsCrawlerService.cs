namespace ThemesOfDotNet.Services;

public sealed class AzureDevOpsCrawlerService : TimerService
{
    private readonly WorkspaceService _workspaceService;

    public AzureDevOpsCrawlerService(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    protected override TimeSpan RefreshInterval => TimeSpan.FromMinutes(30);

    protected override Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    protected override Task RefreshAsync()
    {
        _workspaceService.UpdateAzureDevOps();
        return Task.CompletedTask;
    }
}
