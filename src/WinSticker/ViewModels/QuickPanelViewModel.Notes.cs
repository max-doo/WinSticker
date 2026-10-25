using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.ViewModels;

public sealed partial class QuickPanelViewModel
{
    [ObservableProperty] private QuickPanelNoteItem? _selectedNote;
    [ObservableProperty] private string _noteTitle = string.Empty;
    [ObservableProperty] private string _noteBody = string.Empty;
    [ObservableProperty] private string _noteSaveStatus = string.Empty;
    public ObservableCollection<QuickPanelNoteItem> Notes { get; } = [];
    public ObservableCollection<QuickPanelNoteItem> ClosedNotes { get; } = [];
    public ObservableCollection<QuickPanelNoteItem> AllNotes { get; } = [];
    private NoteWorkspace? _notesWorkspace;
    private NoteEditingSession? _activeNoteSession;
    private bool _isInternalNoteSwitch;
    private bool _loadingNotes;
    private bool _reloadNotesAgain;
    private TaskCompletionSource? _notesLoaded;
    /// <summary>Synchronize native input before a host changes the selected note.</summary>
    public Action? FlushNoteEditor { get; set; }
    public bool NoteAllowsRemoteImages => _settingsService.Settings.QuickCapture.QuickCaptureAllowRemoteImages;
    internal QuickCaptureService NoteService => _quickCaptureService;
    internal SettingsService NoteSettings => _settingsService;
    internal LocalizationService NoteLocalization => _localizationService;
    internal Func<string, TodoWidgetStore> NoteTodoStoreFactory => _todoStoreFactory;

