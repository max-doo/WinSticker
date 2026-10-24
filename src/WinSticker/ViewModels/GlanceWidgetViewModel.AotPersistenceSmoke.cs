#if WINSTICKER_NATIVE_AOT
using WinSticker.Models;

namespace WinSticker.ViewModels;

public sealed partial class GlanceWidgetViewModel
{
    internal AotGlanceViewModelSnapshot CaptureAotGlanceSnapshot()
    {
        return new AotGlanceViewModelSnapshot(
            _settings.SurfaceMaterial.ToString(),
            _settings.ShowTime,
            _settings.ShowDate,
            _settings.ShowYear,
            _settings.ShowWeekday,
            _settings.ShowWeather,
            _settings.ShowSeconds,
            _settings.SizeScale,
            _settings.WorldClocks.Count,
            HasWorldClocks);
    }
}

internal sealed record AotGlanceViewModelSnapshot(
    string SurfaceMaterial,
    bool ShowTime,
    bool ShowDate,
    bool ShowYear,
    bool ShowWeekday,
    bool ShowWeather,
    bool ShowSeconds,
    double SizeScale,
    int WorldClockCount,
    bool HasWorldClocks);
#endif
