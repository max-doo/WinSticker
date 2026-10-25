using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinSticker.Helpers;
using WinSticker.Models;
using WinSticker.Platform;
using WinSticker.Services;
using WinSticker.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace WinSticker.Controls;

/// <summary>The shared note editor: title, color, formatting, attachments and save feedback.</summary>
public sealed partial class NoteEditorPane : UserControl, IDisposable
{
    private QuickCaptureService? _service;
    private SettingsService? _settings;
    private LocalizationService? _localization;
    private NoteWorkspace? _workspace;
    private bool _updating;
    private string _attachmentKey = string.Empty;
    private Func<string, TodoWidgetStore>? _todoStoreFactory;
    public NoteEditingSession? Session { get; private set; }
    public event EventHandler? NoteChanged;
    public MarkdownSourceEditor MarkdownEditor => Editor;

    public NoteEditorPane()
    {
        InitializeComponent();
        Editor.EditorTextChanged += (_, _) => { if (!_updating) Session?.SetBody(Editor.FlushAndGetText()); };
        Editor.CommitRequested += async (_, _) => await FlushAsync();
        Editor.CancelRequested += async (_, _) => await FlushAsync();
        GotFocus += (_, _) => Session?.Activate(this, FlushNative);
        NoteSurface.ActualThemeChanged += (_, _) => DispatcherQueue.TryEnqueue(() => { ApplyAppearance(); BuildColors(); });
        Loaded += (_, _) => { ApplyAppearance(); BuildColors(); };
    }

    public void Configure(QuickCaptureService service, SettingsService settings, LocalizationService localization,
        Func<string, TodoWidgetStore>? todoStoreFactory = null)
    {
        if (_service is not null && !ReferenceEquals(_service, service)) Release();
        if (_workspace is not null) _workspace.ActionChanged -= Workspace_ActionChanged;
        _service = service;
        _settings = settings;
        _localization = localization;
        _todoStoreFactory = todoStoreFactory;
        _workspace = NoteWorkspace.For(service);
        _workspace.ActionChanged += Workspace_ActionChanged;
        Editor.TextResolver = localization.T;
        Editor.EditorEnterBehavior = settings.Settings.QuickCapture.QuickCaptureEditorEnterBehavior;
        double size = settings.Settings.QuickCapture.QuickCaptureContentTextSize;
        Editor.EditorFontSize = SettingsService.NormalizeTextSize(size > 0 ? size : settings.Settings.WidgetShell.TextSize);
        Editor.AllowRemoteImages = settings.Settings.QuickCapture.QuickCaptureAllowRemoteImages;
        TitleEditor.PlaceholderText = T("Notes.Untitled");
        EmptyText.Text = T("Notes.ChooseNote");
        SetName(TitleEditor, "Notes.Title");
        SetName(FavoriteButton, "Notes.Favorite");
        SetName(ColorButton, "Notes.Color");
        SetName(MoreButton, "Common.More");
        SetName(AddAttachmentButton, "Widget.AddFile");
        ExtractTasksItem.Text = T("Notes.ExtractTasks");
        MarkdownItem.Text = T("Notes.LivePreview");
        DeleteItem.Text = T("Common.Delete");
        RetryButton.Content = T("Notes.Retry");
        UndoDeleteButton.Content = T("Common.Undo");
        AttachmentsStrip.RemoveAutomationName = T("Notes.RemoveAttachment");
        BuildColors();
        Refresh();
    }

    public void Open(QuickCaptureItem? item)
    {
        if (Session?.Id == item?.Id) { Refresh(); return; }
        Release();
        if (item is not null && _service is not null)
        {
            Session = _workspace!.Acquire(item,
                () => new DispatcherQueueSessionTimer(DispatcherQueue, TimeSpan.FromMilliseconds(600)), DispatcherQueue);
            Session.Changed += Session_Changed;
        }
        _attachmentKey = string.Empty;
        _updating = true;
        try { Editor.Text = Session?.Body ?? string.Empty; }
        finally { _updating = false; }
        Refresh();
    }

