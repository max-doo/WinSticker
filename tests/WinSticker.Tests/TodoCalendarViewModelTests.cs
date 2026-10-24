using WinSticker.Models;
using WinSticker.Services;
using WinSticker.ViewModels;
using Xunit;

namespace WinSticker.Tests;

public sealed class TodoCalendarViewModelTests
{
    private static void Cleanup(string dir)
    {
        TodoRepository.TryRemove(new TodoWidgetStore(Path.Combine(dir, "widgets"), "cal-test").StorePath);
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch { }
    }

    private static (TodoCalendarViewModel vm, string dir) CreateEnvironment()
    {
        string dir = Path.Combine(Path.GetTempPath(), "winsticker-cal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new TodoWidgetStore(Path.Combine(dir, "widgets"), "cal-test");
        var settings = new SettingsService(dir);
        var loc = new LocalizationService(settings);
        var fixedNow = new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);
        var vm = new TodoCalendarViewModel(
            store,
            loc,
            new WidgetConfig { Id = "cal-test", WidgetKind = WidgetKind.Todo },
            settings,
            () => fixedNow,
            TimeZoneInfo.Utc);
        return (vm, dir);
    }

    [Fact]
    public async Task Initialize_BuildsSixWeekGridCoveringAnchorMonth()
    {
        var (vm, dir) = CreateEnvironment();
        try
        {
            await vm.InitializeAsync();
            Assert.Equal(42, vm.Cells.Count);
            Assert.Equal(new DateOnly(2026, 10, 1), vm.MonthAnchor);
            Assert.Contains(vm.Cells, cell => cell.IsToday && cell.Date == new DateOnly(2026, 10, 24));
        }
        finally
        {
            vm.Dispose();
            Cleanup(dir);
        }
    }

    [Fact]
    public async Task ShowMonth_RebuildsGridToRequestedMonth()
    {
        var (vm, dir) = CreateEnvironment();
        try
        {
            await vm.InitializeAsync();
            await vm.ShowMonthAsync(new DateOnly(2026, 12, 1));
            Assert.Equal(new DateOnly(2026, 12, 1), vm.MonthAnchor);
            Assert.Contains(vm.Cells, cell => cell.Date == new DateOnly(2026, 12, 1) && cell.IsCurrentMonth);
        }
        finally
        {
            vm.Dispose();
            Cleanup(dir);
        }
    }

    [Fact]
    public async Task TaskWithDueDate_AppearsInItsLocalDayCell()
    {
        var (vm, dir) = CreateEnvironment();
        try
        {
            await vm.InitializeAsync();
            await vm.CreateTaskOnDateAsync(new DateOnly(2026, 10, 24), "write tests");

            var cell = vm.Cells.Single(c => c.Date == new DateOnly(2026, 10, 24));
            Assert.Contains(cell.Tasks, chip => chip.Text == "write tests");
        }
        finally
        {
            vm.Dispose();
            Cleanup(dir);
        }
    }

    [Fact]
    public async Task MoveTaskToDate_PreservesTimeOfDayAndMovesToNewDay()
    {
        var (vm, dir) = CreateEnvironment();
        try
        {
            await vm.InitializeAsync();
            var due = new DateTimeOffset(2026, 10, 24, 15, 30, 0, TimeSpan.Zero);
            var created = await vm.Todo.AddItemAsync("ship release", false, due);

            await vm.MoveTaskToDateAsync(created!.Id, new DateOnly(2026, 10, 27));

            var reloaded = TodoRepository.ForStore(new TodoWidgetStore(Path.Combine(dir, "widgets"), "cal-test"));
            var data = await reloaded.LoadAsync();
            var item = data.Items.Single(i => i.Id == created.Id);
            Assert.Equal(new DateOnly(2026, 10, 27), DateOnly.FromDateTime(item.DueDate!.Value.DateTime));
            Assert.Equal(new TimeOnly(15, 30), TimeOnly.FromDateTime(item.DueDate.Value.DateTime));
        }
        finally
        {
            vm.Dispose();
            Cleanup(dir);
        }
    }

