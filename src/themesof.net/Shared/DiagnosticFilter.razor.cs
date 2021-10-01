using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using ThemesOfDotNet.Indexing.WorkItems;
using ThemesOfDotNet.Services;

using Toolbelt.Blazor.HotKeys;

namespace ThemesOfDotNet.Shared;

public partial class DiagnosticFilter : IDisposable
{
    private ElementReference _inputRef;
    private string _quickFilter = string.Empty;
    private CompletionDialog? _completionDialog;
    private HotKeysContext? _hotKeysContext;

    [Inject]
    public WorkspaceService WorkspaceService { get; set; } = null!;

    [Inject]
    public ValidationService ValidationService { get; set; } = null!;

    [Inject]
    public HotKeys HotKeys { get; set; } = null!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = null!;

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

    protected override void OnInitialized()
    {
        _commands = new Command[]
        {
            new ("Code...", () => AddRuleFilter(), Keys.C),
            new ("Assignee...", () => AddAssigneeFilter(), Keys.A),
        };

        _hotKeysContext = HotKeys.CreateContext();
        _hotKeysContext.Add(ModKeys.None, Keys.ESC, () => JSRuntime.InvokeVoidAsync("blurElement", _inputRef), allowIn: AllowIn.Input);
        _hotKeysContext.Add(ModKeys.None, Keys.Slash, () => _inputRef.FocusAsync());

        foreach (var command in _commands)
        {
            if (command.Shortcut is not null)
                _hotKeysContext.Add(ModKeys.None, command.Shortcut.Value, command.Handler);
        }
    }

    public void Dispose()
    {
        _hotKeysContext?.Dispose();
    }

    private record Command(string Text, Action Handler, Keys? Shortcut = null);

    private Command[] _commands = Array.Empty<Command>();

    private void AddFilter(string text)
    {
        if (string.IsNullOrEmpty(QuickFilter))
            QuickFilter = text;
        else
            QuickFilter = QuickFilter + " " + text;
    }

    private void AddFilter(string key, string value)
    {
        if (value.Contains(" "))
            value = "\"" + value + "\"";

        AddFilter(key + ":" + value);
    }

    private void AddRuleFilter()
    {
        var rules = ValidationService.Diagnostics.Select(d => d.Id)
                                                 .OrderBy(r => r)
                                                 .Distinct()
                                                 .ToArray();

        _completionDialog?.Show(
            "Select Rule",
            rules,
            r => AddFilter("code", r)
        );
    }

    private void AddUserFilter(string key, WorkItemUser user)
    {
        var userName = user.GitHubLogin ?? user.MicrosoftAlias;
        if (userName is null)
            return;

        AddFilter(key, userName);
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