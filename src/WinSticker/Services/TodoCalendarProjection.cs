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

    public static IReadOnlyDictionary<DateOnly, IReadOnlyList<TodoItem>> BucketByLocalDate(
        IEnumerable<TodoItem> items,
        TimeZoneInfo tz)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(tz);

        var buckets = new Dictionary<DateOnly, List<TodoItem>>();

        foreach (var item in items)
        {
            if (item.DueDate is not { } due)
            {
                continue;
            }

            var localDateTime = TimeZoneInfo.ConvertTime(due, tz);
            var localDate = DateOnly.FromDateTime(localDateTime.DateTime);

            if (!buckets.TryGetValue(localDate, out var list))
            {
                list = [];
                buckets[localDate] = list;
            }

            list.Add(item);
        }

        var result = new Dictionary<DateOnly, IReadOnlyList<TodoItem>>(buckets.Count);
        foreach (var (date, list) in buckets)
        {
            var ordered = list
                .OrderBy(i => i.IsCompleted)
                .ThenBy(i => i.DueDate)
                .ThenBy(i => i.SortOrder)
                .ThenBy(i => i.CreatedAt)
                .ToList();
            result[date] = ordered;
        }

        return result;
    }

    public static (IReadOnlyList<TodoItem> visible, int overflow) AssignSlots(
        IReadOnlyList<TodoItem> items,
        int maxVisible)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (maxVisible <= 0)
        {
            return (Array.Empty<TodoItem>(), items.Count);
        }

        if (items.Count <= maxVisible)
        {
            return (items, 0);
        }

        var visible = items.Take(maxVisible).ToList();
        int overflow = items.Count - maxVisible;
        return (visible, overflow);
    }
}
