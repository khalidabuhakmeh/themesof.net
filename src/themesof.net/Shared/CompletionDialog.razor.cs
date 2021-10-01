using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ThemesOfDotNet.Shared;

public partial class CompletionDialog
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    private string _filterText = string.Empty;
    private object[] _filteredItems = Array.Empty<object>();
    private IReadOnlyCollection<object> _items = Array.Empty<object>();
    private Action<object> _acceptedCallback = _ => { };
    private ElementReference _inputRef;
    private ElementReference _completionListRef;

    public string Title { get; set; } = "Select item";

    public bool Visible { get; private set; }

    private string FilterText
    {
        get => _filterText;
        set
        {
            if (value is null)
                value = string.Empty;

            if (_filterText != value)
            {
                _filterText = value;
                UpdateItems();
            }
        }
    }

    private object? SelectedItem { get; set; }

    private void UpdateItems()
    {
        _filteredItems = _items.OfType<object>()
                               .Where(m => (m?.ToString() ?? string.Empty).Contains(_filterText, StringComparison.OrdinalIgnoreCase))
                               .ToArray();
        SelectedItem = _filteredItems.FirstOrDefault();
    }

    public void Show<T>(string title, IReadOnlyCollection<T> items, Action<T> acceptedCallback)
        where T : class
    {
        _items = items;
        _filteredItems = items.ToArray();
        _acceptedCallback = item => acceptedCallback((T)item);
        Title = title;
        FilterText = string.Empty;
        SelectedItem = null;
        Visible = true;
        StateHasChanged();
    }

    public void Close()
    {
        Visible = false;
        if (SelectedItem is not null)
            _acceptedCallback(SelectedItem);
        StateHasChanged();
    }

    private void Select(object item)
    {
        SelectedItem = item;
        Close();
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            SelectedItem = null;
            Close();
        }
        else if (e.Key == "Enter")
        {
            Close();
        }
    }

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        if (Visible)
            await JSRuntime.InvokeVoidAsync("initializeCompletionDialogInput", _inputRef, _completionListRef, DotNetObjectReference.Create(this));
    }

    [JSInvokable]
    public void SelectPreviousElement()
    {
        ChangeSelection(-1);
    }

    [JSInvokable]
    public void SelectNextElement()
    {
        ChangeSelection(1);
    }

    private void ChangeSelection(int direction)
    {
        var index = Math.Clamp(Array.IndexOf(_filteredItems, SelectedItem) + direction, 0, _filteredItems.Length - 1);
        if (_filteredItems.Length > 0)
            SelectedItem = _filteredItems[index];

        StateHasChanged();
    }
}
