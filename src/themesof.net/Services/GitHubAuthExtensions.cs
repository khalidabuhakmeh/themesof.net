using System.Diagnostics;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

using Octokit;

namespace ThemesOfDotNet.Services;

internal static class GitHubAuthExtensions
{
    private const string GitHubAvatarUrl = "avatar_url";
    private const string ProductTeamRole = "product-team";

    public static void AddGitHubAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var clientId = configuration["GitHubClientId"];
        var clientSecret = configuration["GitHubClientSecret"];
        var productTeamOrg = configuration["ProductTeamOrg"];
        var productTeamSlug = configuration["ProductTeamSlug"];

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
            options.Events.OnCreatingTicket = c => CreateTickAsync(c, productTeamOrg, productTeamSlug);
            options.Events.OnTicketReceived = c =>
            {
                if (c.Principal?.IsInRole("product-team") != true)
                    c.ReturnUri = "/signout?returnUrl=access-denied";
                return Task.CompletedTask;
            };
        });
    }

    private static async Task CreateTickAsync(OAuthCreatingTicketContext context, string productTeamOrg, string productTeamSlug)
    {
        Debug.Assert(context.AccessToken is not null);
        Debug.Assert(context.Identity?.Name is not null);

        var accessToken = context.AccessToken;
        var userName = context.Identity.Name;
        var isMember = await IsMemberOfTeamAsync(accessToken, productTeamOrg, productTeamSlug, userName);

        if (isMember)
            context.Identity.AddClaim(new Claim(context.Identity.RoleClaimType, ProductTeamRole));
    }

    private static async Task<bool> IsMemberOfTeamAsync(string accessToken, string orgName, string teamSlug, string userName)
    {
        try
        {
            var productInformation = new ProductHeaderValue("themesofdotnet");
            var client = new GitHubClient(productInformation)
            {
                Credentials = new Credentials(accessToken)
            };

            var uri = new Uri(client.Connection.BaseAddress, $"orgs/{orgName}/teams/{teamSlug}/memberships/{userName}");
            var result = await client.Connection.GetResponse<TeamMembershipDetails>(uri);
            return result.Body.State.Value == MembershipState.Active;
        }
        catch (NotFoundException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }
    }
}
