using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSticker.Models;
using WinSticker.Services;
using Microsoft.UI.Dispatching;

namespace WinSticker.ViewModels;

/// <summary>
/// Root ViewModel for the Quick Panel (快捷弹窗).
/// Coordinates Clipboard history, Todo task planning, and Quick Notes.
/// </summary>
public sealed partial class QuickPanelViewModel : ObservableObject, IDisposable
{
    private readonly QuickCaptureService _quickCaptureService;
    private readonly QuickCaptureClipboardService? _clipboardService;
    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localizationService;
    private readonly Func<string, TodoWidgetStore> _todoStoreFactory;
    private readonly DispatcherQueue? _dispatcherQueue;
    private bool _isDisposed;

    [ObservableProperty]
    private QuickPanelTab _activeTab = QuickPanelTab.Clipboard;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// The window coordinates target capture, clipboard writing and paste as one operation.
    /// </summary>
    public Func<QuickCaptureItem, Task>? PasteItemAsync { get; set; }

    /// <summary>
    /// Event raised when the settings window should be opened.
    /// </summary>
    public event EventHandler? RequestOpenSettings;

    /// <summary>
    /// Event raised when the Quick Panel should hide/close.
    /// </summary>
    public event EventHandler? RequestClose;

    public QuickPanelViewModel(
        QuickCaptureService quickCaptureService,
        SettingsService settingsService,
        LocalizationService localizationService,
        QuickCaptureClipboardService? clipboardService = null,
        Func<string, TodoWidgetStore>? todoStoreFactory = null,
        DispatcherQueue? dispatcherQueue = null)
    {
        _quickCaptureService = quickCaptureService;
        _settingsService = settingsService;
        _localizationService = localizationService;
        _clipboardService = clipboardService;
        _todoStoreFactory = todoStoreFactory ?? (id => new TodoWidgetStore(id));
        _dispatcherQueue = dispatcherQueue ?? TryGetDispatcherQueue();

        var defaultTab = settingsService.Settings.Core.QuickPanelDefaultTab;
        _activeTab = IsTabVisible(defaultTab) ? defaultTab : QuickPanelTab.Clipboard;
        _settingsService.SettingsChanged += OnTabSettingsChanged;

        InitializeClipboard();
        InitializeNotes();
    }

    public bool AutoPaste => _settingsService.Settings.Core.QuickPanelAutoPaste;

    public bool IsTabVisible(QuickPanelTab tab) => tab switch
    {
        QuickPanelTab.Clipboard => true,
        QuickPanelTab.Todo => _settingsService.Settings.Core.QuickPanelShowTodoTab,
        QuickPanelTab.Notes => _settingsService.Settings.Core.QuickPanelShowNotesTab,
        _ => false
    };

    private void OnTabSettingsChanged() => RunOnUi(() =>
    {
        if (_isDisposed) return;
        if (!IsTabVisible(ActiveTab)) ActiveTab = QuickPanelTab.Clipboard;
    });

    public void CycleTab(bool backward = false)
    {
        var values = Enum.GetValues<QuickPanelTab>().Where(IsTabVisible).ToArray();
        int index = Array.IndexOf(values, ActiveTab);
        if (index < 0) index = 0;
        int next = backward ? (index - 1 + values.Length) % values.Length : (index + 1) % values.Length;
        ActiveTab = values[next];
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyClipboardFilter();
    }

    [RelayCommand]
    public void SelectTab(QuickPanelTab tab)
    {
        ActiveTab = IsTabVisible(tab) ? tab : QuickPanelTab.Clipboard;
    }

    [RelayCommand]
    public void TogglePin()
    {
        IsPinned = !IsPinned;
    }

    [RelayCommand]
    public void OpenSettings()
    {
        RequestOpenSettings?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public async Task RefreshAllAsync()
    {
        IsLoading = true;
        try
        {
            await Task.WhenAll(
                LoadClipboardHistoryAsync(),
                LoadNotesAsync());
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ShowToast(string message)
    {
        StatusMessage = message;
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcherQueue is not null && !_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
        else
        {
            action();
        }
    }

    private static DispatcherQueue? TryGetDispatcherQueue()
    {
        try
        {
            return DispatcherQueue.GetForCurrentThread();
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _settingsService.SettingsChanged -= OnTabSettingsChanged;
        DisposeClipboard();
        DisposeNotes();
    }
}
