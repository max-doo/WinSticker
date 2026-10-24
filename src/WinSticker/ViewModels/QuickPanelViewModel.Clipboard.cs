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

    private readonly List<QuickCaptureItem> _allClipboardItems = [];

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
            _allClipboardItems.Clear();

            // RecentItems represents clipboard captures; Pinned items are in Items.Where(i => i.IsPinned).
            if (data.RecentItems is { Count: > 0 })
            {
                _allClipboardItems.AddRange(data.RecentItems);
            }

            // Also check for pinned records in Items
            if (data.Items is { Count: > 0 })
            {
                foreach (var item in data.Items.Where(i => i.IsPinned))
                {
                    if (!_allClipboardItems.Any(existing => existing.Id == item.Id))
                    {
                        _allClipboardItems.Add(item);
                    }
                }
            }

            ApplyClipboardFilter();
        }
        catch (Exception ex)
        {
            App.Log($"[QuickPanel] Failed to load clipboard history: {ex.Message}");
        }
    }

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

            FilteredClipboardItems.Clear();
            foreach (var item in results)
            {
                FilteredClipboardItems.Add(item);
            }

            ClipboardCountText = $"{FilteredClipboardItems.Count} 条记录";
        });
    }

    [RelayCommand]
    public async Task ToggleFavoriteAsync(QuickCaptureItem? item)
    {
        if (item is null) return;

        bool newPinned = !item.IsPinned;
        item.IsPinned = newPinned;

        if (item.IsRecent)
        {
            if (newPinned)
            {
                await _quickCaptureService.SaveRecentItemToRecordsAsync(item.Id, pin: true);
            }
            else
            {
                await _quickCaptureService.SetPinnedAsync(item.Id, false);
            }
        }
        else
        {
            await _quickCaptureService.SetPinnedAsync(item.Id, newPinned);
        }

        ApplyClipboardFilter();
        ShowToast(newPinned ? "已添加到收藏" : "已取消收藏");
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
        }
        catch (Exception ex)
        {
            App.Log($"[QuickPanel] Copy failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task PasteClipboardItemAsync(QuickCaptureItem? item)
    {
        if (item is null) return;

        await CopyClipboardItemAsync(item);
        RequestPasteItem?.Invoke(this, item);
    }
}
