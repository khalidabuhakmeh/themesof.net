namespace ThemesOfDotNet.Services;

public sealed class AzureDevOpsCrawlerService : TimerService
{
    private readonly ILogger<AzureDevOpsCrawlerService> _logger;
    private readonly WorkspaceService _workspaceService;

    public AzureDevOpsCrawlerService(ILogger<AzureDevOpsCrawlerService> logger,
                                     WorkspaceService workspaceService)
    {
        _logger = logger;
        _workspaceService = workspaceService;
    }

    protected override TimeSpan RefreshInterval => TimeSpan.FromMinutes(30);

    protected override Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    protected override Task RefreshAsync()
    {
        _logger.LogInformation("Refreshing AzureDevOps cache...");
        try
        {
            _workspaceService.UpdateAzureDevOps();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while refreshing data from AzureDevOps");
        }

        return Task.CompletedTask;
    }
}