    public void FocusBody() { Session?.Activate(this, FlushNative); Editor.FocusEditor(); }
    public void FlushNative()
    {
        if (_updating || Session is not { IsDeleted: false } session) return;
        _updating = true;
        try
        {
            // TextBox notifications can be queued. Capture both buffers before
            // publishing either field, so a status refresh cannot erase a title.
            string title = TitleEditor.Text;
            string body = Editor.FlushAndGetText();
            session.SetTitle(title);
            session.SetBody(body);
        }
        finally { _updating = false; }
        Refresh();
        NoteChanged?.Invoke(this, EventArgs.Empty);
    }
    public async Task<bool> FlushAsync() { FlushNative(); return Session is null || await Session.FlushAsync(); }

    private void Session_Changed(object? sender, EventArgs args) { if (_updating) return; Refresh(); NoteChanged?.Invoke(this, EventArgs.Empty); }

    private void Refresh()
    {
        if (TitleEditor is null) return;
        bool active = Session is { IsDeleted: false };
        TitleEditor.IsReadOnly = !active;
        FavoriteButton.IsEnabled = ColorButton.IsEnabled = MoreButton.IsEnabled = AddAttachmentButton.IsEnabled = active;
        AttachmentsStrip.CanRemove = active;
        Editor.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        _updating = true;
        try
        {
            if (TitleEditor.Text != (Session?.Title ?? string.Empty)) TitleEditor.Text = Session?.Title ?? string.Empty;
            if (active)
            {
                // A save/status event must not reset the active editor's undo,
                // selection or IME. Other hosts receive committed source only.
                if (!Editor.LiveEditor.HasPendingSource && Editor.LiveEditor.Text != Session!.Body)
                    Editor.Text = Session.Body;
                Editor.ShowFormattingToolbar = Session!.ContentFormat == TextContentFormat.Markdown;
                FavoriteIcon.IsPinned = Session.IsPinned;
                SetName(FavoriteButton, Session.IsPinned ? "Notes.Unfavorite" : "Notes.Favorite");
                MarkdownItem.IsChecked = Editor.ShowFormattingToolbar;
                string key = string.Join('|', Session.Attachments.Select(file => file.Id + ":" + file.FilePath));
                if (key != _attachmentKey)
                {
                    _attachmentKey = key;
                    Editor.AttachmentResolver = id => Session?.Attachments.FirstOrDefault(file => file.Id == id)?.FilePath;
                    AttachmentsStrip.ItemsSource = Session.Attachments.Select(file => new TodoAttachmentViewModel(file)).Cast<object>().ToArray();
                }
            }
            else AttachmentsStrip.ItemsSource = Array.Empty<object>();
            SaveStatus.Text = Session?.SaveFailed == true ? T("Notes.SaveFailed") : Session?.HasUnsavedChanges == true ? T("Notes.Saving") : T("Notes.Saved");
            RetryButton.Visibility = Session?.SaveFailed == true ? Visibility.Visible : Visibility.Collapsed;
            UpdatedTime.Text = Session is null ? string.Empty : Session.UpdatedAt.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
        }
        finally { _updating = false; }
        ApplyAppearance();
        foreach (var button in ColorPalette.Children.OfType<Button>())
            button.BorderThickness = new Thickness(button.Tag is QuickCaptureAppearancePreset color && color == Session?.Appearance ? 2 : 1);
    }

    private void TitleEditor_TextChanged(object sender, TextChangedEventArgs args) { if (!_updating) Session?.SetTitle(TitleEditor.Text); }
    private async void FavoriteButton_Click(object sender, RoutedEventArgs args) => await RunActionAsync(async () => { if (Session is not null) await Session.TogglePinnedAsync(); });
    private void MarkdownItem_Click(object sender, RoutedEventArgs args) => Session?.SetFormat(MarkdownItem.IsChecked ? TextContentFormat.Markdown : TextContentFormat.PlainText);
    private async void RetryButton_Click(object sender, RoutedEventArgs args) => await FlushAsync();