    private void InitializeNotes()
    {
        _notesWorkspace = NoteWorkspace.For(_quickCaptureService);
        _notesWorkspace.NoteChanged += WorkspaceNoteChanged;
        _quickCaptureService.ChangeDetailed += NotesStoreChanged;
    }
    private void DisposeNotes()
    {
        _quickCaptureService.ChangeDetailed -= NotesStoreChanged;
        if (_notesWorkspace is not null) _notesWorkspace.NoteChanged -= WorkspaceNoteChanged;
        if (_activeNoteSession is not null) _notesWorkspace?.Release(_activeNoteSession);
        _activeNoteSession = null;
    }
    partial void OnSelectedNoteChanging(QuickPanelNoteItem? oldValue, QuickPanelNoteItem? newValue)
    {
        if (_isInternalNoteSwitch) return;
        FlushNoteEditor?.Invoke();
        if (_activeNoteSession is not null) _ = _activeNoteSession.FlushAsync();
    }
    partial void OnSelectedNoteChanged(QuickPanelNoteItem? value)
    {
        if (!_isInternalNoteSwitch) SwitchNoteSession(value);
    }
    private void SwitchNoteSession(QuickPanelNoteItem? note)
    {
        if (_activeNoteSession?.Id != note?.Id)
        {
            if (_activeNoteSession is not null) _notesWorkspace?.Release(_activeNoteSession);
            _activeNoteSession = note is null ? null : _notesWorkspace!.Acquire(note.ToRecord(),
                CreateNoteSessionTimer, _dispatcherQueue, CreateNoteMaxDelayTimer);
        }
        _isInternalNoteSwitch = true;
        try { NoteTitle = _activeNoteSession?.Title ?? string.Empty; NoteBody = _activeNoteSession?.Body ?? string.Empty; }
        finally { _isInternalNoteSwitch = false; }
        UpdateNoteSaveStatus();
    }
    private IMarkdownSessionTimer CreateNoteSessionTimer() =>
        _dispatcherQueue is null ? new HeadlessMarkdownSessionTimer() :
            new DispatcherQueueSessionTimer(_dispatcherQueue, TimeSpan.FromMilliseconds(600));
    private IMarkdownSessionTimer CreateNoteMaxDelayTimer() =>
        _dispatcherQueue is null ? new HeadlessMarkdownSessionTimer() :
            new DispatcherQueueSessionTimer(_dispatcherQueue, NoteEditingSession.MaxSaveDelay);
    partial void OnNoteTitleChanged(string value) { if (!_isInternalNoteSwitch) _activeNoteSession?.SetTitle(value); }
    partial void OnNoteBodyChanged(string value) { if (!_isInternalNoteSwitch) _activeNoteSession?.SetBody(value); }
    private void UpdateNoteSaveStatus() => NoteSaveStatus = _localizationService.T(_activeNoteSession?.SaveFailed == true
        ? "Notes.SaveFailed" : _activeNoteSession?.HasUnsavedChanges == true ? "Notes.Saving" : "Notes.Saved");
    private void WorkspaceNoteChanged(object? sender, NoteEditingSession session) => RunOnUi(() =>
    {
        var note = AllNotes.FirstOrDefault(item => item.Id == session.Id);
        if (note is not null) UpdateNote(note, session);
        if (_activeNoteSession?.Id != session.Id) return;
        _isInternalNoteSwitch = true;
        try { NoteTitle = session.Title; NoteBody = session.Body; }
        finally { _isInternalNoteSwitch = false; }
        UpdateNoteSaveStatus();
    });
    private static void UpdateNote(QuickPanelNoteItem note, NoteEditingSession session)
    {
        note.Title = session.Title; note.Body = session.Body; note.Appearance = session.Appearance;
        note.ContentFormat = session.ContentFormat; note.Attachments = session.Attachments;
        note.IsPinned = session.IsPinned; note.UpdatedAt = session.UpdatedAt;
    }
    private void NotesStoreChanged(QuickCaptureChange change) => RunOnUi(() => _ = HandleNotesStoreChangedAsync(change));
    private async Task HandleNotesStoreChangedAsync(QuickCaptureChange change)
    {
        // A note save touches only its own file: patch the matching row
        // instead of re-reading the whole store and rebuilding every list.
        if (change.Kind == QuickCaptureChangeKind.NoteSaved &&
            change.NoteIds.Count > 0 &&
            change.NoteIds.All(id => AllNotes.Any(note => note.Id == id)))
        {
            foreach (string id in change.NoteIds)
            {
                var record = await _quickCaptureService.GetNoteAsync(id);
                if (record is null) { await LoadNotesAsync(); return; }
                var note = AllNotes.FirstOrDefault(item => item.Id == id);
                if (note is null) { await LoadNotesAsync(); return; }
                note.Title = record.Title ?? string.Empty; note.Body = record.Body; note.Appearance = record.AppearancePreset;
                note.ContentFormat = record.ContentFormat; note.Attachments = record.Attachments;
                note.IsPinned = record.IsPinned; note.UpdatedAt = record.UpdatedAt;
            }

            if (_activeNoteSession is not null && change.NoteIds.Contains(_activeNoteSession.Id)) UpdateNoteSaveStatus();
            return;
        }

        await LoadNotesAsync();
    }
    public async Task LoadNotesAsync()
    {
        if (_isDisposed) return;
        if (_loadingNotes) { _reloadNotesAgain = true; if (_notesLoaded is { } pending) await pending.Task; return; }
        _loadingNotes = true;
        var completion = _notesLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            do
            {
            _reloadNotesAgain = false;
            if (!await TrySaveCurrentNoteAsync()) return;
            var data = await _quickCaptureService.GetDataAsync();
            var records = data.Items.Where(NoteWorkspace.IsNote).OrderBy(item => item.SortOrder).ToList();
            if (records.Count == 0)
            {
                records.Add(await _quickCaptureService.CreateNoteAsync());
                data = await _quickCaptureService.GetDataAsync();
            }
            RunOnUi(() =>
            {
                string? selected = SelectedNote?.Id;
                var old = AllNotes.ToDictionary(item => item.Id, StringComparer.Ordinal);
                var items = records.Select(record =>
                {
                    if (!old.TryGetValue(record.Id, out var note)) note = new QuickPanelNoteItem { Id = record.Id };
                    note.UntitledText = _localizationService.T("Notes.Untitled");
                    note.Title = record.Title ?? string.Empty; note.Body = record.Body; note.Appearance = record.AppearancePreset;
                    note.ContentFormat = record.ContentFormat; note.Attachments = record.Attachments;
                    note.IsPinned = record.IsPinned; note.CreatedAt = record.CreatedAt; note.UpdatedAt = record.UpdatedAt;
                    note.ClosedAt = data.ClosedQuickPanelNotes is { } closed && closed.TryGetValue(record.Id, out var date) ? date : null;
                    return note;
                }).ToArray();
                _isInternalNoteSwitch = true;
                try
                {
                    SyncNotes(AllNotes, items);
                    SyncNotes(Notes, items.Where(item => item.ClosedAt is null).ToArray());
                    SyncNotes(ClosedNotes, items.Where(item => item.ClosedAt is not null).OrderByDescending(item => item.ClosedAt).ToArray());
                    SelectedNote = Notes.FirstOrDefault(item => item.Id == selected) ?? Notes.FirstOrDefault();
                }
                finally { _isInternalNoteSwitch = false; }
                SwitchNoteSession(SelectedNote);
            });
            }
            while (_reloadNotesAgain && !_isDisposed);
        }
        catch (Exception error) { App.Log($"[Notes] Load failed: {error}"); }
        finally
        {
            _loadingNotes = false;
            completion.TrySetResult();
        }
    }
    private static void SyncNotes(ObservableCollection<QuickPanelNoteItem> target, QuickPanelNoteItem[] source)
    {
        foreach (var removed in target.Where(item => !source.Contains(item)).ToArray()) target.Remove(removed);
        for (int index = 0; index < source.Length; index++)
        {
            int old = target.IndexOf(source[index]);
            if (old < 0) target.Insert(index, source[index]); else if (old != index) target.Move(old, index);
        }
    }
    public async Task<bool> TrySaveCurrentNoteAsync()
    {
        FlushNoteEditor?.Invoke();
        return _activeNoteSession is null || await _activeNoteSession.FlushAsync();
    }
    public async Task SaveCurrentNoteAsync() => await TrySaveCurrentNoteAsync();
    public async Task<bool> SelectNoteAsync(QuickPanelNoteItem note)
    {
        if (!await TrySaveCurrentNoteAsync()) return false;
        if (note.ClosedAt is not null) await _quickCaptureService.SetItemClosedAsync(note.Id, false);
        await LoadNotesAsync();
        SelectedNote = Notes.FirstOrDefault(item => item.Id == note.Id);
        return true;
    }
    [RelayCommand]
    public async Task CreateNewNoteAsync()
    {
        if (!await TrySaveCurrentNoteAsync()) return;
        var created = await _quickCaptureService.CreateNoteAsync();
        await LoadNotesAsync();
        SelectedNote = Notes.FirstOrDefault(note => note.Id == created.Id);
    }
    [RelayCommand]
    public async Task CloseNoteAsync(QuickPanelNoteItem? note)
    {
        if (note is null || !await TrySaveCurrentNoteAsync()) return;
        await _quickCaptureService.SetItemClosedAsync(note.Id, true);
        await LoadNotesAsync();
    }
    [RelayCommand]
    public async Task ReopenNoteAsync(QuickPanelNoteItem? note) { if (note is not null) await SelectNoteAsync(note); }
    [RelayCommand]
    public async Task DeleteNoteAsync(QuickPanelNoteItem? note)
    {
        note ??= SelectedNote;
        if (note is null || !await TrySaveCurrentNoteAsync()) return;
        var session = _notesWorkspace!.Acquire(note.ToRecord(), () => new HeadlessMarkdownSessionTimer(), _dispatcherQueue);
        try { await _notesWorkspace.DeleteAsync(session); }
        finally { _notesWorkspace.Release(session); }
        await LoadNotesAsync();
    }
    [RelayCommand]
    public async Task ExtractTodosFromNoteAsync()
    {
        FlushNoteEditor?.Invoke();
        string id = TodoTargetResolver.ResolveWidgetId(_settingsService.Settings);
        int added = await NoteTodoExtraction.AddAsync(TodoRepository.ForStore(_todoStoreFactory(id)), NoteTodoExtraction.Candidates(NoteBody));
        ShowToast(_localizationService.Format("Notes.TasksAdded", added));
    }
}
