using CommunityToolkit.Mvvm.ComponentModel;

namespace WinSticker.ViewModels;

public sealed record TodoCalendarTaskChip(
    string Id,
    string Text,
    bool IsCompleted,
    string ColorHex,
    bool IsImportant);

public sealed partial class TodoCalendarDayCell : ObservableObject
{
    public DateOnly Date { get; }
    public int DayNumber => Date.Day;
    public bool IsCurrentMonth { get; }
    public bool IsToday { get; }
    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTasks))]
    public partial IReadOnlyList<TodoCalendarTaskChip> Tasks { get; set; } = Array.Empty<TodoCalendarTaskChip>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTasks))]
    public partial int OverflowCount { get; set; }

    public bool HasTasks => Tasks.Count > 0 || OverflowCount > 0;

    public TodoCalendarDayCell(
        DateOnly date,
        DateOnly anchorMonth,
        bool isToday,
        bool isSelected)
    {
        Date = date;
        IsCurrentMonth = date.Year == anchorMonth.Year && date.Month == anchorMonth.Month;
        IsToday = isToday;
        IsSelected = isSelected;
    }

    public void SetCell(IReadOnlyList<TodoCalendarTaskChip> tasks, int overflowCount)
    {
        Tasks = tasks ?? Array.Empty<TodoCalendarTaskChip>();
        OverflowCount = Math.Max(0, overflowCount);
    }
}
