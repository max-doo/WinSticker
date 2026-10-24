namespace WinSticker.Models;

public enum GlanceDisplayElement
{
    Time,
    Date,
    Year,
    Weekday,
    Weather
}

public enum GlanceTimeFormatMode
{
    FollowSystem,
    Hour24,
    Hour12
}

/// <summary>
/// How the widget window's material relates to the global appearance setting.
/// Only the Glance clock owns this choice — every other widget kind keeps
/// following the global material.
/// </summary>
public enum GlanceSurfaceMaterialMode
{
    /// <summary>Use the global widget material and opacity.</summary>
    FollowSystem,

    /// <summary>No material, no border, no divider — text floats on the desktop.</summary>
    Transparent,

    /// <summary>Faint acrylic glass with its own opacity.</summary>
    Glass
}

/// <summary>
/// Text colour strategy for the transparent surface. The desktop wallpaper is
/// arbitrary, so the default resolves against the sampled wallpaper luminance
/// and the other values let the user pin the outcome.
/// </summary>
public enum GlanceTextContrastMode
{
    /// <summary>Sample the wallpaper and pick the readable side, adding a plate when it is not enough.</summary>
    Auto,

    /// <summary>Always light text (for dark wallpapers).</summary>
    White,

    /// <summary>Always dark text (for bright wallpapers).</summary>
    Black,

    /// <summary>Follow the window theme and draw no contrast plate.</summary>
    Off
}

/// <summary>One additional city time shown under the main clock.</summary>
public sealed record GlanceWorldClockData(
    string TimeZoneId,
    string? CityName = null);

/// <summary>
/// Versioned, portable preferences for one Glance clock widget. The Glance
/// surface is a transparent clock patch: no background photos, no calendar —
/// just time, date, a weather block, and additional world clocks.
/// </summary>
public sealed class GlanceWidgetData
{
    public const int CurrentVersion = 12;

    public int Version { get; set; } = CurrentVersion;
    public bool ShowTime { get; set; } = true;
    public bool ShowDate { get; set; } = true;
    public bool ShowYear { get; set; }
    public bool ShowWeekday { get; set; } = true;

    /// <summary>
    /// Phone-style weather block beside the clock (icon + temperature +
    /// condition + range + location). On by default.
    /// </summary>
    public bool ShowWeather { get; set; } = true;

    /// <summary>Whether the main clock (and world clocks) include seconds.</summary>
    public bool ShowSeconds { get; set; }
    public GlanceTimeFormatMode TimeFormat { get; set; } = GlanceTimeFormatMode.FollowSystem;
    public string? TimeFontFamily { get; set; }

    /// <summary>Multiplier on the main clock's typography.</summary>
    public double TimeScale { get; set; } = 1;

    /// <summary>
    /// Overall patch scale. The Glance window is not manually resizable; this
    /// setting is the only way to size it, so the whole surface scales with it.
    /// </summary>
    public double SizeScale { get; set; } = 1;

    /// <summary>Additional city times listed under the main clock.</summary>
    public List<GlanceWorldClockData> WorldClocks { get; set; } = [];

    // ─── Surface (transparent clock) ───────────────────────────────
    /// <summary>
    /// Material override for this widget's window. Defaults to
    /// <see cref="GlanceSurfaceMaterialMode.FollowSystem"/>.
    /// </summary>
    public GlanceSurfaceMaterialMode SurfaceMaterial { get; set; } =
        GlanceSurfaceMaterialMode.FollowSystem;

    /// <summary>Opacity used by <see cref="GlanceSurfaceMaterialMode.Glass"/>.</summary>
    public double SurfaceMaterialOpacity { get; set; } = 0.45;

    /// <summary>Text colour strategy for the transparent surface.</summary>
    public GlanceTextContrastMode TextContrast { get; set; } =
        GlanceTextContrastMode.Auto;

    /// <summary>
    /// Whether the faint contrast plate behind the text is allowed. Auto
    /// contrast still decides per wallpaper; this only lets the user veto it.
    /// </summary>
    public bool ShowTextPlate { get; set; } = true;
}
