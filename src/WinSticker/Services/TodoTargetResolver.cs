using WinSticker.Models;

namespace WinSticker.Services;

/// <summary>
/// Resolves the currently active Todo widget configuration and ID.
/// Used by QuickPanel, Calendar widgets, and any surface projecting Todo data.
/// </summary>
public static class TodoTargetResolver
{
    public const string FallbackWidgetId = "todo";

    public static WidgetConfig? ResolveConfig(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ResolveConfig(settings.WidgetLayout.Widgets);
    }

    public static WidgetConfig? ResolveConfig(IEnumerable<WidgetConfig> widgets)
    {
        ArgumentNullException.ThrowIfNull(widgets);
        return widgets.FirstOrDefault(w => w.WidgetKind == WidgetKind.Todo && !w.IsDisabled);
    }

    public static string ResolveWidgetId(AppSettings settings)
    {
        return ResolveConfig(settings)?.Id ?? FallbackWidgetId;
    }

    public static string ResolveWidgetId(IEnumerable<WidgetConfig> widgets)
    {
        return ResolveConfig(widgets)?.Id ?? FallbackWidgetId;
    }
}
