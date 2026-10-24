using WinSticker.Models;
using WinSticker.ViewModels;

namespace WinSticker.Services;

internal static class QuickCaptureClipboardCopyPolicy
{
    public static bool ShouldCopyBitmap(QuickCaptureItemViewModel item) =>
        item.IsRecent && item.Type == QuickCaptureItemType.Image;
}
