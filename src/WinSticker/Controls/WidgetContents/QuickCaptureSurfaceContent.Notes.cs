using Microsoft.UI.Xaml;
using WinSticker.Services;
using WinSticker.ViewModels;

namespace WinSticker.Controls.WidgetContents;

public sealed partial class QuickCaptureSurfaceContent
{
    private NoteWorkspace? _noteWorkspace;
    private bool UsesUnifiedNote => _detailItem is not null && NoteWorkspace.IsNote(_detailItem.ToModel());

    private void ConfigureUnifiedNotes()
    {
        _noteWorkspace = NoteWorkspace.For(ViewModel.NoteService);
        UnifiedNoteEditor.Configure(ViewModel.NoteService, _settingsService, _localizationService);
        UnifiedNoteEditor.NoteChanged += UnifiedNoteChanged;
    }

    private void UnifiedNoteChanged(object? sender, EventArgs args)
    {
        if (UnifiedNoteEditor.Session?.Id != _detailItem?.Id) return;
        _detailHasUnsavedChanges = UnifiedNoteEditor.Session?.HasUnsavedChanges == true;
        if (UnifiedNoteEditor.Session is { } note)
        {
            _detailAppearance = note.Appearance;
            _detailContentFormat = note.ContentFormat;
        }
    }

    private bool TryOpenUnifiedNote(QuickCaptureItemViewModel item)
    {
        if (!NoteWorkspace.IsNote(item.ToModel())) { UnifiedNoteEditor.Open(null); return false; }
        _isDetailEditing = true;
        _detailContentFormat = item.ContentFormat;
        _detailEditRevision = _detailSavedRevision = 0;
        UnifiedNoteEditor.Open(item.ToModel());
        _detailHasUnsavedChanges = UnifiedNoteEditor.Session?.HasUnsavedChanges == true;
        _showDetailInSinglePane = !_isDualPane;
        foreach (var candidate in ViewModel.Items) candidate.IsDetailSelected = candidate.Id == item.Id;
        ApplyResponsiveLayout();
        RefreshDetailPresentation();
        return true;
    }

    private bool RefreshUnifiedNotePresentation()
    {
        bool show = UsesUnifiedNote || (_detailItem is null && _noteWorkspace?.LastDeletion is not null);
        UnifiedNoteEditor.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        LegacyDetailContent.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        DetailHeaderActions.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        DetailEditSaveHost.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        if (!show) return false;
        DetailContent.Visibility = Visibility.Visible;
        DetailEmptyState.Visibility = Visibility.Collapsed;
        DetailHeader.Visibility = _isDualPane ? Visibility.Collapsed : Visibility.Visible;
        DetailBackButton.Visibility = _isDualPane ? Visibility.Collapsed : Visibility.Visible;
        return true;
    }

    private async Task<bool> FlushUnifiedNoteAsync()
    {
        bool saved = await UnifiedNoteEditor.FlushAsync();
        _detailHasUnsavedChanges = UnifiedNoteEditor.Session?.HasUnsavedChanges == true;
        return saved;
    }

    private async Task<bool> ChangeUnifiedNoteAppearanceAsync(QuickCaptureItemViewModel item, Models.QuickCaptureAppearancePreset preset)
    {
        var session = _noteWorkspace!.Acquire(item.ToModel(),
            () => new DispatcherQueueSessionTimer(DispatcherQueue, TimeSpan.FromMilliseconds(600)), DispatcherQueue,
            () => new DispatcherQueueSessionTimer(DispatcherQueue, NoteEditingSession.MaxSaveDelay));
        try { session.SetAppearance(preset); return await session.FlushAsync(); }
        finally { _noteWorkspace.Release(session); }
    }
}
