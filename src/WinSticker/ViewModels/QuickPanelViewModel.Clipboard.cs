using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSticker.Models;
using WinSticker.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace WinSticker.ViewModels;

public sealed partial class QuickPanelViewModel
{
    [ObservableProperty]
    private QuickPanelFilter _clipboardFilter = QuickPanelFilter.All;

    [ObservableProperty]
    private QuickCaptureItem? _selectedClipboardItem;

    [ObservableProperty]
    private string _clipboardCountText = "0 条记录";

    public ObservableCollection<QuickCaptureItem> FilteredClipboardItems { get; } = [];

    private readonly object _clipboardProjectionLock = new();
    private List<QuickCaptureItem> _allClipboardItems = [];

    private void InitializeClipboard()
    {
        _quickCaptureService.Changed += OnQuickCaptureServiceChanged;
    }

    private void DisposeClipboard()
    {
        _quickCaptureService.Changed -= OnQuickCaptureServiceChanged;
    }

    private void OnQuickCaptureServiceChanged()
    {
        RunOnUi(() => _ = LoadClipboardHistoryAsync());
    }

    public async Task LoadClipboardHistoryAsync()
    {
        try
        {
            QuickCaptureStoreData data = await _quickCaptureService.GetDataAsync();
            var items = new List<QuickCaptureItem>();

            // RecentItems represents clipboard captures; Pinned items are in Items.Where(i => i.IsPinned).
            if (data.RecentItems is { Count: > 0 })
            {
                items.AddRange(data.RecentItems.Where(item => !item.IsDeleted));
            }

            // Also check for pinned records in Items
            if (data.Items is { Count: > 0 })
            {
                var ids = items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
                foreach (var item in data.Items.Where(i => i.IsPinned && !i.IsDeleted))
                {
                    if (ids.Add(item.Id))
                    {
                        items.Add(item);
                    }
                }
            }

            RunOnUi(() =>
            {
                lock (_clipboardProjectionLock)
                {
                    // The store returns clones. Keep the displayed objects for
                    // unchanged records so their templates and images survive.
                    var previous = _allClipboardItems.ToDictionary(item => item.Id, StringComparer.Ordinal);
                    for (int index = 0; index < items.Count; index++)
                    {
                        if (previous.TryGetValue(items[index].Id, out var existing) &&
                            HasSameClipboardPresentation(existing, items[index]))
                            items[index] = existing;
                    }
                    _allClipboardItems = items;
                    ApplyClipboardFilter();
                }
            });
        }
        catch (Exception ex)
        {
            App.Log($"[QuickPanel] Failed to load clipboard history: {ex.Message}");
        }
    }

    private static bool HasSameClipboardPresentation(QuickCaptureItem left, QuickCaptureItem right) =>
        left.Type == right.Type && left.Body == right.Body && left.Title == right.Title &&
        left.Url == right.Url && left.ImagePath == right.ImagePath && left.ContentHash == right.ContentHash &&
        left.IsPinned == right.IsPinned && left.IsRecent == right.IsRecent &&
        left.CreatedAt == right.CreatedAt && left.UpdatedAt == right.UpdatedAt &&
        left.Attachments.Select(file => (file.Id, file.FilePath))
            .SequenceEqual(right.Attachments.Select(file => (file.Id, file.FilePath)));

    [RelayCommand]
    public void SetClipboardFilter(QuickPanelFilter filter)
    {
        ClipboardFilter = filter;
        ApplyClipboardFilter();
    }

