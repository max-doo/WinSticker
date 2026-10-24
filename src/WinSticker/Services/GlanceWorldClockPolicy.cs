using System.Globalization;
using WinSticker.Models;

namespace WinSticker.Services;

/// <summary>
/// Validation, list management, and per-city time formatting for the world
/// clock rows under the Glance main clock. All zone math goes through
/// <see cref="TimeZoneInfo.ConvertTime(DateTimeOffset, TimeZoneInfo)"/> so
/// daylight-saving transitions are handled by the platform, never by cached
/// offsets.
/// </summary>
internal static class GlanceWorldClockPolicy
{
    public const int MaximumWorldClockCount = 5;
    public const double MinimumSizeScale = 0.75;
    public const double MaximumSizeScale = 1.75;

    /// <summary>
    /// Formats the current time in <paramref name="zone"/>. FollowSystem keeps
    /// the system pattern verbatim (AM/PM designator included); the explicit
    /// modes use fixed patterns so all cities share the main clock's regime.
    /// </summary>
    public static string FormatCityTime(
        DateTimeOffset utcNow,
        TimeZoneInfo zone,
        GlanceTimeFormatMode mode,
        CultureInfo displayCulture)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(displayCulture);
        string pattern = mode switch
        {
            GlanceTimeFormatMode.Hour24 => "HH:mm",
            GlanceTimeFormatMode.Hour12 => "h:mm tt",
            // The caller hands the system culture for FollowSystem, so its own
            // short-time pattern (AM/PM designator or none) is the regime.
            _ => displayCulture.DateTimeFormat.ShortTimePattern
        };
        DateTime zoneTime = TimeZoneInfo.ConvertTime(utcNow, zone).DateTime;
        return zoneTime.ToString(pattern, displayCulture);
    }

    /// <summary>Display name for a row: the user's city name, or the zone id.</summary>
    public static string ResolveCityName(GlanceWorldClockData clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        string trimmed = clock.CityName?.Trim() ?? string.Empty;
        return trimmed.Length > 0 ? trimmed : clock.TimeZoneId;
    }

    /// <summary>
    /// Resolves a zone id without throwing. A corrupted timezone registry
    /// (common on stripped-down systems) must degrade to "drop the row",
    /// never crash the widget.
    /// </summary>
    public static bool TryResolveZone(string timeZoneId, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return zone is not null;
        }
        catch (Exception ex) when (
            ex is TimeZoneNotFoundException or InvalidTimeZoneException or OutOfMemoryException)
        {
            zone = null!;
            return false;
        }
    }

    public static void AddWorldClock(GlanceWidgetData settings, string timeZoneId, string? cityName)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string trimmedId = timeZoneId.Trim();
        if (!TryResolveZone(trimmedId, out _) ||
            settings.WorldClocks.Count >= MaximumWorldClockCount ||
            settings.WorldClocks.Any(clock =>
                string.Equals(clock.TimeZoneId, trimmedId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        settings.WorldClocks.Add(new GlanceWorldClockData(
            trimmedId,
            cityName?.Trim()));
    }

    public static void RemoveWorldClock(GlanceWidgetData settings, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.WorldClocks.RemoveAll(clock =>
            string.Equals(clock.TimeZoneId, timeZoneId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Normalizes a persisted world-clock list: drops invalid zone ids,
    /// trims names, dedupes by zone id, and caps the stored count.
    /// </summary>
    public static List<GlanceWorldClockData> NormalizeWorldClocks(
        IEnumerable<GlanceWorldClockData>? clocks)
    {
        if (clocks is null)
        {
            return [];
        }

        var result = new List<GlanceWorldClockData>();
        foreach (GlanceWorldClockData clock in clocks)
        {
            if (clock is null)
            {
                continue;
            }

            string trimmedId = clock.TimeZoneId?.Trim() ?? string.Empty;
            if (trimmedId.Length == 0 ||
                !TryResolveZone(trimmedId, out _) ||
                result.Any(existing => string.Equals(
                    existing.TimeZoneId,
                    trimmedId,
                    StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            string trimmedName = clock.CityName?.Trim() ?? string.Empty;
            result.Add(new GlanceWorldClockData(
                trimmedId,
                trimmedName.Length > 0 ? trimmedName : null));
            if (result.Count == MaximumWorldClockCount)
            {
                break;
            }
        }

        return result;
    }

    public static void SetSizeScale(GlanceWidgetData settings, double scale)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.SizeScale = double.IsFinite(scale)
            ? Math.Clamp(scale, MinimumSizeScale, MaximumSizeScale)
            : 1;
    }
}
