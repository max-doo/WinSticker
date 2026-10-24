using System.Globalization;
using WinSticker.Models;

namespace WinSticker.Services;

/// <summary>
/// Pure deterministic projection logic for calendar views: grid layout,
/// local date bucketing, and cell slot assignment.
/// </summary>
public static class TodoCalendarProjection
{
    public const int WeeksPerMonth = 6;
    public const int DaysPerWeek = 7;
    public const int CellCount = WeeksPerMonth * DaysPerWeek;

    public static DayOfWeek ResolveWeekStart(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return culture.DateTimeFormat.FirstDayOfWeek;
    }

    public static IReadOnlyList<DateOnly> BuildMonthGrid(int year, int month, DayOfWeek weekStart)
    {
        var firstDay = new DateOnly(year, month, 1);
        int offset = ((int)firstDay.DayOfWeek - (int)weekStart + DaysPerWeek) % DaysPerWeek;
        var start = firstDay.AddDays(-offset);

        var cells = new DateOnly[CellCount];
        for (int i = 0; i < CellCount; i++)
        {
            cells[i] = start.AddDays(i);
        }

        return cells;
    }
}
