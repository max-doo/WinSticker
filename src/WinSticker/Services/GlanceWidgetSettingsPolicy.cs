using WinSticker.Models;

namespace WinSticker.Services;

internal static class GlanceWidgetSettingsPolicy
{
    public static bool IsDisplayElementVisible(
        GlanceWidgetData settings,
        GlanceDisplayElement element)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return element switch
        {
            GlanceDisplayElement.Time => settings.ShowTime,
            GlanceDisplayElement.Date => settings.ShowDate,
            GlanceDisplayElement.Year => settings.ShowYear,
            GlanceDisplayElement.Weekday => settings.ShowWeekday,
            GlanceDisplayElement.Weather => settings.ShowWeather,
            _ => false
        };
    }

    public static void SetDisplayElement(
        GlanceWidgetData settings,
        GlanceDisplayElement element,
        bool isVisible)
    {
        ArgumentNullException.ThrowIfNull(settings);
        switch (element)
        {
            case GlanceDisplayElement.Time:
                settings.ShowTime = isVisible;
                break;
            case GlanceDisplayElement.Date:
                settings.ShowDate = isVisible;
                if (!isVisible)
                {
                    settings.ShowYear = false;
                }
                break;
            case GlanceDisplayElement.Year:
                settings.ShowYear = isVisible;
                if (isVisible)
                {
                    settings.ShowDate = true;
                }
                break;
            case GlanceDisplayElement.Weekday:
                settings.ShowWeekday = isVisible;
                break;
            case GlanceDisplayElement.Weather:
                settings.ShowWeather = isVisible;
                break;
        }
    }

    /// <summary>
    /// Sets this widget's window material. Transparent intentionally clears the
    /// surface opacity: with no material there is nothing left to tint.
    /// </summary>
    public static void SetSurfaceMaterial(
        GlanceWidgetData settings,
        GlanceSurfaceMaterialMode mode)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.SurfaceMaterial = Enum.IsDefined(mode)
            ? mode
            : GlanceSurfaceMaterialMode.FollowSystem;
    }

    public static void SetSurfaceMaterialOpacity(
        GlanceWidgetData settings,
        double opacity)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.SurfaceMaterialOpacity = double.IsFinite(opacity)
            ? Math.Clamp(opacity, 0.05, 1.0)
            : 0.45;
    }

    public static void SetTextContrast(
        GlanceWidgetData settings,
        GlanceTextContrastMode mode)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.TextContrast = Enum.IsDefined(mode)
            ? mode
            : GlanceTextContrastMode.Auto;
    }
}
