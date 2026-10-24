using WinSticker.ViewModels;
using Xunit;

namespace WinSticker.Tests;

public sealed class TodoCalendarDayCellTests
{
    [Fact]
    public void DayCell_ExposesDerivedFlags()
    {
        var cell = new TodoCalendarDayCell(new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 1), isToday: true, isSelected: false);

        Assert.Equal(24, cell.DayNumber);
        Assert.True(cell.IsCurrentMonth);
        Assert.True(cell.IsToday);
        Assert.False(cell.IsSelected);
        Assert.True(cell.IsWeekend); // 2026-10-24 是周六
        Assert.False(cell.HasTasks);
    }

    [Fact]
    public void DayCell_OutsideMonthIsNotCurrentMonth()
    {
        var cell = new TodoCalendarDayCell(new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 1), isToday: false, isSelected: false);
        Assert.False(cell.IsCurrentMonth);
        Assert.False(cell.IsWeekend); // 周一
    }

    [Fact]
    public void DayCell_SetCellUpdatesTasksAndOverflow()
    {
        var cell = new TodoCalendarDayCell(new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 1), isToday: true, isSelected: false);
        var chip = new TodoCalendarTaskChip("id1", "Buy milk", false, "#FFFFFF", false);
        cell.SetCell([chip], 2);

        Assert.Single(cell.Tasks);
        Assert.Equal(2, cell.OverflowCount);
        Assert.True(cell.HasTasks);
    }
}
