using System.Diagnostics;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace ThemesOfDotNet.Services;

internal static class GitHubAuthExtensions
{
    private const string GitHubAvatarUrl = "avatar_url";
    private const string ProductTeamRole = "product-team";

    public static void AddGitHubAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var clientId = configuration["GitHubClientId"];
        var clientSecret = configuration["GitHubClientSecret"];

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.LoginPath = "/signin";
            options.LogoutPath = "/signout";
        })
        .AddGitHub(options =>
        {
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;
            options.ClaimActions.MapJsonKey(GitHubAvatarUrl, GitHubAvatarUrl);
            options.Events.OnCreatingTicket = c => CreateTickAsync(c);
            options.Events.OnTicketReceived = c =>
            {
                if (c.Principal?.IsInRole("product-team") != true)
                    c.ReturnUri = "/signout?returnUrl=access-denied";
                return Task.CompletedTask;
            };
        });
    }

    private static async Task CreateTickAsync(OAuthCreatingTicketContext context)
    {
        Debug.Assert(context.Identity?.Name is not null);

        var userName = context.Identity.Name;

        var productTeamService = context.HttpContext.RequestServices.GetRequiredService<ProductTeamService>();
        var isMember = await productTeamService.IsMemberAsync(userName);

        if (isMember)
            context.Identity.AddClaim(new Claim(context.Identity.RoleClaimType, ProductTeamRole));
    }
}
