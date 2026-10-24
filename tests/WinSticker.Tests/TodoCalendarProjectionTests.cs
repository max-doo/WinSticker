using System.Globalization;
using WinSticker.Services;
using Xunit;

namespace WinSticker.Tests;

public sealed class TodoCalendarProjectionTests
{
    [Fact]
    public void BuildMonthGrid_ReturnsFortyTwoConsecutiveDays()
    {
        var cells = TodoCalendarProjection.BuildMonthGrid(2026, 10, DayOfWeek.Monday);
        Assert.Equal(42, cells.Count);
        for (int i = 1; i < cells.Count; i++)
        {
            Assert.Equal(cells[i - 1].AddDays(1), cells[i]);
        }
    }

    [Fact]
    public void BuildMonthGrid_StartsOnRequestedWeekdayAndContainsWholeMonth()
    {
        var monday = TodoCalendarProjection.BuildMonthGrid(2026, 10, DayOfWeek.Monday);
        Assert.Equal(DayOfWeek.Monday, monday[0].DayOfWeek);
        Assert.Contains(new DateOnly(2026, 10, 1), monday);
        Assert.Contains(new DateOnly(2026, 10, 31), monday);

        var sunday = TodoCalendarProjection.BuildMonthGrid(2026, 10, DayOfWeek.Sunday);
        Assert.Equal(DayOfWeek.Sunday, sunday[0].DayOfWeek);
        Assert.Contains(new DateOnly(2026, 10, 1), sunday);
        Assert.Contains(new DateOnly(2026, 10, 31), sunday);
    }

    [Fact]
    public void BuildMonthGrid_HandlesLeapFebruaryAndMonthEnd()
    {
        var cells = TodoCalendarProjection.BuildMonthGrid(2024, 2, DayOfWeek.Monday);
        Assert.Contains(new DateOnly(2024, 2, 29), cells);
        Assert.Contains(new DateOnly(2024, 2, 1), cells);

        var january = TodoCalendarProjection.BuildMonthGrid(2026, 1, DayOfWeek.Monday);
        Assert.Contains(new DateOnly(2026, 1, 31), january);
    }

    [Theory]
    [InlineData("en-US", DayOfWeek.Sunday)]
    [InlineData("fr-FR", DayOfWeek.Monday)]
    public void ResolveWeekStart_MatchesCulture(string cultureName, DayOfWeek expected)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        Assert.Equal(expected, TodoCalendarProjection.ResolveWeekStart(culture));
    }
}
