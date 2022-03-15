using ThemesOfDotNet.Indexing.Ospo;

namespace ThemesOfDotNet.Services;

public sealed class OspoCrawlerService : TimerService
{
    private readonly ILogger<OspoCrawlerService> _logger;
    private readonly WorkspaceService _workspaceService;

    public OspoCrawlerService(ILogger<OspoCrawlerService> logger,
                              WorkspaceService workspaceService)
    {
        _logger = logger;
        _workspaceService = workspaceService;
    }

    public IReadOnlyList<OspoLink> Links { get; private set; } = Array.Empty<OspoLink>();

    protected override TimeSpan RefreshInterval => TimeSpan.FromHours(2);

    protected override Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    protected override async Task RefreshAsync()
    {
        _logger.LogInformation("Refreshing OSPO cache...");
        try
        {
            await _workspaceService.UpdateOspoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while refreshing data from OSPO");
        }
    }
}
