using Microsoft.AspNetCore.Components.Authorization;

using ThemesOfDotNet.Indexing.Querying;

namespace ThemesOfDotNet.Services;

public sealed class QueryContextProvider
{
    public QueryContextProvider(AuthenticationStateProvider authenticationStateProvider)
    {
        var state = authenticationStateProvider.GetAuthenticationStateAsync().Result;
        var userName = state.User?.Identity?.Name;
        Context = new QueryContext(userName);
    }

    public QueryContext Context { get; }
}
