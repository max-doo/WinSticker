using System.Security.Cryptography;
using WinSticker.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace WinSticker.Services;

public sealed record QuickCaptureImageCacheInfo(
    int TotalFileCount,
    long TotalBytes,
    int UnusedFileCount,
    long UnusedBytes);

public sealed record QuickCaptureImageCacheCleanupResult(
    int DeletedFileCount,
    long DeletedBytes);

public sealed record QuickCaptureDeletedItemSnapshot(
    QuickCaptureItem Item,
    bool IsRecent);

public sealed record QuickCaptureWriteResult(
    bool Saved,
    bool WasTruncated,
    QuickCaptureItem? Item);

/// <summary>Granularity of a store change, for targeted refreshes.</summary>
public enum QuickCaptureChangeKind
{
    /// <summary>One note's content/metadata was committed to its own file; ids are included.</summary>
    NoteSaved,
    /// <summary>Records were added, removed, reordered or re-pinned; notes were created or deleted.</summary>
    ItemsChanged,
    /// <summary>Host view state changed (closed-tab history, current view).</summary>
    HostStateChanged,
    /// <summary>The recent-clipboard list changed.</summary>
    ClipboardChanged
}

public sealed record QuickCaptureChange(
    QuickCaptureChangeKind Kind,
    IReadOnlyList<string> NoteIds);

public sealed record QuickCaptureNoteFileError(
    string NoteId,
    string Status,
    string? FileName,
    string? ErrorMessage);

public sealed class QuickCaptureService
{
    public const int DefaultRecentLimit = 30;
    public const int MinRecentLimit = 10;
    public const int MaxRecentLimit = 100;
    public const int MaxItemBodyCharacters = MarkdownDocumentService.MaxCharacters;
    private const uint ThumbnailMaxPixelSize = 180;
    private static readonly TimeSpan ExportCleanupAge = TimeSpan.FromDays(1);
    private const int MaxRetainedNoteTombstoneFiles = 500;

    private readonly QuickCaptureStore _store;
    private readonly NoteFileStore _noteFiles;
    private readonly Func<string, Task<bool>>? _recycleAttachmentFileAsync;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _thumbnailTaskLock = new();
    private readonly Dictionary<string, Task<string?>> _thumbnailTasks = new(StringComparer.OrdinalIgnoreCase);
    private QuickCaptureStoreData? _data;
    private Dictionary<string, QuickCaptureItem> _notes = new(StringComparer.Ordinal);
    private List<QuickCaptureNoteFileError> _noteFileErrors = [];

    /// <summary>
    /// Fires for every change with its granularity; <see cref="NoteSaved"/>
    /// carries the affected note ids so sessions and lists refresh only the
    /// touched rows instead of re-reading the whole store.
    /// </summary>
    public event Action<QuickCaptureChange>? ChangeDetailed;

    /// <summary>Legacy all-changes event; not raised for single-note saves.</summary>
    public event Action? Changed;

    public QuickCaptureService(
        QuickCaptureStore? store = null,
        Func<string, Task<bool>>? recycleAttachmentFileAsync = null)
    {
        _store = store ?? new QuickCaptureStore();
        _noteFiles = new NoteFileStore(_store.DataDirectory);
        _recycleAttachmentFileAsync = recycleAttachmentFileAsync;
    }

    public IReadOnlyList<QuickCaptureNoteFileError> NoteFileErrors => _noteFileErrors;

    internal string NotesDirectory => _noteFiles.NotesDirectory;

    public async Task<QuickCaptureStoreData> GetDataAsync()
    {
        await EnsureLoadedAsync();
        return CloneWithMergedItems();
    }

    /// <summary>Single-note projection clone; null when the id is unknown or deleted.</summary>
    public async Task<QuickCaptureItem?> GetNoteAsync(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        await EnsureLoadedAsync();
        return FindItemIncludingDeleted(itemId) is { } item ? Clone(item) : null;
    }

