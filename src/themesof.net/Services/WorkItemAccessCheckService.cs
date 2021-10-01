using Microsoft.AspNetCore.Components.Authorization;

namespace ThemesOfDotNet.Services;

public sealed class WorkItemAccessCheckService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public WorkItemAccessCheckService(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<WorkItemAccessCheck> CreateAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var userCanSeePrivates = state.User?.IsInRole("product-team") == true;
        return userCanSeePrivates ? WorkItemAccessCheck.All : WorkItemAccessCheck.PublicOnly;
    }
}
