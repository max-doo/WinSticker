using WinSticker.Controls.WidgetContents;

namespace WinSticker.Services;

public sealed partial class WidgetManager
{
    internal IReadOnlyList<FolderWatcherHealthSnapshot> GetFolderWatcherHealthSnapshots()
    {
        return _fileWidgets.Values
            .Select(entry => entry.ViewModel)
            .Concat(_contentWidgets.Values
                .Select(window => window.CurrentContent)
                .OfType<FileWidgetContentAdapter>()
                .Select(content => content.ViewModel))
            .Distinct()
            .SelectMany(viewModel => new[]
            {
                viewModel.FolderWatcherHealth,
                viewModel.PublicFolderWatcherHealth
            })
            .ToArray();
    }

    internal WinStickerWidgetManagerDiagnostic CreateDiagnosticsSnapshot()
    {
        IReadOnlyList<IDesktopWidgetWindow> windows = GetLoadedDesktopWindows();
        TrayToggleQueueSnapshot queue = _trayToggleRequestQueue.GetSnapshot();
        int loadedGroupedFileCount = _contentWidgets.Values
            .DistinctBy(window => window.WindowHandle)
            .Count(window =>
                window.Identity.IsGroupSurface &&
                window.CurrentContent is FileWidgetContentAdapter);
        WinStickerFileHostDiagnostic fileHosts = _fileWidgetHostDiagnostics.CreateSnapshot(
            _fileWidgets.Values
                .DistinctBy(session => session.Host.WindowHandle)
                .Count(),
            loadedGroupedFileCount);
        WinStickerWidgetHostDiagnostic[] hosts = windows
            .OrderBy(window => window.Identity.SurfaceId, StringComparer.Ordinal)
            .Select((window, index) =>
            {
                FileWidgetContentAdapter? fileSurface = _contentWidgets.Values
                    .FirstOrDefault(contentWindow =>
                        contentWindow.WindowHandle == window.WindowHandle)
                    ?.CurrentContent as FileWidgetContentAdapter;
                return new WinStickerWidgetHostDiagnostic(
                    index + 1,
                    window.Identity.WidgetKind,
                    window.Identity.LogKind,
                    window.Identity.IsGroupSurface,
                    window.Visible,
                    window.IsRaisedAboveDesktopLayer,
                    window.IsCompactArrangementActive,
                    fileSurface?.IsImportBusy == true,
                    fileSurface?.ImportBusyElapsedMilliseconds,
                    ToDiagnosticRect(window.AnimationBounds),
                    ToDiagnosticRect(window.RestingAnimationBounds));
            })
            .ToArray();

        return new WinStickerWidgetManagerDiagnostic(
            WidgetsRaisedFromTray,
            SessionState.ToString(),
            IsWidgetInteractionActive,
            LoadedSurfaceCount,
            windows.Count(window => window.Visible),
            fileHosts,
            new WinStickerTrayQueueDiagnostic(
                queue.PendingCount,
                queue.WorkerRunning,
                queue.TotalRequests,
                queue.EffectiveToggles,
                queue.FoldedNoOpBatches,
                queue.LastSource,
                !string.IsNullOrWhiteSpace(queue.LastError)),
            hosts);
    }

    private static WinStickerDiagnosticRect ToDiagnosticRect(Windows.Foundation.Rect bounds)
    {
        return new WinStickerDiagnosticRect(
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height);
    }
}