    /// <summary>Create a persisted, unnamed blank note without inventing a user title.</summary>
    public async Task<QuickCaptureItem> CreateNoteAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var merged = MergedItemsCore();
            var item = new QuickCaptureItem
            {
                IsNote = true, ContentFormat = TextContentFormat.Markdown,
                SortOrder = merged.Count == 0 ? 0 : merged.Max(entry => entry.SortOrder) + 1
            };
            _notes[item.Id] = item;
            await SaveItemAsync(item, notify: false);
            RaiseChanged(QuickCaptureChangeKind.ItemsChanged);
            return Clone(item);
        }
        finally { _gate.Release(); }
    }

    public async Task<QuickCaptureItem> AddItemAsync(string body)
    {
        return await AddDetailedItemAsync(null, body, QuickCaptureAppearancePreset.Default);
    }

    public async Task<QuickCaptureItem> AddDetailedItemAsync(
        string? title,
        string body,
        QuickCaptureAppearancePreset appearancePreset,
        TextContentFormat contentFormat = TextContentFormat.PlainText,
        bool pin = false)
    {
        QuickCaptureWriteResult result = await AddDetailedItemWithResultAsync(
            title,
            body,
            appearancePreset,
            contentFormat,
            pin);
        return result.Item!;
    }

    public async Task<QuickCaptureWriteResult> AddDetailedItemWithResultAsync(
        string? title,
        string body,
        QuickCaptureAppearancePreset appearancePreset,
        TextContentFormat contentFormat = TextContentFormat.PlainText,
        bool pin = false)
    {
        contentFormat = NormalizeContentFormat(contentFormat);
        string normalizedBody = NormalizeBody(body, contentFormat);
        string? normalizedTitle = NormalizeOptionalText(title);
        if (string.IsNullOrWhiteSpace(normalizedTitle) && string.IsNullOrWhiteSpace(normalizedBody))
        {
            throw new ArgumentException("Quick Capture title and body cannot both be empty.", nameof(body));
        }

        string itemId = Guid.NewGuid().ToString("N");
        var attachments = new List<TodoAttachment>();
        normalizedBody = await ExtractInlineMarkdownImagesAsync(
            itemId,
            normalizedBody,
            contentFormat,
            attachments);
        normalizedBody = TruncateBody(normalizedBody, out bool wasTruncated);

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var now = DateTimeOffset.UtcNow;
            var before = SnapshotOrders();
            if (pin)
            {
                ShiftPinnedBelowNewTopPin();
            }

            var item = new QuickCaptureItem
            {
                Id = itemId,
                Body = normalizedBody,
                ContentFormat = contentFormat,
                Title = normalizedTitle,
                Type = TryDetectUrl(normalizedBody, out string? url) ? QuickCaptureItemType.Link : QuickCaptureItemType.Text,
                Url = url,
                Attachments = attachments,
                AppearancePreset = NormalizeAppearancePreset(appearancePreset),
                SourceKind = QuickCaptureSourceKind.Manual,
                IsPinned = pin,
                IsRecent = false,
                SortOrder = NextSortOrderForInsertAtTop(),
                PinnedSortOrder = pin ? 0 : -1,
                CreatedAt = now,
                UpdatedAt = now
            };
            if (QualifiesAsNote(item))
            {
                item.IsNote = true;
            }

            if (item.IsNote)
            {
                _notes[item.Id] = item;
                await SaveItemAsync(item, notify: false);
            }
            else
            {
                _data!.Items.Insert(0, item);
                await SaveHostCoreAsync(notify: false);
            }

            await PersistOrderChangesCoreAsync(before);
            RaiseChanged(QuickCaptureChangeKind.ItemsChanged);
            return new QuickCaptureWriteResult(true, wasTruncated, Clone(item));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> UpdateItemAsync(string itemId, string body)
    {
        QuickCaptureWriteResult result = await UpdateItemWithResultAsync(itemId, body);
        return result.Saved;
    }

    public async Task<QuickCaptureWriteResult> UpdateItemWithResultAsync(string itemId, string body)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return new QuickCaptureWriteResult(false, false, null);
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var item = FindItem(itemId);
            if (item is null || item.IsDeleted)
            {
                return new QuickCaptureWriteResult(false, false, null);
            }

            string normalizedBody = NormalizeBody(body, item.ContentFormat);
            if (string.IsNullOrWhiteSpace(normalizedBody))
            {
                return new QuickCaptureWriteResult(false, false, null);
            }

            normalizedBody = await ExtractInlineMarkdownImagesAsync(
                item.Id,
                normalizedBody,
                item.ContentFormat,
                item.Attachments);
            normalizedBody = TruncateBody(normalizedBody, out bool wasTruncated);
            item.Body = normalizedBody;
            item.Type = TryDetectUrl(normalizedBody, out string? url) ? QuickCaptureItemType.Link : QuickCaptureItemType.Text;
            item.Url = url;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            bool saved = await SaveItemAsync(item);
            return new QuickCaptureWriteResult(saved, wasTruncated, saved ? Clone(item) : null);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuickCaptureItem?> AddRecentClipboardItemAsync(string body, int maxRecentItems)
    {
        string normalizedBody = NormalizeBody(body);
        if (string.IsNullOrWhiteSpace(normalizedBody) || ShouldIgnoreClipboardText(normalizedBody))
        {
            return null;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            if (_data!.RecentItems.FirstOrDefault(item => !item.IsDeleted) is { } latest &&
                string.Equals(latest.Body, normalizedBody, StringComparison.Ordinal))
            {
                return null;
            }

            _data.RecentItems.RemoveAll(item => string.Equals(item.Body, normalizedBody, StringComparison.Ordinal));
            var now = DateTimeOffset.UtcNow;
            var item = new QuickCaptureItem
            {
                Body = normalizedBody,
                ContentFormat = TextContentFormat.PlainText,
                Type = TryDetectUrl(normalizedBody, out string? url) ? QuickCaptureItemType.Link : QuickCaptureItemType.Text,
                Url = url,
                SourceKind = QuickCaptureSourceKind.Clipboard,
                IsRecent = true,
                SortOrder = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            foreach (var existing in _data.RecentItems)
            {
                existing.SortOrder++;
            }

            _data.RecentItems.Insert(0, item);
            TrimRecentItemsCore(NormalizeRecentLimit(maxRecentItems));
            await SaveHostCoreAsync(kind: QuickCaptureChangeKind.ClipboardChanged);
            CleanupUnusedImageCacheCore();
            return Clone(item);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuickCaptureItem?> AddRecentClipboardImageAsync(byte[] imagePngBytes, int maxRecentItems)
    {
        if (imagePngBytes.Length == 0)
        {
            return null;
        }

        string contentHash = ComputeContentHash(imagePngBytes);

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            if (_data!.RecentItems.FirstOrDefault(item => !item.IsDeleted) is { } latest &&
                string.Equals(latest.ContentHash, contentHash, StringComparison.Ordinal))
            {
                return null;
            }

            string imagePath = await SaveImageAsync(imagePngBytes, contentHash);
            _data.RecentItems.RemoveAll(item => string.Equals(item.ContentHash, contentHash, StringComparison.Ordinal));
            var now = DateTimeOffset.UtcNow;
            var item = new QuickCaptureItem
            {
                Body = "Image",
                ContentFormat = TextContentFormat.PlainText,
                Type = QuickCaptureItemType.Image,
                ImagePath = imagePath,
                ContentHash = contentHash,
                Attachments = [CreateImageAttachment(imagePath, now)],
                SourceKind = QuickCaptureSourceKind.Clipboard,
                IsRecent = true,
                SortOrder = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            foreach (var existing in _data.RecentItems)
            {
                existing.SortOrder++;
            }

            _data.RecentItems.Insert(0, item);
            TrimRecentItemsCore(NormalizeRecentLimit(maxRecentItems));
            await SaveHostCoreAsync(kind: QuickCaptureChangeKind.ClipboardChanged);
            CleanupUnusedImageCacheCore();
            return Clone(item);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuickCaptureItem?> AddImageFileItemAsync(
        string imagePath,
        bool pin = false)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath) || !IsImageFile(imagePath))
        {
            return null;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            string cachedImagePath = await SaveImageFileAsync(imagePath);
            string contentHash = Path.GetFileNameWithoutExtension(cachedImagePath);
            var now = DateTimeOffset.UtcNow;
            var before = SnapshotOrders();
            if (pin)
            {
                ShiftPinnedBelowNewTopPin();
            }

            var item = new QuickCaptureItem
            {
                Body = "Image",
                ContentFormat = TextContentFormat.PlainText,
                Type = QuickCaptureItemType.Image,
                ImagePath = cachedImagePath,
                ContentHash = contentHash,
                Attachments = [CreateImageAttachment(cachedImagePath, now)],
                SourceKind = QuickCaptureSourceKind.Image,
                IsPinned = pin,
                IsRecent = false,
                SortOrder = NextSortOrderForInsertAtTop(),
                PinnedSortOrder = pin ? 0 : -1,
                CreatedAt = now,
                UpdatedAt = now
            };

            _data!.Items.Insert(0, item);
            await SaveHostCoreAsync(notify: false);
            await PersistOrderChangesCoreAsync(before);
            RaiseChanged(QuickCaptureChangeKind.ItemsChanged);
            CleanupUnusedImageCacheCore();
            return Clone(item);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> CreateImageExportFileAsync(QuickCaptureItem item, string? fileNamePrefix = null)
    {
        if (item.Type != QuickCaptureItemType.Image ||
            string.IsNullOrWhiteSpace(item.ImagePath) ||
            !File.Exists(item.ImagePath))
        {
            return null;
        }

        Directory.CreateDirectory(_store.ExportDirectory);
        CleanupOldExportFiles();

        string exportFileName = BuildImageExportFileName(
            fileNamePrefix,
            item.UpdatedAt == default ? item.CreatedAt : item.UpdatedAt,
            item.ImagePath);
        string exportPath = FileService.GetAvailablePath(Path.Combine(_store.ExportDirectory, exportFileName));
        await Task.Run(() => File.Copy(item.ImagePath, exportPath));
        return exportPath;
    }

    public async Task<QuickCaptureItem?> SaveRecentItemToRecordsAsync(string recentItemId, bool pin)
    {
        if (string.IsNullOrWhiteSpace(recentItemId))
        {
            return null;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var recentItem = _data!.RecentItems.FirstOrDefault(entry =>
                string.Equals(entry.Id, recentItemId, StringComparison.Ordinal) &&
                !entry.IsDeleted);
            if (recentItem is null ||
                (string.IsNullOrWhiteSpace(recentItem.Body) &&
                 string.IsNullOrWhiteSpace(recentItem.ImagePath)))
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            var before = SnapshotOrders();
            if (pin)
            {
                ShiftPinnedBelowNewTopPin();
            }

            var item = new QuickCaptureItem
            {
                Body = recentItem.Body,
                ContentFormat = TextContentFormat.PlainText,
                Title = recentItem.Title,
                Type = recentItem.Type,
                Url = recentItem.Url,
                ImagePath = recentItem.ImagePath,
                ContentHash = recentItem.ContentHash,
                Attachments = recentItem.Attachments.Select(attachment => attachment.Clone()).ToList(),
                SourceKind = QuickCaptureSourceKind.Clipboard,
                IsPinned = pin,
                IsRecent = false,
                SortOrder = NextSortOrderForInsertAtTop(),
                PinnedSortOrder = pin ? 0 : -1,
                CreatedAt = now,
                UpdatedAt = now
            };
            if (QualifiesAsNote(item))
            {
                item.IsNote = true;
            }

            if (item.IsNote)
            {
                _notes[item.Id] = item;
                await SaveItemAsync(item, notify: false);
            }
            else
            {
                _data.Items.Insert(0, item);
                await SaveHostCoreAsync(notify: false);
            }

            await PersistOrderChangesCoreAsync(before);
            RaiseChanged(QuickCaptureChangeKind.ItemsChanged);
            return Clone(item);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> SetPinnedAsync(string itemId, bool isPinned)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var item = FindItem(itemId);
            if (item is null || item.IsDeleted || item.IsPinned == isPinned)
            {
                return false;
            }

            var before = SnapshotOrders();
            item.IsPinned = isPinned;
            if (isPinned)
            {
                ShiftPinnedBelowNewTopPin();
                item.PinnedSortOrder = 0;
            }
            else
            {
                item.PinnedSortOrder = -1;
            }

            item.UpdatedAt = DateTimeOffset.UtcNow;
            await PersistOrderChangesAsync(before);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> SetPinnedAsync(IEnumerable<string> itemIds, bool isPinned)
    {
        var selectedIds = itemIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        if (selectedIds.Count == 0)
        {
            return 0;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var targets = MergedItemsCore()
                .Where(item => !item.IsDeleted && selectedIds.Contains(item.Id) && item.IsPinned != isPinned)
                .ToList();
            if (targets.Count == 0)
            {
                return 0;
            }

            var before = SnapshotOrders();
            var now = DateTimeOffset.UtcNow;
            foreach (var item in targets)
            {
                item.IsPinned = isPinned;
                item.PinnedSortOrder = isPinned ? 0 : -1;
                item.UpdatedAt = now;
            }

            if (isPinned)
            {
                var targetIdSet = targets.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
                var pinnedOrder = targets
                    .Concat(MergedItemsCore()
                        .Where(item => item.IsPinned && !targetIdSet.Contains(item.Id))
                        .OrderBy(item => item.PinnedSortOrder))
                    .ToList();
                for (int index = 0; index < pinnedOrder.Count; index++)
                {
                    pinnedOrder[index].PinnedSortOrder = index;
                }
            }

            await PersistOrderChangesAsync(before);
            return targets.Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuickCaptureItem?> AddItemWithAttachmentsAsync(
        IEnumerable<string> filePaths,
        bool copyToManagedStorage,
        bool pin = false)
    {
        string[] paths = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0)
        {
            return null;
        }

        string itemId = Guid.NewGuid().ToString("N");
        var attachments = new List<TodoAttachment>();
        foreach (string path in paths)
        {
            TodoAttachment? attachment = await AttachmentStorageService.ImportPathAsync(
                path,
                Path.Combine(_store.AttachmentDirectory, itemId),
                copyToManagedStorage);
            if (attachment is not null)
            {
                attachments.Add(attachment);
            }
        }

        if (attachments.Count == 0)
        {
            return null;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var now = DateTimeOffset.UtcNow;
            var before = SnapshotOrders();
            if (pin)
            {
                ShiftPinnedBelowNewTopPin();
            }

            string? primaryImagePath = attachments
                .FirstOrDefault(attachment => string.Equals(attachment.Type, "image", StringComparison.OrdinalIgnoreCase))
                ?.FilePath;
            var item = new QuickCaptureItem
            {
                Id = itemId,
                Body = string.Join(", ", attachments.Select(attachment => attachment.DisplayName)),
                ContentFormat = TextContentFormat.PlainText,
                Type = primaryImagePath is null ? QuickCaptureItemType.Text : QuickCaptureItemType.Image,
                ImagePath = primaryImagePath,
                Attachments = attachments,
                SourceKind = QuickCaptureSourceKind.DragDrop,
                IsPinned = pin,
                SortOrder = NextSortOrderForInsertAtTop(),
                PinnedSortOrder = pin ? 0 : -1,
                CreatedAt = now,
                UpdatedAt = now
            };
            if (QualifiesAsNote(item))
            {
                item.IsNote = true;
            }

            if (item.IsNote)
            {
                _notes[item.Id] = item;
                await SaveItemAsync(item, notify: false);
            }
            else
            {
                _data!.Items.Insert(0, item);
                await SaveHostCoreAsync(notify: false);
            }

            await PersistOrderChangesCoreAsync(before);
            RaiseChanged(QuickCaptureChangeKind.ItemsChanged);
            return Clone(item);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuickCaptureItem?> AddAttachmentsAsync(
        string itemId,
        IEnumerable<string> filePaths,
        bool copyToManagedStorage)
    {
        string[] paths = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (string.IsNullOrWhiteSpace(itemId) || paths.Length == 0)
        {
            return null;
        }

        var imported = new List<TodoAttachment>();
        foreach (string path in paths)
        {
            TodoAttachment? attachment = await AttachmentStorageService.ImportPathAsync(
                path,
                Path.Combine(_store.AttachmentDirectory, itemId),
                copyToManagedStorage);
            if (attachment is not null)
            {
                imported.Add(attachment);
            }
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            QuickCaptureItem? item = FindItem(itemId);
            if (item is null || item.IsDeleted)
            {
                return null;
            }

            foreach (TodoAttachment attachment in imported)
            {
                if (!item.Attachments.Any(existing =>
                        string.Equals(existing.FilePath, attachment.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    item.Attachments.Add(attachment);
                }
            }

            item.ImagePath ??= item.Attachments
                .FirstOrDefault(attachment => string.Equals(attachment.Type, "image", StringComparison.OrdinalIgnoreCase))
                ?.FilePath;
            if (!string.IsNullOrWhiteSpace(item.ImagePath))
            {
                item.Type = QuickCaptureItemType.Image;
            }
            item.UpdatedAt = DateTimeOffset.UtcNow;
            bool saved = await SaveItemAsync(item);
            return saved ? Clone(item) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuickCaptureItem?> DeleteAttachmentAsync(string itemId, string attachmentId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(attachmentId))
        {
            return null;
        }

        TodoAttachment? removedAttachment = null;
        bool removalPersisted = false;
        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            QuickCaptureItem? item = FindItem(itemId);
            if (item is null || item.IsDeleted)
            {
                return null;
            }

            removedAttachment = item.Attachments.FirstOrDefault(attachment =>
                string.Equals(attachment.Id, attachmentId, StringComparison.Ordinal));
            if (removedAttachment is null)
            {
                return Clone(item);
            }

            item.Attachments.Remove(removedAttachment);
            if (string.Equals(item.ImagePath, removedAttachment.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                item.ImagePath = item.Attachments
                    .FirstOrDefault(attachment =>
                        string.Equals(attachment.Type, "image", StringComparison.OrdinalIgnoreCase))
                    ?.FilePath;
                item.ContentHash = null;
                if (string.IsNullOrWhiteSpace(item.ImagePath))
                {
                    item.Type = TryDetectUrl(item.Body, out string? url)
                        ? QuickCaptureItemType.Link
                        : QuickCaptureItemType.Text;
                    item.Url = url;
                }
            }

            item.UpdatedAt = DateTimeOffset.UtcNow;
            removalPersisted = await SaveItemAsync(item);
            CleanupUnusedImageCacheCore();
            return removalPersisted ? Clone(item) : null;
        }
        finally
        {
            _gate.Release();
            if (removalPersisted &&
                removedAttachment is { IsManagedCopy: true } &&
                !IsPathInsideDirectory(removedAttachment.FilePath, _store.ImageDirectory))
            {
                // Files in the shared image cache stay owned by the refcount
                // GC above; anything else is deleted only when provably
                // inside the managed attachment tree, through the recycle
                // kernel (containment gate inside TryDeleteManagedCopyAsync).
                await AttachmentStorageService.TryDeleteManagedCopyAsync(
                    removedAttachment.FilePath,
                    _store.AttachmentDirectory,
                    _recycleAttachmentFileAsync);
            }
        }
    }

    /// <summary>
    /// Close or reopen a quick-panel tab without changing the note or its visibility in other hosts.
    /// </summary>
    public async Task<bool> SetItemClosedAsync(string itemId, bool isClosed)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var item = FindItem(itemId);
            var closed = _data!.ClosedQuickPanelNotes ??= [];
            if (item is null || closed.ContainsKey(itemId) == isClosed)
            {
                return false;
            }

            if (isClosed) closed[itemId] = DateTimeOffset.UtcNow;
            else closed.Remove(itemId);
            await SaveHostCoreAsync(kind: QuickCaptureChangeKind.HostStateChanged);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> MovePinnedItemAsync(string itemId, int direction)
    {
        if (string.IsNullOrWhiteSpace(itemId) || direction == 0)
        {
            return false;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var pinnedItems = OrderedPinnedItemsCore();

            int currentIndex = pinnedItems.FindIndex(item => string.Equals(item.Id, itemId, StringComparison.Ordinal));
            if (currentIndex < 0)
            {
                return false;
            }

            int targetIndex = currentIndex + (direction < 0 ? -1 : 1);
            if (targetIndex < 0 || targetIndex >= pinnedItems.Count)
            {
                return false;
            }

            var before = SnapshotOrders();
            (pinnedItems[currentIndex].PinnedSortOrder, pinnedItems[targetIndex].PinnedSortOrder) =
                (pinnedItems[targetIndex].PinnedSortOrder, pinnedItems[currentIndex].PinnedSortOrder);

            await PersistOrderChangesAsync(before);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuickCaptureDeletedItemSnapshot?> DeleteItemAsync(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var item = FindItem(itemId);
            if (item is null)
            {
                return null;
            }

            var deletedItem = Clone(item);
            // Tombstone, not physical removal: a merge restore treats an
            // absent id as "unknown to this device" and would resurrect the
            // item. Notes keep their minimal tombstone in their own file;
            // records keep it in the aggregate store. Undo restores from the
            // in-memory snapshot, not from the store.
            await ReplaceWithTombstoneAsync(item, DateTimeOffset.UtcNow, isRecent: false);
            return deletedItem is null ? null : new QuickCaptureDeletedItemSnapshot(deletedItem, IsRecent: false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<QuickCaptureDeletedItemSnapshot>> DeleteItemsAsync(
        IEnumerable<string> itemIds,
        bool isRecent)
    {
        var selectedIds = itemIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        if (selectedIds.Count == 0)
        {
            return [];
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            List<QuickCaptureItem> itemsToDelete = (isRecent ? _data!.RecentItems : MergedItemsCore())
                .Where(item => selectedIds.Contains(item.Id))
                .ToList();
            if (itemsToDelete.Count == 0)
            {
                return [];
            }

            var snapshots = itemsToDelete
                .Select(item => new QuickCaptureDeletedItemSnapshot(Clone(item), isRecent))
                .ToList();
            var deletedAt = DateTimeOffset.UtcNow;
            foreach (var item in itemsToDelete)
            {
                await ReplaceWithTombstoneAsync(item, deletedAt, isRecent);
            }

            return snapshots;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuickCaptureDeletedItemSnapshot?> DeleteRecentItemAsync(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var item = _data!.RecentItems.FirstOrDefault(entry =>
                string.Equals(entry.Id, itemId, StringComparison.Ordinal));
            if (item is null)
            {
                return null;
            }

            var deletedItem = Clone(item)!;
            // Content-stripped tombstone (see DeleteItemAsync).
            await ReplaceWithTombstoneAsync(item, DateTimeOffset.UtcNow, isRecent: true);
            return new QuickCaptureDeletedItemSnapshot(deletedItem, IsRecent: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RestoreDeletedItemAsync(QuickCaptureDeletedItemSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return false;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            if (FindItemIncludingDeleted(snapshot.Item.Id) is { } existing)
            {
                if (!existing.IsDeleted)
                {
                    return false;
                }

                // Undo resurrects the tombstone left by the delete — the
                // refreshed timestamp also wins any later merge conflict.
                var resurrected = Clone(snapshot.Item)!;
                resurrected.IsDeleted = false;
                resurrected.UpdatedAt = DateTimeOffset.UtcNow;
                if (_notes.ContainsKey(existing.Id))
                {
                    _notes[existing.Id] = resurrected;
                    await SaveItemAsync(resurrected, notify: false);
                }
                else
                {
                    int existingIndex = _data!.Items.IndexOf(existing);
                    _data.Items[existingIndex] = resurrected;
                    await SaveHostCoreAsync(notify: false);
                }

                RaiseChanged(QuickCaptureChangeKind.ItemsChanged);
                return true;
            }

            var item = Clone(snapshot.Item)!;
            item.IsDeleted = false;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            if (!snapshot.IsRecent && QualifiesAsNote(item))
            {
                item.IsNote = true;
                _notes[item.Id] = item;
                await SaveItemAsync(item, notify: false);
            }
            else
            {
                int insertIndex = Math.Clamp(item.SortOrder, 0, _data!.Items.Count);
                _data.Items.Insert(insertIndex, item);
                await SaveHostCoreAsync(notify: false);
            }

            RaiseChanged(QuickCaptureChangeKind.ItemsChanged);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            // Stub tombstones keep the wipe winning merge restores without
            // retaining cleared content in note files or the store file.
            var clearedAt = DateTimeOffset.UtcNow;
            foreach (var note in _notes.Values.ToList())
            {
                if (note.IsDeleted)
                {
                    continue;
                }

                _notes[note.Id] = CreateTombstoneStub(note, clearedAt, isRecent: false, isNote: true);
                await SaveItemAsync(_notes[note.Id], notify: false);
            }

            _data!.Items = _data.Items
                .Select(item => item.IsDeleted ? item : CreateTombstoneStub(item, clearedAt, isRecent: false, isNote: false))
                .ToList();
            _data.RecentItems = _data.RecentItems
                .Select(item => item.IsDeleted ? item : CreateTombstoneStub(item, clearedAt, isRecent: true, isNote: false))
                .ToList();
            await SaveHostCoreAsync();
            CleanupUnusedImageCacheCore();
            TryDeleteDirectory(_store.AttachmentDirectory);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearRecentAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var clearedAt = DateTimeOffset.UtcNow;
            _data!.RecentItems = _data.RecentItems
                .Select(item => item.IsDeleted ? item : CreateTombstoneStub(item, clearedAt, isRecent: true, isNote: false))
                .ToList();
            await SaveHostCoreAsync(kind: QuickCaptureChangeKind.ClipboardChanged);
            CleanupUnusedImageCacheCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task TrimRecentItemsAsync(int maxRecentItems,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedCoreAsync();
            cancellationToken.ThrowIfCancellationRequested();
            int before = _data!.RecentItems.Count;
            TrimRecentItemsCore(NormalizeRecentLimit(maxRecentItems));
            if (_data.RecentItems.Count != before)
            {
                await SaveHostCoreAsync(kind: QuickCaptureChangeKind.ClipboardChanged);
                CleanupUnusedImageCacheCore();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetCurrentViewAsync(QuickCaptureViewMode viewMode)
    {
        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            if (_data!.CurrentView == viewMode)
            {
                return;
            }

            _data.CurrentView = viewMode;
            await SaveHostCoreAsync(notify: false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuickCaptureImageCacheInfo> GetImageCacheInfoAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            return GetImageCacheInfoCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuickCaptureImageCacheCleanupResult> CleanupUnusedImageCacheAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            return CleanupUnusedImageCacheCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<string?> GetOrCreateImageThumbnailPathAsync(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return Task.FromResult<string?>(null);
        }

        string normalizedPath = Path.GetFullPath(imagePath);
        if (!File.Exists(normalizedPath) || !IsImageFile(normalizedPath))
        {
            return Task.FromResult<string?>(null);
        }

        string thumbnailPath = GetThumbnailPath(normalizedPath);
        if (File.Exists(thumbnailPath))
        {
            return Task.FromResult<string?>(thumbnailPath);
        }

        lock (_thumbnailTaskLock)
        {
            if (_thumbnailTasks.TryGetValue(normalizedPath, out var existingTask))
            {
                return existingTask;
            }

            var task = CreateImageThumbnailAndRemoveTaskAsync(normalizedPath);
            _thumbnailTasks[normalizedPath] = task;
            return task;
        }
    }

    public async Task<bool> MovePinnedItemToIndexAsync(string itemId, int targetIndex)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var pinnedItems = OrderedPinnedItemsCore();
            int currentIndex = pinnedItems.FindIndex(item =>
                string.Equals(item.Id, itemId, StringComparison.Ordinal));
            if (currentIndex < 0)
            {
                return false;
            }

            var before = SnapshotOrders();
            QuickCaptureItem movedItem = pinnedItems[currentIndex];
            pinnedItems.RemoveAt(currentIndex);
            pinnedItems.Insert(Math.Clamp(targetIndex, 0, pinnedItems.Count), movedItem);
            for (int index = 0; index < pinnedItems.Count; index++)
            {
                pinnedItems[index].PinnedSortOrder = index;
            }

            await PersistOrderChangesAsync(before);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuickCaptureItem?> ReplaceItemImageAsync(string itemId, string imagePath)
    {
        if (string.IsNullOrWhiteSpace(itemId) ||
            string.IsNullOrWhiteSpace(imagePath) ||
            !File.Exists(imagePath) ||
            !IsImageFile(imagePath))
        {
            return null;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var item = FindItem(itemId);
            if (item is null || item.IsDeleted)
            {
                return null;
            }

            string cachedImagePath = await SaveImageFileAsync(imagePath);
            string contentHash = Path.GetFileNameWithoutExtension(cachedImagePath);
            TodoAttachment? primaryImageAttachment = item.Attachments.FirstOrDefault(attachment =>
                string.Equals(attachment.FilePath, item.ImagePath, StringComparison.OrdinalIgnoreCase));
            if (primaryImageAttachment is null)
            {
                item.Attachments.Insert(0, CreateImageAttachment(cachedImagePath, DateTimeOffset.UtcNow));
            }
            else
            {
                primaryImageAttachment.FilePath = cachedImagePath;
                primaryImageAttachment.DisplayName = Path.GetFileName(cachedImagePath);
                primaryImageAttachment.Type = "image";
                primaryImageAttachment.StorageMode = TodoAttachment.ManagedStorageMode;
            }

            item.Type = QuickCaptureItemType.Image;
            item.ImagePath = cachedImagePath;
            item.ContentHash = contentHash;
            item.SourceKind = QuickCaptureSourceKind.Image;
            item.Url = null;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            bool saved = await SaveItemAsync(item);
            CleanupUnusedImageCacheCore();
            return saved ? Clone(item) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> MoveItemAsync(string itemId, int targetIndex)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var items = MergedItemsCore()
                .Where(item => !item.IsDeleted)
                .ToList();
            int currentIndex = items.FindIndex(item => string.Equals(item.Id, itemId, StringComparison.Ordinal));
            if (currentIndex < 0)
            {
                return false;
            }

            var before = SnapshotOrders();
            QuickCaptureItem movedItem = items[currentIndex];
            items.RemoveAt(currentIndex);
            items.Insert(Math.Clamp(targetIndex, 0, items.Count), movedItem);
            for (int index = 0; index < items.Count; index++)
            {
                items[index].SortOrder = index;
            }

            await PersistOrderChangesAsync(before);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> UpdateItemDetailsAsync(
        string itemId,
        string? title,
        string body,
        QuickCaptureAppearancePreset? appearancePreset = null,
        TextContentFormat? contentFormat = null)
    {
        QuickCaptureWriteResult result = await UpdateItemDetailsWithResultAsync(
            itemId,
            title,
            body,
            appearancePreset,
            contentFormat);
        return result.Saved;
    }

    public Task<QuickCaptureWriteResult> UpdateItemDetailsWithResultAsync(
        string itemId, string? title, string body,
        QuickCaptureAppearancePreset? appearancePreset = null, TextContentFormat? contentFormat = null) =>
        UpdateItemDetailsCoreAsync(itemId, title, body, appearancePreset, contentFormat, isNotePatch: false);

    /// <summary>Atomically applies only changed note fields; null leaves a field untouched.</summary>
    public Task<QuickCaptureWriteResult> PatchNoteAsync(string itemId, string? title = null, string? body = null,
        QuickCaptureAppearancePreset? appearancePreset = null, TextContentFormat? contentFormat = null) =>
        UpdateItemDetailsCoreAsync(itemId, title, body, appearancePreset, contentFormat, isNotePatch: true);

    private async Task<QuickCaptureWriteResult> UpdateItemDetailsCoreAsync(
        string itemId, string? title, string? body, QuickCaptureAppearancePreset? appearancePreset,
        TextContentFormat? contentFormat, bool isNotePatch)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return new QuickCaptureWriteResult(false, false, null);
        }

        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var item = FindItem(itemId);
            if (item is null || item.IsDeleted)
            {
                return new QuickCaptureWriteResult(false, false, null);
            }

            TextContentFormat effectiveFormat = NormalizeContentFormat(contentFormat ?? item.ContentFormat);
            string normalizedBody = NormalizeBody(isNotePatch && body is null ? item.Body : body, effectiveFormat);
            // Patch semantics: a null title keeps the stored title; explicit text (even empty) replaces it.
            string? effectiveTitle = title is null ? item.Title : NormalizeOptionalText(title);

            if (!isNotePatch && item.Type != QuickCaptureItemType.Image &&
                string.IsNullOrWhiteSpace(effectiveTitle) &&
                string.IsNullOrWhiteSpace(normalizedBody))
            {
                return new QuickCaptureWriteResult(false, false, null);
            }

            normalizedBody = await ExtractInlineMarkdownImagesAsync(
                item.Id,
                normalizedBody,
                effectiveFormat,
                item.Attachments);
            normalizedBody = TruncateBody(normalizedBody, out bool wasTruncated);
            if (isNotePatch) item.IsNote = true;
            item.Title = effectiveTitle;
            item.Body = normalizedBody;
            item.ContentFormat = effectiveFormat;
            if (item.Type != QuickCaptureItemType.Image)
            {
                item.Type = TryDetectUrl(normalizedBody, out string? url)
                    ? QuickCaptureItemType.Link
                    : QuickCaptureItemType.Text;
                item.Url = url;
            }

            if (appearancePreset is not null)
            {
                item.AppearancePreset = NormalizeAppearancePreset(appearancePreset.Value);
            }

            item.UpdatedAt = DateTimeOffset.UtcNow;
            if (isNotePatch && _notes.ContainsKey(item.Id))
            {
                // Hot path: one note file, one targeted notification; the
                // aggregate store is not read, re-serialized or touched.
                if (!await SaveItemAsync(item, notify: false))
                {
                    return new QuickCaptureWriteResult(false, wasTruncated, null);
                }

                RaiseChanged(QuickCaptureChangeKind.NoteSaved, [item.Id]);
            }
            else
            {
                if (!await SaveItemAsync(item, notify: false))
                {
                    return new QuickCaptureWriteResult(false, wasTruncated, null);
                }

                RaiseChanged(QuickCaptureChangeKind.ItemsChanged);
            }

            return new QuickCaptureWriteResult(true, wasTruncated, Clone(item));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> SetAppearanceAsync(
        IEnumerable<string> itemIds,
        QuickCaptureAppearancePreset appearancePreset)
    {
        var selectedIds = itemIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        if (selectedIds.Count == 0)
        {
            return 0;
        }

        QuickCaptureAppearancePreset normalizedPreset = NormalizeAppearancePreset(appearancePreset);
        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
            var targets = MergedItemsCore()
                .Where(item => !item.IsDeleted &&
                               selectedIds.Contains(item.Id) &&
                               item.AppearancePreset != normalizedPreset)
                .ToList();
            if (targets.Count == 0)
            {
                return 0;
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var item in targets)
            {
                item.AppearancePreset = normalizedPreset;
                item.UpdatedAt = now;
                await SaveItemAsync(item, notify: false);
            }

            RaiseChanged(QuickCaptureChangeKind.ItemsChanged);
            return targets.Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string?> CreateImageThumbnailAndRemoveTaskAsync(string imagePath)
    {
        try
        {
            return await CreateImageThumbnailAsync(imagePath);
        }
        finally
        {
            lock (_thumbnailTaskLock)
            {
                _thumbnailTasks.Remove(imagePath);
            }
        }
    }

    private async Task EnsureLoadedAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await EnsureLoadedCoreAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedCoreAsync()
    {
        if (_data is not null)
        {
            return;
        }

        QuickCaptureStoreData data = await _store.LoadAsync();
        if (data.NotesStorageVersion < QuickCaptureStore.CurrentNotesStorageVersion)
        {
            // One-way switch to independent note files; on any failure the
            // old aggregate stays readable and no store is half-migrated.
            data = await QuickCaptureNoteMigration.MigrateAsync(_store, _noteFiles, data);
        }

        _data = data;
        _notes = new Dictionary<string, QuickCaptureItem>(StringComparer.Ordinal);
        var fileErrors = new List<QuickCaptureNoteFileError>();
        foreach (NoteFileLoadResult load in await _noteFiles.LoadAllAsync())
        {
            if (load.Item is { } note &&
                load.Status is NoteFileLoadStatus.Loaded or NoteFileLoadStatus.NoHeader)
            {
                if (!_notes.TryAdd(note.Id, note))
                {
                    App.Log($"[QuickCaptureService] Duplicate note id '{note.Id}'; keeping the first file ({load.FileName}).");
                }

                continue;
            }

            fileErrors.Add(new QuickCaptureNoteFileError(
                load.NoteId ?? string.Empty,
                load.Status.ToString(),
                load.FileName,
                load.ErrorMessage));
            // A damaged file only invalidates its own note: the rest of the
            // library still loads, and the source file is never overwritten.
            App.Log($"[QuickCaptureService] Note '{load.FileName}' failed to load ({load.Status}): {load.ErrorMessage}");
        }

        _noteFileErrors = fileErrors;
        PruneNoteTombstoneFiles();

        // Legacy inline data-URI extraction once per load; each affected
        // note commits its own file, records go through one host save.
        bool hostDirty = false;
        foreach (QuickCaptureItem item in _data.Items.Concat(_notes.Values).Where(item => !item.IsDeleted).ToList())
        {
            string body = await ExtractInlineMarkdownImagesAsync(
                item.Id,
                item.Body,
                item.ContentFormat,
                item.Attachments);
            body = TruncateBody(body, out _);
            if (!string.Equals(body, item.Body, StringComparison.Ordinal))
            {
                item.Body = body;
                if (_notes.ContainsKey(item.Id))
                {
                    await SaveItemAsync(item, notify: false);
                }
                else
                {
                    hostDirty = true;
                }
            }
        }

        if (hostDirty)
        {
            await SaveHostCoreAsync(notify: false);
        }
    }

    private void PruneNoteTombstoneFiles()
    {
        var tombstones = _notes.Values.Where(note => note.IsDeleted)
            .OrderByDescending(note => note.UpdatedAt)
            .ToList();
        if (tombstones.Count <= MaxRetainedNoteTombstoneFiles)
        {
            return;
        }

        foreach (var stale in tombstones.Skip(MaxRetainedNoteTombstoneFiles))
        {
            _notes.Remove(stale.Id);
            _noteFiles.TryDeleteNoteFiles(stale.Id);
            App.Log($"[QuickCaptureService] Pruned note tombstone '{stale.Id}' beyond the retention cap.");
        }
    }

    private async Task ReplaceWithTombstoneAsync(QuickCaptureItem item, DateTimeOffset deletedAt, bool isRecent)
    {
        bool isNote = !isRecent && _notes.ContainsKey(item.Id);
        QuickCaptureItem stub = CreateTombstoneStub(item, deletedAt, isRecent, isNote || item.IsNote);
        if (isNote)
        {
            _notes[item.Id] = stub;
            await SaveItemAsync(stub, notify: false);
        }
        else
        {
            List<QuickCaptureItem> list = isRecent ? _data!.RecentItems : _data!.Items;
            int index = list.IndexOf(item);
            if (index >= 0)
            {
                list[index] = stub;
            }
            else
            {
                list.Add(stub);
            }

            await SaveHostCoreAsync(notify: false);
        }

        RaiseChanged(QuickCaptureChangeKind.ItemsChanged);
    }

    /// <summary>All note and record items in display order, live objects (not clones).</summary>
    private List<QuickCaptureItem> MergedItemsCore() =>
        _data!.Items
            .Concat(_notes.Values)
            .Where(item => item is not null)
            .OrderBy(item => item.SortOrder)
            .ThenByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();

    private List<QuickCaptureItem> OrderedPinnedItemsCore() =>
        MergedItemsCore()
            .Where(item => !item.IsDeleted && item.IsPinned)
            .OrderBy(item => item.PinnedSortOrder < 0 ? int.MaxValue : item.PinnedSortOrder)
            .ThenBy(item => item.SortOrder)
            .ThenByDescending(item => item.UpdatedAt)
            .ToList();

    private QuickCaptureItem? FindItem(string itemId) =>
        FindItemIncludingDeleted(itemId) is { } item && !item.IsDeleted ? item : null;

    private QuickCaptureItem? FindItemIncludingDeleted(string itemId)
    {
        if (_notes.TryGetValue(itemId, out QuickCaptureItem? note))
        {
            return note;
        }

        return _data!.Items.FirstOrDefault(entry => string.Equals(entry.Id, itemId, StringComparison.Ordinal));
    }

    private static bool QualifiesAsNote(QuickCaptureItem item) => NoteWorkspace.IsNote(item);

    private int NextSortOrderForInsertAtTop()
    {
        var merged = MergedItemsCore();
        return merged.Count == 0 ? 0 : merged.Min(item => item.SortOrder) - 1;
    }

    /// <summary>
    /// Pre-insert shift for a new top pin: existing pinned items move down
    /// so the newcomer takes slot 0. Pinned sets are small and pinning is
    /// never on the typing hot path, so shifting keeps pinned orders dense
    /// and non-negative — negative values are the "not pinned" sentinel.
    /// </summary>
    private void ShiftPinnedBelowNewTopPin()
    {
        foreach (var pinned in MergedItemsCore().Where(item => !item.IsDeleted && item.IsPinned))
        {
            pinned.PinnedSortOrder++;
        }
    }

    private Dictionary<string, (int SortOrder, int PinnedSortOrder)> SnapshotOrders() =>
        MergedItemsCore().ToDictionary(item => item.Id, item => (item.SortOrder, item.PinnedSortOrder));

    /// <summary>
    /// Persists only the items whose order values actually changed: note
    /// files one by one, records through a single host save.
    /// </summary>
    private async Task PersistOrderChangesAsync(Dictionary<string, (int SortOrder, int PinnedSortOrder)> before)
    {
        await PersistOrderChangesCoreAsync(before);
        RaiseChanged(QuickCaptureChangeKind.ItemsChanged);
    }

    private async Task PersistOrderChangesCoreAsync(Dictionary<string, (int SortOrder, int PinnedSortOrder)> before)
    {
        bool hostDirty = false;
        foreach (var item in MergedItemsCore())
        {
            if (!before.TryGetValue(item.Id, out var was))
            {
                continue;
            }

            if (was.SortOrder == item.SortOrder && was.PinnedSortOrder == item.PinnedSortOrder)
            {
                continue;
            }

            if (_notes.ContainsKey(item.Id))
            {
                await SaveItemAsync(item, notify: false);
            }
            else
            {
                hostDirty = true;
            }
        }

        if (hostDirty)
        {
            await SaveHostCoreAsync(notify: false);
        }
    }

    /// <summary>
    /// Persists one item through its owning store: note files commit a
    /// single document with a targeted notification; records ride the
    /// aggregate JSON save.
    /// </summary>
    private async Task<bool> SaveItemAsync(QuickCaptureItem item, bool notify = true)
    {
        if (_notes.ContainsKey(item.Id))
        {
            NoteFileCommitResult result = await _noteFiles.SaveAsync(item);
            if (result.Status == NoteFileCommitStatus.Conflict)
            {
                App.Log($"[QuickCaptureService] Note '{item.Id}' was not saved: {result.ErrorMessage}");
                if (notify) RaiseChanged(QuickCaptureChangeKind.ItemsChanged);
                return false;
            }

            if (notify) RaiseChanged(QuickCaptureChangeKind.NoteSaved, [item.Id]);
            return true;
        }

        await SaveHostCoreAsync(notify: notify);
        return true;
    }

    private async Task SaveHostCoreAsync(
        bool notify = true,
        QuickCaptureChangeKind kind = QuickCaptureChangeKind.ItemsChanged)
    {
        await _store.SaveAsync(_data!);
        if (notify)
        {
            RaiseChanged(kind);
        }
    }

    private void RaiseChanged(QuickCaptureChangeKind kind) => RaiseChanged(kind, Array.Empty<string>());

    private void RaiseChanged(QuickCaptureChangeKind kind, IReadOnlyList<string> noteIds)
    {
        ChangeDetailed?.Invoke(new QuickCaptureChange(kind, noteIds));
        if (kind != QuickCaptureChangeKind.NoteSaved)
        {
            Changed?.Invoke();
        }
    }

    private async Task<string> ExtractInlineMarkdownImagesAsync(
        string itemId,
        string body,
        TextContentFormat contentFormat,
        List<TodoAttachment> attachments)
    {
        if (contentFormat != TextContentFormat.Markdown || string.IsNullOrEmpty(body))
        {
            return body;
        }

        MarkdownInlineImageExtractionResult result = await MarkdownInlineImageExtractor.ExtractAsync(
            body,
            Path.Combine(_store.AttachmentDirectory, itemId));
        foreach (TodoAttachment attachment in result.Attachments)
        {
            if (!attachments.Any(existing =>
                    string.Equals(existing.Id, attachment.Id, StringComparison.Ordinal)))
            {
                attachments.Add(attachment);
            }
        }

        return result.Body;
    }

    private QuickCaptureImageCacheInfo GetImageCacheInfoCore()
    {
        return GetImageCacheInfoCore(GetReferencedImagePathsCore());
    }

    private QuickCaptureImageCacheCleanupResult CleanupUnusedImageCacheCore()
    {
        return CleanupUnusedImageCacheCore(GetReferencedImagePathsCore());
    }

    private static string NormalizeBody(
        string? body,
        TextContentFormat contentFormat = TextContentFormat.PlainText)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        string value = body.Replace("\0", string.Empty, StringComparison.Ordinal);
        return contentFormat == TextContentFormat.Markdown ? value : value.Trim();
    }

    private static string TruncateBody(string body, out bool wasTruncated)
    {
        wasTruncated = body.Length > MaxItemBodyCharacters;
        if (!wasTruncated)
        {
            return body;
        }

        int length = MaxItemBodyCharacters;
        if (char.IsHighSurrogate(body[length - 1]) && char.IsLowSurrogate(body[length]))
        {
            length--;
        }

        return body[..length];
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static QuickCaptureAppearancePreset NormalizeAppearancePreset(QuickCaptureAppearancePreset value)
    {
        return Enum.IsDefined(value) ? value : QuickCaptureAppearancePreset.Default;
    }

    private static TextContentFormat NormalizeContentFormat(TextContentFormat value) =>
        Enum.IsDefined(value) ? value : TextContentFormat.PlainText;

    public void MarkClipboardTextWrittenByWinSticker(string? body)
    {
        WinStickerClipboardWriteScope.MarkText(NormalizeBody(body));
    }

    public static int NormalizeRecentLimit(int value)
    {
        if (value < MinRecentLimit)
        {
            return DefaultRecentLimit;
        }

        return Math.Clamp(value, MinRecentLimit, MaxRecentLimit);
    }

    private bool ShouldIgnoreClipboardText(string body)
    {
        return WinStickerClipboardWriteScope.ShouldIgnoreText(body);
    }

    private static bool TryDetectUrl(string body, out string? url)
    {
        url = null;
        if (Uri.TryCreate(body.Trim(), UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
        {
            url = uri.AbsoluteUri;
            return true;
        }

        return false;
    }

    private HashSet<string> GetReferencedImagePathsCore()
    {
        return MergedItemsCore()
            .Concat(_data!.RecentItems)
            .Where(item => item is not null &&
                           !item.IsDeleted &&
                           item.Type == QuickCaptureItemType.Image &&
                           !string.IsNullOrWhiteSpace(item.ImagePath))
            .Select(item => NormalizePath(item.ImagePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private HashSet<string> GetReferencedThumbnailPathsCore(HashSet<string> referencedImagePaths)
    {
        return referencedImagePaths
            .Select(GetThumbnailPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private QuickCaptureImageCacheInfo GetImageCacheInfoCore(HashSet<string> referencedImagePaths)
    {
        if (!Directory.Exists(_store.ImageDirectory) && !Directory.Exists(_store.ThumbnailDirectory))
        {
            return new QuickCaptureImageCacheInfo(0, 0, 0, 0);
        }

        int totalFileCount = 0;
        long totalBytes = 0;
        int unusedFileCount = 0;
        long unusedBytes = 0;
        var referencedThumbnailPaths = GetReferencedThumbnailPathsCore(referencedImagePaths);

        foreach (var filePath in EnumerateCacheFiles(_store.ImageDirectory).Concat(EnumerateCacheFiles(_store.ThumbnailDirectory)))
        {
            var fileInfo = new FileInfo(filePath);
            totalFileCount++;
            totalBytes += fileInfo.Length;
            string normalizedFilePath = NormalizePath(filePath);
            bool isReferenced = referencedImagePaths.Contains(normalizedFilePath) ||
                referencedThumbnailPaths.Contains(normalizedFilePath);
            if (!isReferenced)
            {
                unusedFileCount++;
                unusedBytes += fileInfo.Length;
            }
        }

        return new QuickCaptureImageCacheInfo(totalFileCount, totalBytes, unusedFileCount, unusedBytes);
    }

    private QuickCaptureImageCacheCleanupResult CleanupUnusedImageCacheCore(HashSet<string> referencedImagePaths)
    {
        if (!Directory.Exists(_store.ImageDirectory) && !Directory.Exists(_store.ThumbnailDirectory))
        {
            return new QuickCaptureImageCacheCleanupResult(0, 0);
        }

        int deletedFileCount = 0;
        long deletedBytes = 0;
        var referencedThumbnailPaths = GetReferencedThumbnailPathsCore(referencedImagePaths);

        foreach (var filePath in EnumerateCacheFiles(_store.ImageDirectory).Concat(EnumerateCacheFiles(_store.ThumbnailDirectory)))
        {
            string normalizedFilePath = NormalizePath(filePath);
            if (referencedImagePaths.Contains(normalizedFilePath) ||
                referencedThumbnailPaths.Contains(normalizedFilePath))
            {
                continue;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                deletedBytes += fileInfo.Exists ? fileInfo.Length : 0;
                File.Delete(filePath);
                deletedFileCount++;
            }
            catch (Exception ex)
            {
                App.Log($"[QuickCaptureService] Failed to delete image cache file: {ex}");
            }
        }

        return new QuickCaptureImageCacheCleanupResult(deletedFileCount, deletedBytes);
    }

    private static IEnumerable<string> EnumerateCacheFiles(string directory)
    {
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).ToList()
            : [];
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string trimmed = path.Trim();
        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch
        {
            return trimmed;
        }
    }

    /// <summary>
    /// The consumer-facing projection: notes and records merged in display
    /// order. SortOrder values in files stay sparse; this clone renumbers
    /// them to dense list positions because every existing consumer treats
    /// SortOrder as the position within Items.
    /// </summary>
    private QuickCaptureStoreData CloneWithMergedItems()
    {
        var data = Clone(_data!);
        List<QuickCaptureItem> merged = MergedItemsCore().Select(Clone).ToList();
        for (int index = 0; index < merged.Count; index++)
        {
            merged[index].SortOrder = index;
        }

        NormalizePinnedSortOrders(merged);
        data.Items = merged;
        return data;
    }

    private static QuickCaptureStoreData Clone(QuickCaptureStoreData data)
    {
        return new QuickCaptureStoreData
        {
            Version = data.Version,
            NotesStorageVersion = data.NotesStorageVersion,
            CurrentView = data.CurrentView,
            ClosedQuickPanelNotes = data.ClosedQuickPanelNotes is null ? null : new(data.ClosedQuickPanelNotes, StringComparer.Ordinal),
            Items = data.Items.Select(Clone).ToList(),
            RecentItems = data.RecentItems.Select(Clone).ToList()
        };
    }

    private static QuickCaptureItem? Clone(QuickCaptureItem? item)
    {
        if (item is null)
        {
            return null;
        }

        return new QuickCaptureItem
        {
            Id = item.Id,
            Type = item.Type,
            Body = item.Body,
            ContentFormat = item.ContentFormat,
            Title = item.Title,
            IsNote = item.IsNote,
            Url = item.Url,
            ImagePath = item.ImagePath,
            ContentHash = item.ContentHash,
            IsPinned = item.IsPinned,
            IsRecent = item.IsRecent,
            IsDeleted = item.IsDeleted,
            AppearancePreset = item.AppearancePreset,
            SourceKind = item.SourceKind,
            Tags = item.Tags is null ? [] : [.. item.Tags],
            Attachments = item.Attachments?.Select(attachment => attachment.Clone()).ToList() ?? [],
            ArchivedAt = item.ArchivedAt,
            SortOrder = item.SortOrder,
            PinnedSortOrder = item.PinnedSortOrder,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    /// <summary>
    /// A tombstone stripped of user content: keeps the id + timestamps so a
    /// merge restore cannot resurrect a deleted or wiped item, while the
    /// payload actually leaves the store file (and any cloud backup of it).
    /// Undo restores from the in-memory delete snapshot, not from this stub.
    /// </summary>
    private static QuickCaptureItem CreateTombstoneStub(
        QuickCaptureItem item,
        DateTimeOffset deletedAt,
        bool isRecent,
        bool isNote) =>
        new()
        {
            Id = item.Id,
            IsDeleted = true,
            IsNote = isNote,
            IsRecent = isRecent,
            SortOrder = item.SortOrder,
            CreatedAt = item.CreatedAt,
            UpdatedAt = deletedAt
        };

    private void TrimRecentItemsCore(int maxRecentItems)
    {
        // Only live entries compete for the recent window; tombstones are
        // kept outside it so a trim can never drop delete protection.
        var liveItems = _data!.RecentItems
            .Where(item => !item.IsDeleted &&
                           (!string.IsNullOrWhiteSpace(item.Body) ||
                            (item.Type == QuickCaptureItemType.Image && !string.IsNullOrWhiteSpace(item.ImagePath))))
            .OrderBy(item => item.SortOrder)
            .ThenByDescending(item => item.UpdatedAt)
            .Take(maxRecentItems)
            .ToList();
        var tombstoneItems = _data.RecentItems
            .Where(item => item is not null && item.IsDeleted)
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.UpdatedAt).First())
            .ToList();

        _data.RecentItems = liveItems.Concat(tombstoneItems).ToList();

        NormalizeSortOrders(_data.RecentItems);
    }

    private async Task<string> SaveImageAsync(byte[] imagePngBytes, string contentHash)
    {
        Directory.CreateDirectory(_store.ImageDirectory);
        string imagePath = Path.Combine(_store.ImageDirectory, $"{contentHash}.png");
        if (!File.Exists(imagePath))
        {
            await File.WriteAllBytesAsync(imagePath, imagePngBytes);
        }

        await CreateImageThumbnailAsync(imagePath);
        return imagePath;
    }

    private static TodoAttachment CreateImageAttachment(string imagePath, DateTimeOffset addedAt)
    {
        return new TodoAttachment
        {
            FilePath = imagePath,
            DisplayName = Path.GetFileName(imagePath),
            Type = "image",
            StorageMode = TodoAttachment.ManagedStorageMode,
            AddedAt = addedAt
        };
    }

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        string normalizedPath = Path.GetFullPath(path);
        string normalizedDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            App.Log($"[QuickCaptureService] Failed to clear attachment directory: {ex.Message}");
        }
    }

    private async Task<string> SaveImageFileAsync(string sourceImagePath)
    {
        byte[] bytes = await File.ReadAllBytesAsync(sourceImagePath);
        string contentHash = ComputeContentHash(bytes);
        string extension = NormalizeImageExtension(sourceImagePath);
        Directory.CreateDirectory(_store.ImageDirectory);
        string imagePath = Path.Combine(_store.ImageDirectory, $"{contentHash}{extension}");
        if (!File.Exists(imagePath))
        {
            await File.WriteAllBytesAsync(imagePath, bytes);
        }

        await CreateImageThumbnailAsync(imagePath);
        return imagePath;
    }

    private async Task<string?> CreateImageThumbnailAsync(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) ||
            !File.Exists(imagePath) ||
            !IsImageFile(imagePath))
        {
            return null;
        }

        string thumbnailPath = GetThumbnailPath(imagePath);
        if (File.Exists(thumbnailPath))
        {
            return thumbnailPath;
        }

        try
        {
            Directory.CreateDirectory(_store.ThumbnailDirectory);
            var file = await StorageFile.GetFileFromPathAsync(imagePath);
            using IRandomAccessStream inputStream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(inputStream);
            var transform = new BitmapTransform
            {
                InterpolationMode = BitmapInterpolationMode.Fant
            };

            if (decoder.PixelWidth >= decoder.PixelHeight)
            {
                transform.ScaledWidth = Math.Min(decoder.PixelWidth, ThumbnailMaxPixelSize);
                transform.ScaledHeight = Math.Max(1, (uint)Math.Round(decoder.PixelHeight * (transform.ScaledWidth / (double)decoder.PixelWidth)));
            }
            else
            {
                transform.ScaledHeight = Math.Min(decoder.PixelHeight, ThumbnailMaxPixelSize);
                transform.ScaledWidth = Math.Max(1, (uint)Math.Round(decoder.PixelWidth * (transform.ScaledHeight / (double)decoder.PixelHeight)));
            }

            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb);

            string tempPath = $"{thumbnailPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var outputFileStream = File.Open(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    using IRandomAccessStream outputStream = outputFileStream.AsRandomAccessStream();
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
                    encoder.SetSoftwareBitmap(softwareBitmap);
                    await encoder.FlushAsync();
                }

                if (File.Exists(thumbnailPath))
                {
                    File.Delete(tempPath);
                }
                else
                {
                    File.Move(tempPath, thumbnailPath);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }

            return thumbnailPath;
        }
        catch (Exception ex)
        {
            App.Log($"[QuickCaptureService] Failed to create thumbnail for '{imagePath}': {ex}");
            return File.Exists(thumbnailPath) ? thumbnailPath : null;
        }
    }

    private string GetThumbnailPath(string imagePath)
    {
        string hash = !string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(imagePath))
            ? Path.GetFileNameWithoutExtension(imagePath)
            : ComputeContentHash(File.ReadAllBytes(imagePath));
        return Path.Combine(_store.ThumbnailDirectory, $"{hash}.png");
    }

    private static string ComputeContentHash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    internal static string BuildImageExportFileName(
        string? fileNamePrefix,
        DateTimeOffset timestamp,
        string sourceImagePath)
    {
        string prefix = FileService.SanitizeFileSystemName(fileNamePrefix);
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "Capture";
        }

        string extension = NormalizeImageExtension(sourceImagePath);
        return $"{prefix} {timestamp.ToLocalTime():yyyy-MM-dd HH-mm-ss}{extension}";
    }

    private void CleanupOldExportFiles()
    {
        if (!Directory.Exists(_store.ExportDirectory))
        {
            return;
        }

        DateTime cutoffUtc = DateTime.UtcNow - ExportCleanupAge;
        foreach (string filePath in Directory.EnumerateFiles(_store.ExportDirectory, "*", SearchOption.TopDirectoryOnly).ToList())
        {
            try
            {
                if (File.GetLastWriteTimeUtc(filePath) < cutoffUtc)
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                App.Log($"[QuickCaptureService] Failed to clean image export file: {ex}");
            }
        }
    }

    private static bool IsImageFile(string? path)
    {
        string extension = string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeImageExtension(string? path)
    {
        string extension = string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetExtension(path);
        return IsImageFile(path)
            ? extension.ToLowerInvariant()
            : ".png";
    }

    private static void NormalizeSortOrders(List<QuickCaptureItem> items)
    {
        for (int index = 0; index < items.Count; index++)
        {
            items[index].SortOrder = index;
        }
    }

    private static void NormalizePinnedSortOrders(List<QuickCaptureItem> items)
    {
        var pinnedItems = items
            .Where(item => !item.IsDeleted && item.IsPinned)
            .OrderBy(item => item.PinnedSortOrder < 0 ? int.MaxValue : item.PinnedSortOrder)
            .ThenBy(item => item.SortOrder)
            .ThenByDescending(item => item.UpdatedAt)
            .ToList();

        for (int index = 0; index < pinnedItems.Count; index++)
        {
            pinnedItems[index].PinnedSortOrder = index;
        }

        foreach (var item in items.Where(item => !item.IsPinned))
        {
            item.PinnedSortOrder = -1;
        }
    }
}
