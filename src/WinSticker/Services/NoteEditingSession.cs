using Microsoft.UI.Dispatching;
using WinSticker.Models;

namespace WinSticker.Services;

/// <summary>One note draft and save state shared by every open host of that note.</summary>
public sealed class NoteEditingSession : IDisposable
{
    [Flags]
    private enum Fields { None = 0, Title = 1, Body = 2, Appearance = 4, Format = 8 }
    private readonly QuickCaptureService _service;
    private readonly IMarkdownSessionTimer _timer;
    private readonly MarkdownEditingSession _saving;
    private readonly DispatcherQueue? _dispatcher;
    private Fields _dirty;
    private long _version;
    private object? _activeOwner;
    private Action? _flushEditor;
    private bool _refreshQueued;
    private bool _refreshAgain;
    private bool _disposed;
    internal int ViewCount { get; set; }

    internal NoteEditingSession(QuickCaptureService service, QuickCaptureItem item, IMarkdownSessionTimer timer, DispatcherQueue? dispatcher)
    {
        _service = service;
        _timer = timer;
        _dispatcher = dispatcher;
        Id = item.Id;
        _saving = new(timer) { SaveCore = PersistAsync };
        _saving.SaveCompleted += (_, _) => RaiseChanged();
        _saving.SaveFailed += (_, _) => { SaveFailed = true; RaiseChanged(); };
        Merge(item);
        _service.Changed += StoreChanged;
    }

    public string Id { get; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public QuickCaptureAppearancePreset Appearance { get; private set; }
    public TextContentFormat ContentFormat { get; private set; }
    public IReadOnlyList<TodoAttachment> Attachments { get; private set; } = [];
    public bool IsPinned { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public bool HasUnsavedChanges => _dirty != Fields.None || _saving.HasUnsavedChanges;
    public bool SaveFailed { get; private set; }
    public event EventHandler? Changed;

    public void SetTitle(string value) { if (Title == value) return; Title = value; Dirty(Fields.Title); }
    public void SetBody(string value) { if (Body == value) return; Body = value; Dirty(Fields.Body); }
    public void SetAppearance(QuickCaptureAppearancePreset value) { if (Appearance == value) return; Appearance = value; Dirty(Fields.Appearance); }
    public void SetFormat(TextContentFormat value) { if (ContentFormat == value) return; ContentFormat = value; Dirty(Fields.Format); }

    private void Dirty(Fields field)
    {
        if (IsDeleted) return;
        _dirty |= field;
        _version++;
        SaveFailed = false;
        _saving.NotifyChanged();
        RaiseChanged();
    }

    /// <summary>Synchronize the previous editor before another host takes the input focus.</summary>
    public void Activate(object owner, Action flush)
    {
        if (!ReferenceEquals(owner, _activeOwner)) _flushEditor?.Invoke();
        _activeOwner = owner;
        _flushEditor = flush;
    }

    public void Deactivate(object owner)
    {
        if (!ReferenceEquals(owner, _activeOwner)) return;
        _flushEditor?.Invoke();
        _activeOwner = null;
        _flushEditor = null;
    }

    public async Task<bool> FlushAsync()
    {
        _flushEditor?.Invoke();
        if (IsDeleted) return !HasUnsavedChanges;
        if (!HasUnsavedChanges) return true;
        bool saved = await _saving.SaveAsync(completeEditing: true);
        RaiseChanged();
        return saved;
    }

    private async Task<bool> PersistAsync()
    {
        _flushEditor?.Invoke();
        Fields fields = _dirty;
        if (fields == Fields.None) return true;
        long version = _version;
        QuickCaptureWriteResult result = await _service.PatchNoteAsync(Id,
            fields.HasFlag(Fields.Title) ? Title : null,
            fields.HasFlag(Fields.Body) ? Body : null,
            fields.HasFlag(Fields.Appearance) ? Appearance : null,
            fields.HasFlag(Fields.Format) ? ContentFormat : null);
        // Capture native input that arrived while storage was awaited before
        // the save state machine decides whether the draft is now clean.
        _flushEditor?.Invoke();
        if (!result.Saved) { SaveFailed = true; RaiseChanged(); return false; }
        if (_version == version) _dirty &= ~fields;
        SaveFailed = false;
        if (result.Item is { } item) Merge(item);
        return true;
    }

    public async Task<bool> TogglePinnedAsync()
    {
        if (!await FlushAsync()) return false;
        await _service.SetPinnedAsync(Id, !IsPinned);
        await RefreshAsync();
        return true;
    }

    public async Task<bool> AddAttachmentsAsync(IEnumerable<string> paths, bool copy)
    {
        if (!await FlushAsync()) return false;
        if (await _service.AddAttachmentsAsync(Id, paths, copy) is not { } item) return false;
        Merge(item);
        return true;
    }

    public async Task<bool> RemoveAttachmentAsync(string attachmentId)
    {
        if (!await FlushAsync()) return false;
        if (await _service.DeleteAttachmentAsync(Id, attachmentId) is not { } item) return false;
        Merge(item);
        return true;
    }

    public async Task<QuickCaptureDeletedItemSnapshot?> DeleteAsync()
    {
        if (!await FlushAsync()) return null;
        var snapshot = await _service.DeleteItemAsync(Id);
        if (snapshot is not null) { IsDeleted = true; RaiseChanged(); }
        return snapshot;
    }

    public async Task RefreshAsync()
    {
        var item = (await _service.GetDataAsync()).Items.FirstOrDefault(item => item.Id == Id);
        void Apply()
        {
            if (_disposed) return;
            if (item is null || item.IsDeleted) { IsDeleted = true; RaiseChanged(); }
            else Merge(item);
        }
        if (_dispatcher is not null && !_dispatcher.HasThreadAccess) _dispatcher.TryEnqueue(Apply);
        else Apply();
    }

    private void Merge(QuickCaptureItem item)
    {
        if (item.UpdatedAt < UpdatedAt) return;
        if (!_dirty.HasFlag(Fields.Title)) Title = item.Title ?? string.Empty;
        if (!_dirty.HasFlag(Fields.Body)) Body = item.Body;
        if (!_dirty.HasFlag(Fields.Appearance)) Appearance = item.AppearancePreset;
        if (!_dirty.HasFlag(Fields.Format)) ContentFormat = item.ContentFormat;
        Attachments = item.Attachments.Select(attachment => attachment.Clone()).ToArray();
        IsPinned = item.IsPinned;
        IsDeleted = item.IsDeleted;
        UpdatedAt = item.UpdatedAt;
        RaiseChanged();
    }

    private async void StoreChanged()
    {
        if (_disposed) return;
        if (_refreshQueued) { _refreshAgain = true; return; }
        _refreshQueued = true;
        try
        {
            do { _refreshAgain = false; await RefreshAsync(); }
            while (_refreshAgain && !_disposed);
        }
        catch (Exception error) { App.Log($"[Notes] Refresh failed: {error.Message}"); }
        finally { _refreshQueued = false; }
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _service.Changed -= StoreChanged;
        _timer.Dispose();
        _activeOwner = null;
        _flushEditor = null;
    }
}