    public void ApplyClipboardFilter()
    {
        RunOnUi(() =>
        {
            lock (_clipboardProjectionLock)
            {
            IEnumerable<QuickCaptureItem> query = _allClipboardItems;

            // Apply category filter
            query = ClipboardFilter switch
            {
                QuickPanelFilter.Text => query.Where(item => item.Type == QuickCaptureItemType.Text),
                QuickPanelFilter.Images => query.Where(item => item.Type == QuickCaptureItemType.Image || !string.IsNullOrEmpty(item.ImagePath)),
                QuickPanelFilter.Links => query.Where(item => item.Type == QuickCaptureItemType.Link || !string.IsNullOrEmpty(item.Url)),
                QuickPanelFilter.Favorites => query.Where(item => item.IsPinned),
                _ => query
            };

            // Apply search query
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.Trim();
                query = query.Where(item =>
                    (!string.IsNullOrEmpty(item.Body) && item.Body.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(item.Title) && item.Title.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(item.Url) && item.Url.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            var results = query.OrderByDescending(item => item.CreatedAt).ToList();

            var ids = results.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            for (int index = FilteredClipboardItems.Count - 1; index >= 0; index--)
            {
                if (!ids.Contains(FilteredClipboardItems[index].Id))
                    FilteredClipboardItems.RemoveAt(index);
            }
            for (int index = 0; index < results.Count; index++)
            {
                var item = results[index];
                if (index < FilteredClipboardItems.Count && FilteredClipboardItems[index].Id == item.Id)
                {
                    if (!ReferenceEquals(FilteredClipboardItems[index], item))
                        FilteredClipboardItems[index] = item;
                    continue;
                }
                int oldIndex = -1;
                for (int candidate = index + 1; candidate < FilteredClipboardItems.Count; candidate++)
                {
                    if (FilteredClipboardItems[candidate].Id == item.Id) { oldIndex = candidate; break; }
                }
                if (oldIndex >= 0)
                {
                    FilteredClipboardItems.Move(oldIndex, index);
                    if (!ReferenceEquals(FilteredClipboardItems[index], item))
                        FilteredClipboardItems[index] = item;
                }
                else FilteredClipboardItems.Insert(index, item);
            }

            ClipboardCountText = $"{FilteredClipboardItems.Count} 条记录";
            }
        });
    }

    [RelayCommand]
    public async Task ToggleFavoriteAsync(QuickCaptureItem? item)
    {
        if (item is null) return;

        bool newPinned = !item.IsPinned;
        bool changed = item.IsRecent
            ? await _quickCaptureService.SetRecentPinnedAsync(item.Id, newPinned)
            : await _quickCaptureService.SetPinnedAsync(item.Id, newPinned);
        if (!changed) return;
        item.IsPinned = newPinned;

        ApplyClipboardFilter();
        ShowToast(newPinned ? "已添加到收藏" : "已取消收藏");
    }

    [RelayCommand]
    public async Task SaveClipboardAsNoteAsync(QuickCaptureItem? item)
    {
        if (item is null || !item.IsRecent || item.Type == QuickCaptureItemType.Image) return;
        var saved = await _quickCaptureService.SaveRecentItemToRecordsAsync(item.Id, pin: false);
        ShowToast(saved is null ? "保存失败，请重试" : "已保存为随记，剪贴板记录仍会保留");
    }

    [RelayCommand]
    public async Task DeleteClipboardItemAsync(QuickCaptureItem? item)
    {
        if (item is null) return;

        if (item.IsRecent)
        {
            await _quickCaptureService.DeleteRecentItemAsync(item.Id);
        }
        else
        {
            await _quickCaptureService.DeleteItemAsync(item.Id);
        }

        _allClipboardItems.RemoveAll(i => i.Id == item.Id);
        ApplyClipboardFilter();
        ShowToast("已删除剪贴板记录");
    }

    [RelayCommand]
    public async Task CopyClipboardItemAsync(QuickCaptureItem? item)
    {
        if (item is null) return;
        await TryCopyClipboardItemAsync(item);
    }

    public async Task<bool> TryCopyClipboardItemAsync(QuickCaptureItem item)
    {
        try
        {
            var dataPackage = new DataPackage { RequestedOperation = DataPackageOperation.Copy };

            if (!string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
            {
                var file = await StorageFile.GetFileFromPathAsync(item.ImagePath);
                dataPackage.SetStorageItems(new[] { file });
            }
            else
            {
                string text = item.Body ?? string.Empty;
                dataPackage.SetText(text);
                _quickCaptureService.MarkClipboardTextWrittenByWinSticker(text);
            }

            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
            ShowToast("已复制到剪贴板");
            return true;
        }
        catch (Exception ex)
        {
            App.Log($"[QuickPanel] Copy failed: {ex.Message}");
            ShowToast("复制失败，请重试");
            return false;
        }
    }

    [RelayCommand]
    public async Task PasteClipboardItemAsync(QuickCaptureItem? item)
    {
        if (item is null) return;

        if (PasteItemAsync is { } paste) await paste(item);
        else await TryCopyClipboardItemAsync(item);
    }

    /// <summary>
    /// Windows 剪贴板"全部清除"语义：清掉未固定的最近记录，固定（收藏）项保留。
    /// </summary>
    [RelayCommand]
    public async Task ClearAllClipboardItemsAsync()
    {
        try
        {
            await _quickCaptureService.ClearRecentAsync();
            await LoadClipboardHistoryAsync();
            ShowToast("已清空剪贴板历史");
        }
        catch (Exception ex)
        {
            App.Log($"[QuickPanel] Clear all clipboard items failed: {ex.Message}");
        }
    }
}
