using WinSticker.Models;
using WinSticker.Services;
using Microsoft.UI.Xaml;

namespace WinSticker.Helpers;

public static class QuickCaptureClipboardActivationHelper
{
    public static async Task<bool> EnableAsync(XamlRoot? xamlRoot, LocalizationService localizationService)
    {
        await App.Current.QuickCaptureSettings.EnableClipboardFromOpenWidgetAsync();
        App.Log("[QuickCaptureClipboard] Enabled");
        return true;
    }
}
