using Terrajobst.GitHubEvents;

namespace ThemesOfDotNet.Services;

public sealed class GitHubEventProcessingService : IGitHubEventProcessor
{
    private readonly WorkspaceService _workspaceService;

    public GitHubEventProcessingService(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

     public void Process(GitHubEventMessage message)
    {
        _workspaceService.UpdateGitHub(message);
    }
}
