using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinSticker.Services;

/// <summary>
/// The one canonical confirmation dialog: wrapped body text, action-verb
/// primary button, localized cancel, safe default. Destructive-action
/// confirms across surfaces route through here so wording, layout, and
/// button order stay uniform.
/// </summary>
public static class ConfirmDialog
{
    public static async Task<bool> ShowAsync(
        XamlRoot? root,
        LocalizationService localization,
        string title,
        string body,
        string primaryButtonText,
        ContentDialogButton defaultButton = ContentDialogButton.Close)
    {
        if (root is null)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = localization.T("Common.Cancel"),
            DefaultButton = defaultButton,
            Content = new TextBlock
            {
                Text = body,
                TextWrapping = TextWrapping.Wrap
            }
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