    [Fact]
    public async Task MoveTaskToDate_ShiftsRecurrenceAnchor()
    {
        var (vm, dir) = CreateEnvironment();
        try
        {
            await vm.InitializeAsync();
            var due = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
            var created = await vm.Todo.AddItemAsync("weekly", false, due);
            await vm.Todo.SetRecurrenceAsync(created!.Id, TodoRecurrenceMode.Weekly);

            await vm.MoveTaskToDateAsync(created.Id, new DateOnly(2026, 10, 28));

            var item = vm.Todo.Items.Single(i => i.Id == created.Id);
            Assert.Equal(new DateOnly(2026, 10, 28), DateOnly.FromDateTime(item.DueDate!.Value.UtcDateTime));
            Assert.Equal(new DateOnly(2026, 10, 28), DateOnly.FromDateTime(item.Recurrence!.AnchorDueDate!.Value.UtcDateTime));
        }
        finally
        {
            vm.Dispose();
            Cleanup(dir);
        }
    }

    [Fact]
    public async Task ToggleCompleted_OnRecurringTask_GeneratesNextOccurrence()
    {
        var (vm, dir) = CreateEnvironment();
        try
        {
            await vm.InitializeAsync();
            var created = await vm.Todo.AddItemAsync("weekly", false, new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));
            await vm.Todo.SetRecurrenceAsync(created!.Id, TodoRecurrenceMode.Weekly);
            int before = vm.Todo.Items.Count;

            await vm.ToggleCompletedAsync(created.Id);

            Assert.True(vm.Todo.Items.Single(i => i.Id == created.Id).IsCompleted);
            Assert.True(vm.Todo.Items.Count > before, "完成循环任务后必须生成下一次");
        }
        finally
        {
            vm.Dispose();
            Cleanup(dir);
        }
    }

    [Fact]
    public async Task CreateTaskOnDate_OnToday_UsesClockTime_OnOtherDay_UsesNineAm()
    {
        var (vm, dir) = CreateEnvironment();
        try
        {
            await vm.InitializeAsync();
            await vm.CreateTaskOnDateAsync(new DateOnly(2026, 10, 24), "today task");
            await vm.CreateTaskOnDateAsync(new DateOnly(2026, 11, 2), "future task");

            var today = vm.Todo.Items.Single(i => i.Item.Text == "today task");
            var future = vm.Todo.Items.Single(i => i.Item.Text == "future task");
            Assert.Equal(new TimeOnly(10, 0), TimeOnly.FromDateTime(today.DueDate!.Value.UtcDateTime));
            Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(future.DueDate!.Value.UtcDateTime));
        }
        finally
        {
            vm.Dispose();
            Cleanup(dir);
        }
    }

    [Fact]
    public async Task RebuildCells_IdenticalMonth_StillRaisesCellsPropertyChanged()
    {
        // Same-month rebuilds reuse cell instances, so CollectionChanged is
        // silent; the view contract is a Cells PropertyChanged instead.
        var (vm, dir) = CreateEnvironment();
        try
        {
            await vm.InitializeAsync();
            var notified = new List<string>();
            vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? string.Empty);

            vm.RebuildCells();

            Assert.Equal(42, vm.Cells.Count);
            Assert.Contains(nameof(TodoCalendarViewModel.Cells), notified);
        }
        finally
        {
            vm.Dispose();
            Cleanup(dir);
        }
    }

    [Fact]
    public async Task GoToTodayIfAnchorStale_ReanchorsOnlyWhenMonthDiffers()
    {
        var (vm, dir) = CreateEnvironment();
        try
        {
            await vm.InitializeAsync();
            await vm.ShowMonthAsync(new DateOnly(2026, 1, 15));
            Assert.Equal(new DateOnly(2026, 1, 1), vm.MonthAnchor);

            await vm.GoToTodayIfAnchorStaleAsync();
            Assert.Equal(new DateOnly(2026, 10, 1), vm.MonthAnchor);
            Assert.Equal(new DateOnly(2026, 10, 24), vm.SelectedDate);

            // Already anchored to today: a stale-anchor re-entry must not
            // clobber the user's in-month selection.
            await vm.SelectDateAsync(new DateOnly(2026, 10, 5));
            await vm.GoToTodayIfAnchorStaleAsync();
            Assert.Equal(new DateOnly(2026, 10, 1), vm.MonthAnchor);
            Assert.Equal(new DateOnly(2026, 10, 5), vm.SelectedDate);
        }
        finally
        {
            vm.Dispose();
            Cleanup(dir);
        }
    }
}
