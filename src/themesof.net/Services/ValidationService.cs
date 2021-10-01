using ThemesOfDotNet.Indexing.Validation;

namespace ThemesOfDotNet.Services;

public sealed class ValidationService
{
    private readonly WorkspaceService _workspaceService;

    public ValidationService(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    public void Initialize()
    {
        Diagnostics = _workspaceService.Workspace.GetDiagnostics();
    }

    public IReadOnlyList<Diagnostic> Diagnostics { get; private set; } = Array.Empty<Diagnostic>();
}