    private void BuildColors()
    {
        if (ColorPalette is null) return;
        ColorPalette.Children.Clear();
        foreach (var preset in Enum.GetValues<QuickCaptureAppearancePreset>())
        {
            var swatch = new Button { Width = 28, Height = 26, Padding = new Thickness(2), Tag = preset,
                Background = MaterialBrush(preset), BorderThickness = new Thickness(Session?.Appearance == preset ? 2 : 1),
                BorderBrush = new SolidColorBrush(NeutralInteractionBrush.Line(this)) };
            string name = T("Notes.Color." + preset);
            AutomationProperties.SetName(swatch, name);
            ToolTipService.SetToolTip(swatch, name);
            swatch.Click += (_, _) => { FlushNative(); Session?.SetAppearance(preset); ColorButton.Flyout.Hide(); BuildColors(); };
            ColorPalette.Children.Add(swatch);
        }
    }

    private void ApplyAppearance()
    {
        if (NoteSurface is null) return;
        VisualStateManager.GoToState(this, (Session?.Appearance ?? QuickCaptureAppearancePreset.Default).ToString(), false);
    }

    private Brush MaterialBrush(QuickCaptureAppearancePreset preset)
    {
        bool dark = NoteSurface.ActualTheme == ElementTheme.Dark;
        var color = preset switch
        {
            QuickCaptureAppearancePreset.Paper => dark ? 0xFF3A3630u : 0xFFFAF5EAu,
            QuickCaptureAppearancePreset.StickyYellow => dark ? 0xFF4A4025u : 0xFFFFF0B3u,
            QuickCaptureAppearancePreset.Rose => dark ? 0xFF472E38u : 0xFFFCE3EAu,
            QuickCaptureAppearancePreset.Mint => dark ? 0xFF284235u : 0xFFDDF3E3u,
            QuickCaptureAppearancePreset.MistBlue => dark ? 0xFF2B3D53u : 0xFFDFECF8u,
            _ => 0u
        };
        return preset == QuickCaptureAppearancePreset.Default
            ? NeutralInteractionBrush.ResolveThemedResource("CardBackgroundFillColorDefaultBrush", NoteSurface) ?? new SolidColorBrush(Colors.Transparent)
            : new SolidColorBrush(Windows.UI.Color.FromArgb((byte)(color >> 24), (byte)(color >> 16), (byte)(color >> 8), (byte)color));
    }

    private async void AddAttachmentButton_Click(object sender, RoutedEventArgs args)
    {
        if (Session is null) return;
        await RunActionAsync(async () =>
        {
            var foreground = Win32Helper.GetForegroundWindow();
            var files = await FileOpenPickerService.PickMultipleFilesAsync(foreground, [], Microsoft.Windows.Storage.Pickers.PickerLocationId.Desktop);
            await AddFilesAsync(files);
        });
    }

    private async Task AddFilesAsync(IEnumerable<string> files)
    {
        if (Session is null || _settings is null) return;
        try { await Session.AddAttachmentsAsync(files, _settings.Settings.QuickCapture.AttachmentStorageMode == SettingsService.AttachmentStorageModeCopy); }
        catch (Exception error) { ReportError(error); }
    }

