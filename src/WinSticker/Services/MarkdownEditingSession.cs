using Microsoft.UI.Dispatching;

namespace WinSticker.Services;

/// <summary>
/// Timer abstraction so the editing session state machine stays unit
/// testable without a live DispatcherQueue.
/// </summary>
public interface IMarkdownSessionTimer : IDisposable
{
    event EventHandler? Tick;

    bool IsEnabled { get; }

    void Start();

    void Stop();
}

/// <summary>
/// One-shot <see cref="DispatcherQueueTimer"/> adapter for
/// <see cref="IMarkdownSessionTimer"/>.
/// </summary>
public sealed class DispatcherQueueSessionTimer : IMarkdownSessionTimer
{
    private readonly DispatcherQueueTimer _timer;

    public DispatcherQueueSessionTimer(DispatcherQueue dispatcherQueue, TimeSpan interval)
    {
        _timer = dispatcherQueue.CreateTimer();
        _timer.Interval = interval;
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) => Tick?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Tick;

    public bool IsEnabled => _timer.IsRunning;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        _timer.Stop();
    }
}

/// <summary>
/// Shared instant-editing session state machine for Markdown surfaces
/// (quick capture detail and desktop todo notes). Owns revision-based
/// dirty tracking, debounced auto-save, save serialization, and the
/// commit retry loop that re-saves changes typed while a save was in
/// flight. The actual persistence lives in the host-supplied
/// <see cref="SaveCore"/> callback; it must not touch revisions.
/// </summary>
public sealed class MarkdownEditingSession
{
    private readonly IMarkdownSessionTimer _autosaveTimer;
    private readonly IMarkdownSessionTimer? _maxDelayTimer;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private long _editRevision;
    private long _savedRevision;

    /// <param name="autosaveTimer">Idle-debounce timer (the 600 ms window).</param>
    /// <param name="maxDelayTimer">
    /// Optional longest-pending-dirty timer. When supplied (notes only —
    /// other Markdown surfaces keep the unchanged debounce-only contract),
    /// continuous input without an idle window still forces a save within
    /// the timer's interval: the deadline runs from the first unsaved
    /// revision, typing does not reset it, and it re-arms only after a save.
    /// </param>
    public MarkdownEditingSession(
        IMarkdownSessionTimer autosaveTimer,
        IMarkdownSessionTimer? maxDelayTimer = null)
    {
        _autosaveTimer = autosaveTimer;
        _autosaveTimer.Tick += AutosaveTimer_Tick;
        _maxDelayTimer = maxDelayTimer;
        if (_maxDelayTimer is not null)
        {
            _maxDelayTimer.Tick += MaxDelayTimer_Tick;
        }
    }

    /// <summary>
    /// Persists the current editor text. Returns true when the content is
    /// on disk (including "nothing to persist"); false on failure.
    /// </summary>
    public Func<Task<bool>>? SaveCore { get; set; }

    public bool IsSaving { get; private set; }

    public bool HasUnsavedChanges { get; private set; }

    public bool IsAutoSaveScheduled => _autosaveTimer.IsEnabled;

    public event EventHandler? SaveCompleted;

    public event EventHandler? SaveFailed;

    /// <summary>User typed: mark dirty and (re)arm the auto-save debounce.</summary>
    public void NotifyChanged()
    {
        MarkDirty();
        _autosaveTimer.Stop();
        _autosaveTimer.Start();
    }

    /// <summary>Mark dirty without scheduling auto-save (appearance/format edits).</summary>
    public void MarkDirty()
    {
        _editRevision++;
        HasUnsavedChanges = true;
        // Arm-once semantics: the deadline spans from the earliest unsaved
        // revision, so continuous typing cannot defer the save forever.
        if (_maxDelayTimer is { IsEnabled: false })
        {
            _maxDelayTimer.Start();
        }
    }

    /// <summary>(Re)arm the auto-save debounce without marking dirty.</summary>
    public void ScheduleAutoSave()
    {
        _autosaveTimer.Stop();
        _autosaveTimer.Start();
    }

    /// <summary>Clear all session state for a new document.</summary>
    public void Reset()
    {
        _autosaveTimer.Stop();
        _maxDelayTimer?.Stop();
        _editRevision = 0;
        _savedRevision = 0;
        HasUnsavedChanges = false;
    }

    /// <summary>Drop pending changes (blank drafts have nothing to preserve).</summary>
    public void AbandonDirtyDraft()
    {
        _editRevision = _savedRevision;
        HasUnsavedChanges = false;
        _maxDelayTimer?.Stop();
    }

    /// <summary>
    /// Wait for any in-flight save and persist pending changes, keeping the
    /// editor active. Call before switching items or closing the surface.
    /// </summary>
    public async Task<bool> FlushAsync()
    {
        if (!HasUnsavedChanges && !IsSaving)
        {
            return true;
        }

        return await SaveAsync(completeEditing: false);
    }

    /// <summary>
    /// Persist under the save gate. With <paramref name="completeEditing"/>
    /// the revision loop keeps saving until the document is clean, so
    /// changes typed during a save are never lost on commit.
    /// </summary>
    public async Task<bool> SaveAsync(bool completeEditing)
    {
        _autosaveTimer.Stop();
        await _saveGate.WaitAsync();
        try
        {
            IsSaving = true;
            if (!HasUnsavedChanges && !completeEditing)
            {
                // A concurrent save already persisted this exact revision
                // while this call waited on the gate.
                return true;
            }

            bool saved;
            do
            {
                saved = await SaveCoreAsync();
                if (!saved)
                {
                    return false;
                }
            }
            while (completeEditing && HasUnsavedChanges);

            return true;
        }
        finally
        {
            IsSaving = false;
            _saveGate.Release();
        }
    }

    private async Task<bool> SaveCoreAsync()
    {
        if (SaveCore is null)
        {
            return true;
        }

        long revisionAtStart = _editRevision;
        bool saved;
        try
        {
            saved = await SaveCore();
        }
        catch (Exception)
        {
            SaveFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        if (saved)
        {
            _savedRevision = Math.Max(_savedRevision, revisionAtStart);
            HasUnsavedChanges = _editRevision > revisionAtStart;
            if (_maxDelayTimer is not null)
            {
                // Revisions that arrived during the save start a fresh
                // deadline; a clean document disarms it.
                if (HasUnsavedChanges)
                {
                    _maxDelayTimer.Start();
                }
                else
                {
                    _maxDelayTimer.Stop();
                }
            }

            SaveCompleted?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            // The draft is still pending: re-arm the deadline so the next
            // window retries instead of spinning a hot retry loop.
            _maxDelayTimer?.Start();
            SaveFailed?.Invoke(this, EventArgs.Empty);
        }

        return saved;
    }

    private async void AutosaveTimer_Tick(object? sender, EventArgs args)
    {
        _autosaveTimer.Stop();
        if (HasUnsavedChanges)
        {
            await SaveAsync(completeEditing: false);
        }
    }

    private async void MaxDelayTimer_Tick(object? sender, EventArgs args)
    {
        _maxDelayTimer?.Stop();
        if (HasUnsavedChanges)
        {
            await SaveAsync(completeEditing: false);
        }
    }
}
