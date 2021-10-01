using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using ThemesOfDotNet.Indexing.WorkItems;
using ThemesOfDotNet.Services;

using Toolbelt.Blazor.HotKeys;

namespace ThemesOfDotNet.Shared;

public partial class WorkItemFilter : IDisposable
{
    private ElementReference _inputRef;
    private string _filter = string.Empty;
    private CompletionDialog? _completionDialog;
    private HotKeysContext? _hotKeysContext;

    [Inject]
    public WorkspaceService WorkspaceService { get; set; } = null!;

    [Inject]
    public HotKeys HotKeys { get; set; } = null!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = null!;

    [Parameter]
    public bool IsQuery { get; set; }

    [Parameter]
    public string Filter
    {
        get => _filter;
        set
        {
            if (_filter != value)
            {
                _filter = value;
                FilterChanged.InvokeAsync(value);
            }
        }
    }

    [Parameter]
    public EventCallback<string> FilterChanged { get; set; }

    protected override void OnInitialized()
    {
        var ctrl = IsQuery ? ModKeys.Ctrl : ModKeys.None;

        var commands = new List<Command>()
        {
            new ("Is...", () => AddIsFilter(), ctrl, Keys.I),
            new ("Priority...", () => AddPriorityFilter(), ctrl, Keys.P),
            new ("Cost...", () => AddCostFilter(), ctrl, Keys.C),
            new ("Team...", () => AddTeamFilter(), ctrl, Keys.T),
            new ("Area...", () => AddAreaFilter(), ctrl, Keys.E),
            new ("Product...", () => AddProductFilter(), ctrl, Keys.D),
            new ("Milestone...", () => AddMilestoneFilter(), ctrl, Keys.M),
            new ("Author...", () => AddAuthorFilter(), ctrl, Keys.U),
            new ("Assignee...", () => AddAssigneeFilter(), ctrl, Keys.A),
        };

        if (IsQuery)
        {
            commands.Insert(0, new("Group By...", () => SetGroupFilter(), ctrl, Keys.G));
        }

        _commands = commands.ToArray();

        _hotKeysContext = HotKeys.CreateContext();
        _hotKeysContext.Add(ModKeys.None, Keys.ESC, () => JSRuntime.InvokeVoidAsync("blurElement", _inputRef), allowIn: AllowIn.Input);
        _hotKeysContext.Add(ctrl, Keys.Slash, () => _inputRef.FocusAsync());

        foreach (var command in _commands)
        {
            if (command.Keys is not null)
                _hotKeysContext.Add(command.ModKeys, command.Keys.Value, command.Handler);
        }
    }

    public void Dispose()
    {
        _hotKeysContext?.Dispose();
    }

    private record Command(string Text, Action Handler, ModKeys ModKeys, Keys? Keys = null);

    private Command[] _commands = Array.Empty<Command>();

    private void SetFilter(string key, string value)
    {
        var keyWithColon = key + ":";
        var index = Filter.IndexOf(keyWithColon);
        if (index >= 0)
        {
            var keyEnd = index + keyWithColon.Length;
            var indexOfFirstSpace = Filter.IndexOf(' ', keyEnd);
            if (indexOfFirstSpace < 0)
                indexOfFirstSpace = Filter.Length;

            var before = Filter.Substring(0, keyEnd);
            var after = Filter.Substring(indexOfFirstSpace);
            Filter = before + value + after;
            return;
        }

        AddFilter(key, value);
    }

    private void SetGroupFilter()
    {
        var items = new[] {
            "none",
            "parent",
            "theme",
            "area"
        };

        _completionDialog?.Show(
            "Group By",
            items,
            i => SetFilter("group", i)
        );
    }

    private void AddFilter(string text)
    {
        if (string.IsNullOrEmpty(Filter))
            Filter = text;
        else
            Filter = Filter + " " + text;
    }

    private void AddFilter(string key, string value)
    {
        if (value.Contains(" "))
            value = "\"" + value + "\"";

        AddFilter(key + ":" + value);
    }

    private void AddIsFilter()
    {
        var items = new[] {
            "open",
            "closed",
            "theme",
            "epic",
            "userstory",
            "task",
            "committed",
            "proposed",
            "completed",
            "cut",
        };

        _completionDialog?.Show(
            "Select Is",
            items,
            i => AddFilter("is", i)
        );
    }

    private void AddPriorityFilter()
    {
        var priorities = new[] { "0", "1", "2", "3" };

        _completionDialog?.Show(
            "Select Priorities",
            priorities,
            p => AddFilter("priority", p)
        );
    }

    private void AddCostFilter()
    {
        var costs = Enum.GetNames<WorkItemCost>()
                        .Select(n => n.ToLower())
                        .ToArray();

        _completionDialog?.Show(
            "Select Cost",
            costs,
            c => AddFilter("cost", c)
        );
    }

    private void AddTeamFilter()
    {
        var teams = WorkspaceService.Workspace
                                    .WorkItems
                                    .SelectMany(wi => wi.Teams)
                                    .OrderBy(t => t)
                                    .Distinct()
                                    .ToArray();

        _completionDialog?.Show(
            "Select Team",
            teams,
            t => AddFilter("team", t)
        );
    }

    private void AddAreaFilter()
    {
        var areas = WorkspaceService.Workspace
                                    .WorkItems
                                    .SelectMany(wi => wi.Areas)
                                    .OrderBy(t => t)
                                    .Distinct()
                                    .ToArray();

        _completionDialog?.Show(
            "Select Area",
            areas,
            a => AddFilter("area", a)
        );
    }

    private void AddProductFilter()
    {
        _completionDialog?.Show(
            "Select Team",
            WorkspaceService.Workspace.Products,
            p => AddFilter("product", p.Name)
        );
    }

    private void AddMilestoneFilter()
    {
        _completionDialog?.Show(
            "Select Milestone",
            WorkspaceService.Workspace.Milestones,
            m => AddFilter("milestone", m.Version.ToString())
        );
    }

    private void AddUserFilter(string key, WorkItemUser user)
    {
        var userName = user.GitHubLogin ?? user.MicrosoftAlias;
        if (userName is null)
            return;

        AddFilter(key, userName);
    }

    private void AddAuthorFilter()
    {
        _completionDialog?.Show(
            "Select Author",
            WorkspaceService.Workspace.Users,
            u => AddUserFilter("author", u)
        );
    }

    private void AddAssigneeFilter()
    {
        _completionDialog?.Show(
            "Select Assignee",
            WorkspaceService.Workspace.Users,
            u => AddUserFilter("assignee", u)
        );
    }
}
