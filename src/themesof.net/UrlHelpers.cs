using System.Web;

using Microsoft.AspNetCore.Components;

using ThemesOfDotNet.Indexing.WorkItems;

namespace ThemesOfDotNet;

public static class UrlHelpers
{
    public static string GetUrl(this WorkItemKind kind)
    {
        return $"icons/kind_{kind.ToString().ToLowerInvariant()}.svg";
    }

    public static string GetUrl(this WorkItemState state)
    {
        return $"icons/state_{state.ToString().ToLowerInvariant()}.svg";
    }

    public static string GetUrl(this WorkItemChangeKind kind)
    {
        switch (kind)
        {
            case WorkItemChangeKind.PriorityChanged:
                return $"icons/change_priority.svg";
            case WorkItemChangeKind.CostChanged:
                return $"icons/change_cost.svg";
            case WorkItemChangeKind.MilestoneChanged:
                return $"icons/change_milestone.svg";
            default:
                throw new Exception(@"Unhandled value '{kind}'");
        }
    }

    public static string GetViewUrl(this WorkItem workItem)
    {
        return "/view/" + workItem.Id.Replace('#', '/');
    }

    public static string GetWorkItemId(string url)
    {
        var lastIndexOfSlash = url.LastIndexOf('/');
        if (lastIndexOfSlash < 0)
            return url;

        return url.Remove(lastIndexOfSlash, 1)
                  .Insert(lastIndexOfSlash, "#");
    }

    public static string GetSignInUrl(this NavigationManager navigationManager)
    {
        var uri = new Uri(navigationManager.Uri);
        var returnUrl = HttpUtility.UrlEncode(uri.PathAndQuery);
        return returnUrl == "/" ? "/signin" : $"/signin?returnUrl={returnUrl}";
    }

    public static void SignIn(this NavigationManager navigationManager)
    {
        var url = navigationManager.GetSignInUrl();
        navigationManager.NavigateTo(url);
    }
}
