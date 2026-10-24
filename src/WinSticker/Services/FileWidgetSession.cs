using WinSticker.Controls.WidgetContents;
using WinSticker.ViewModels;
using WinSticker.Views;

namespace WinSticker.Services;

/// <summary>
/// Typed access to a standalone file surface hosted by ContentWidgetWindow.
/// Window lifecycle and content initialization remain owned by the unified
/// content host; this class only exposes file-specific operations used by the
/// manager.
/// </summary>
internal sealed class FileWidgetSession
{
    internal FileWidgetSession(
        ContentWidgetWindow host,
        FileWidgetContentAdapter content)
    {
        Host = host;
        Content = content;
    }

    internal ContentWidgetWindow Host { get; }

    internal FileWidgetContentAdapter Content { get; }

    internal WidgetViewModel ViewModel => Content.ViewModel;

    internal void ClearItemSelection() => Content.ClearItemSelection();

    internal void RevealSavedItem(string itemPath) => Content.RevealSavedItem(itemPath);

    internal void SetMigrationBusy(bool isBusy) => Content.SetMigrationBusy(isBusy);

    internal void SetDesktopOrganizationBusy(bool isBusy) =>
        Content.SetDesktopOrganizationBusy(isBusy);
}
