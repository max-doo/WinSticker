using WinSticker.Models;

namespace WinSticker.Services;

/// <summary>
/// Process-wide single write path for one todo store. Every surface that
/// edits todo data (desktop widget, Quick Panel, reminder service, search
/// actions) goes through the repository for its widget id: mutations are
/// serialized against the freshest in-memory copy, and every successful
/// write raises <see cref="StoreChanged"/> so other surfaces can reload.
/// Writing the store directly from a second in-memory snapshot silently
/// clobbers concurrent edits (last-writer-wins on the whole file).
/// </summary>
public sealed class TodoRepository
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, TodoRepository> Repositories = new(StringComparer.OrdinalIgnoreCase);

    private readonly TodoWidgetStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TodoWidgetData? _cached;
    private DateTime _lastWriteStampUtc = DateTime.MinValue;

    /// <summary>
    /// Raised after a successful write. Payload is the writer (origin) so
    /// subscribers can skip reloads they caused themselves. Raised on the
    /// writer's thread — subscribers must marshal to their own UI thread.
    /// </summary>
    public event Action<object?>? StoreChanged;

    private TodoRepository(TodoWidgetStore store)
    {
        _store = store;
    }

    public static TodoRepository ForStore(TodoWidgetStore store)
    {
        string key = store.StorePath;
        lock (SyncRoot)
        {
            if (!Repositories.TryGetValue(key, out TodoRepository? repository))
            {
                repository = new TodoRepository(store);
                Repositories[key] = repository;
            }

            return repository;
        }
    }

    /// <summary>
    /// Drops the cached copy for a store path. Call after the store file is
    /// deleted or cleared outside the repository so a stale cache cannot
    /// resurrect the old data on the next write.
    /// </summary>
    public static void TryRemove(string storePath)
    {
        lock (SyncRoot)
        {
            Repositories.Remove(storePath);
        }
    }

    /// <summary>
    /// Returns the freshest data. The returned instance is shared with the
    /// repository cache — treat it as read-only and use the Mutate/Replace
    /// methods for writes. A write that bypassed the repository (backup
    /// restore, direct store save) is detected via the file timestamp and
    /// reloaded.
    /// </summary>
    public async Task<TodoWidgetData> LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _cached = await LoadFreshIfChangedAsync(_cached, refreshStamp: true);
            return _cached!;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Applies a mutation to the freshest data under the write lock and
    /// persists it atomically (load-modify-save against the live cache).
    /// </summary>
    public async Task MutateAsync(Action<TodoWidgetData> mutate, object? origin = null)
    {
        await _gate.WaitAsync();
        try
        {
            _cached = await LoadFreshIfChangedAsync(_cached, refreshStamp: false);
            mutate(_cached!);
            await _store.SaveAsync(_cached!);
            _lastWriteStampUtc = ReadWriteStampUtc();
        }
        finally
        {
            _gate.Release();
        }

        StoreChanged?.Invoke(origin);
    }

    /// <summary>
    /// Merges a snapshot the caller assembled in memory (the desktop
    /// widget's save path) into the freshest data. Shared ids take the
    /// snapshot's state; items the caller deleted are honored through its
    /// tombstones; items added by another surface after the caller's last
    /// reload are kept, so no concurrent add is ever dropped.
    /// </summary>
    public async Task MergeAsync(TodoWidgetData snapshot, object? origin = null)
    {
        await _gate.WaitAsync();
        try
        {
            _cached = await LoadFreshIfChangedAsync(_cached, refreshStamp: false);
            var current = _cached ?? new TodoWidgetData();

            var snapshotIds = snapshot.Items
                .Select(item => item.Id)
                .ToHashSet(StringComparer.Ordinal);
            var externalItems = current.Items
                .Where(item => !snapshotIds.Contains(item.Id))
                .ToList();

            var merged = new TodoWidgetData
            {
                Items = snapshot.Items.Concat(externalItems).ToList()
            };

            _cached = merged;
            await _store.SaveAsync(merged);
            _lastWriteStampUtc = ReadWriteStampUtc();
        }
        finally
        {
            _gate.Release();
        }

        StoreChanged?.Invoke(origin);
    }

    private async Task<TodoWidgetData?> LoadFreshIfChangedAsync(
        TodoWidgetData? cached,
        bool refreshStamp)
    {
        if (cached is not null && ReadWriteStampUtc() != _lastWriteStampUtc)
        {
            cached = null;
        }

        if (cached is null)
        {
            cached = await _store.LoadAsync();
            if (refreshStamp)
            {
                _lastWriteStampUtc = ReadWriteStampUtc();
            }
        }

        return cached;
    }

    private DateTime ReadWriteStampUtc()
    {
        try
        {
            return File.GetLastWriteTimeUtc(_store.StorePath);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
}