    private void Pane_DragOver(object sender, DragEventArgs args)
    {
        if (Session is not null && args.DataView.Contains(StandardDataFormats.StorageItems)) { args.AcceptedOperation = DataPackageOperation.Copy; args.Handled = true; }
    }
    private async void Pane_Drop(object sender, DragEventArgs args)
    {
        if (!args.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var deferral = args.GetDeferral();
        try { await AddFilesAsync((await args.DataView.GetStorageItemsAsync()).Select(item => item.Path)); args.Handled = true; }
        catch (Exception error) { ReportError(error); }
        finally { deferral.Complete(); }
    }
    private async void AttachmentsStrip_OpenRequested(object? sender, AttachmentTileEventArgs args)
    {
        await RunActionAsync(async () =>
        {
            if (!File.Exists(args.Attachment.FilePath)) throw new FileNotFoundException();
            await Windows.System.Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(args.Attachment.FilePath));
        });
    }
    private async void AttachmentsStrip_RemoveRequested(object? sender, AttachmentTileEventArgs args)
    {
        await RunActionAsync(async () => { if (Session is not null) await Session.RemoveAttachmentAsync(args.Attachment.Id); });
    }
    private async void DeleteItem_Click(object sender, RoutedEventArgs args)
    {
        await RunActionAsync(async () => { if (Session is not null && _workspace is not null) await _workspace.DeleteAsync(Session); });
    }
    private async void UndoDeleteButton_Click(object sender, RoutedEventArgs args)
    {
        await RunActionAsync(async () => { if (_workspace is not null) await _workspace.UndoDeleteAsync(); });
    }
    private void Workspace_ActionChanged(object? sender, EventArgs args)
    {
        ActionNotice.IsOpen = _workspace?.LastDeletion is not null;
        ActionNotice.Severity = InfoBarSeverity.Success;
        ActionNotice.Message = T("Notes.Deleted");
        UndoDeleteButton.Visibility = Visibility.Visible;
    }
    private void ShowMessage(string message, InfoBarSeverity severity)
    {
        ActionNotice.Severity = severity;
        ActionNotice.Message = message;
        UndoDeleteButton.Visibility = Visibility.Collapsed;
        ActionNotice.IsOpen = true;
    }
    private async Task RunActionAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception error) { ReportError(error); }
    }
    private void ReportError(Exception error)
    {
        App.Log($"[Notes] Operation failed: {error}");
        ShowMessage(T("Common.OperationFailedRetry"), InfoBarSeverity.Error);
    }
    private async void ExtractTasksItem_Click(object sender, RoutedEventArgs args) => await RunActionAsync(ExtractTasksAsync);

    private async Task ExtractTasksAsync()
    {
        if (Session is null || _settings is null) return;
        FlushNative();
        string[] candidates = NoteTodoExtraction.Candidates(Session.Body);
        if (candidates.Length == 0) { ShowMessage(T("Notes.NoTasks"), InfoBarSeverity.Informational); return; }
        var list = new ListView { ItemsSource = candidates, SelectionMode = ListViewSelectionMode.Multiple, MaxHeight = 280 };
        foreach (string candidate in candidates) list.SelectedItems.Add(candidate);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = T("Notes.ExtractTasks"), Content = list,
            PrimaryButtonText = T("Notes.AddTasks"), CloseButtonText = T("Common.Cancel"), DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        string id = TodoTargetResolver.ResolveWidgetId(_settings.Settings);
        var repository = TodoRepository.ForStore((_todoStoreFactory ?? (value => new TodoWidgetStore(value)))(id));
        int added = await NoteTodoExtraction.AddAsync(repository, list.SelectedItems.Cast<string>());
        ShowMessage(string.Format(CultureInfo.CurrentCulture, T("Notes.TasksAdded"), added), InfoBarSeverity.Success);
    }

    private void SetName(DependencyObject element, string key)
    {
        string text = T(key);
        AutomationProperties.SetName(element, text);
        if (element is FrameworkElement control) ToolTipService.SetToolTip(control, text);
    }
    private string T(string key) => (_localization ?? App.Current.LocalizationService).T(key);
    private void Release()
    {
        if (Session is null) return;
        FlushNative();
        Session.Deactivate(this);
        Session.Changed -= Session_Changed;
        _workspace?.Release(Session);
        Session = null;
    }
    public void Dispose()
    {
        Release();
        if (_workspace is not null) _workspace.ActionChanged -= Workspace_ActionChanged;
    }
}
