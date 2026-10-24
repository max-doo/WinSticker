#if WINSTICKER_NATIVE_AOT
using Microsoft.UI.Xaml;

namespace WinSticker.Controls.WidgetContents;

public sealed partial class GlanceWidgetContent
{
    internal async Task<AotGlanceSurfaceSnapshot> WaitForAotGlanceSurfaceAsync(
        bool expectWorldClocks)
    {
        AotGlanceSurfaceSnapshot last = CaptureAotGlanceSurface();
        for (int attempt = 0; attempt < 120; attempt++)
        {
            last = CaptureAotGlanceSurface();
            bool worldClocksReady = expectWorldClocks
                ? last.WorldClocksVisible && last.WorldClockRowCount > 0
                : !last.WorldClocksVisible && last.WorldClockRowCount == 0;
            if (last.IsLoaded &&
                last.HasXamlRoot &&
                last.DataContextMatchesViewModel &&
                last.ContentVisible &&
                last.ClockColumnVisible &&
                worldClocksReady)
            {
                return last;
            }

            await Task.Delay(50);
        }

        throw new InvalidOperationException(
            $"The Glance surface did not stabilize. Snapshot={last}");
    }

    private AotGlanceSurfaceSnapshot CaptureAotGlanceSurface()
    {
        return new AotGlanceSurfaceSnapshot(
            _isLoaded,
            XamlRoot is not null,
            ReferenceEquals(DataContext, _viewModel),
            ActualWidth,
            ActualHeight,
            ContentHost.Visibility == Visibility.Visible,
            ClockColumn.Visibility == Visibility.Visible,
            _viewModel.HasWorldClocks,
            _viewModel.WorldClockRows.Count);
    }
}

internal sealed record AotGlanceSurfaceSnapshot(
    bool IsLoaded,
    bool HasXamlRoot,
    bool DataContextMatchesViewModel,
    double ActualWidth,
    double ActualHeight,
    bool ContentVisible,
    bool ClockColumnVisible,
    bool WorldClocksVisible,
    int WorldClockRowCount);
#endif
