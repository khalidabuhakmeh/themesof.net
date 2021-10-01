using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ThemesOfDotNet.Shared;

public partial class PageTreeToolbar
{
    private string _quickFilter = string.Empty;

    [Parameter]
    public EventCallback<MouseEventArgs> ExpandAllClicked { get; set; }

    [Parameter]
    public EventCallback<MouseEventArgs> CollapseAllClicked { get; set; }

    [Parameter]
    public EventCallback<MouseEventArgs> ExpandUnmatchedClicked { get; set; }

    [Parameter]
    public string QuickFilter
    {
        get => _quickFilter;
        set
        {
            if (_quickFilter != value)
            {
                _quickFilter = value;
                QuickFilterChanged.InvokeAsync(value);
            }
        }
    }

    [Parameter]
    public EventCallback<string> QuickFilterChanged { get; set; }
}
