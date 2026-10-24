using System.Globalization;
using WinSticker.Models;
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

    [Fact]
    public void BucketByLocalDate_UsesLocalCalendarDayNotUtcDay()
    {
        var tz = TimeZoneInfo.CreateCustomTimeZone("t+8", TimeSpan.FromHours(8), "t+8", "t+8");
        var items = new List<TodoItem>
        {
            new() { Id = "a", Text = "late night", DueDate = new DateTimeOffset(2026, 10, 24, 23, 30, 0, TimeSpan.FromHours(8)) }
        };

        var buckets = TodoCalendarProjection.BucketByLocalDate(items, tz);

        Assert.True(buckets.ContainsKey(new DateOnly(2026, 10, 24)));
        Assert.Equal("a", buckets[new DateOnly(2026, 10, 24)][0].Id);
    }

    [Fact]
    public void BucketByLocalDate_ExcludesItemsWithoutDueDate()
    {
        var items = new List<TodoItem>
        {
            new() { Id = "a", Text = "dated", DueDate = DateTimeOffset.UtcNow },
            new() { Id = "b", Text = "undated", DueDate = null }
        };

        var buckets = TodoCalendarProjection.BucketByLocalDate(items, TimeZoneInfo.Utc);

        Assert.DoesNotContain(buckets.Values.SelectMany(v => v), item => item.Id == "b");
    }

    [Fact]
    public void BucketByLocalDate_OrdersActiveBeforeCompletedThenByTime()
    {
        var day = new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero);
        var items = new List<TodoItem>
        {
            new() { Id = "done", Text = "done", IsCompleted = true, DueDate = day.AddHours(9) },
            new() { Id = "late", Text = "late", DueDate = day.AddHours(18), SortOrder = 1 },
            new() { Id = "early", Text = "early", DueDate = day.AddHours(8), SortOrder = 5 }
        };

        var buckets = TodoCalendarProjection.BucketByLocalDate(items, TimeZoneInfo.Utc);

        Assert.Equal(["early", "late", "done"], buckets[new DateOnly(2026, 10, 24)].Select(item => item.Id));
    }

    [Fact]
    public void AssignSlots_TruncatesAndCountsOverflow()
    {
        var items = Enumerable.Range(0, 5)
            .Select(i => new TodoItem { Id = i.ToString(), Text = $"t{i}", DueDate = DateTimeOffset.UtcNow })
            .ToList();

        var (visible, overflow) = TodoCalendarProjection.AssignSlots(items, maxVisible: 3);

        Assert.Equal(3, visible.Count);
        Assert.Equal(2, overflow);
    }

    [Fact]
    public void AssignSlots_WithZeroMaxReturnsEverythingAsOverflow()
    {
        var items = new List<TodoItem> { new() { Id = "a", Text = "a", DueDate = DateTimeOffset.UtcNow } };

        var (visible, overflow) = TodoCalendarProjection.AssignSlots(items, maxVisible: 0);

        Assert.Empty(visible);
        Assert.Equal(1, overflow);
    }
}
