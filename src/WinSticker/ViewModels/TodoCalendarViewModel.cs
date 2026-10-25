using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.ViewModels;

public sealed partial class TodoCalendarViewModel : ObservableObject, IDisposable
{
    private readonly TodoWidgetViewModel _todo;
    private readonly LocalizationService _localizationService;
    private readonly WidgetConfig _config;
    private readonly SettingsService? _settingsService;
    private readonly Func<DateTimeOffset> _now;
    private readonly TimeZoneInfo _tz;
    private readonly bool _ownsTodoViewModel;
    private readonly DispatcherQueue? _dispatcherQueue;

    private int _rebuildGate;
    private bool _disposed;

    [ObservableProperty]
    public partial DateOnly MonthAnchor { get; set; }

    [ObservableProperty]
    public partial DateOnly? SelectedDate { get; set; }

    [ObservableProperty]
    public partial int MaxTasksPerCell { get; set; } = 3;

    public ObservableCollection<TodoCalendarDayCell> Cells { get; } = [];

    public ObservableCollection<TodoItemViewModel> SelectedDayTasks { get; } = [];

    public TodoWidgetViewModel Todo => _todo;

    public string MonthTitle
    {
        get
        {
            var text = _localizationService.T("Calendar.TitleFormat");
            if (text == "Calendar.TitleFormat" || !text.Contains("{0}"))
            {
                return $"{MonthAnchor.Year} 年 {MonthAnchor.Month} 月";
            }
            return _localizationService.Format("Calendar.TitleFormat", MonthAnchor.Year, MonthAnchor.Month);
        }
    }

    public bool IsEmptyStateVisible => _todo.Items.Count == 0;

    public TodoCalendarViewModel(
        TodoWidgetStore store,
        LocalizationService localizationService,
        WidgetConfig config,
        SettingsService? settingsService = null,
        Func<DateTimeOffset>? clock = null,
        TimeZoneInfo? timeZone = null,
        bool ownsTodoViewModel = true)
        : this(
            new TodoWidgetViewModel(store, localizationService, config, settingsService),
            localizationService,
            config,
            settingsService,
            clock,
            timeZone,
            ownsTodoViewModel: ownsTodoViewModel)
    {
    }

    public TodoCalendarViewModel(
        TodoWidgetViewModel todoViewModel,
        LocalizationService localizationService,
        WidgetConfig config,
        SettingsService? settingsService = null,
        Func<DateTimeOffset>? clock = null,
        TimeZoneInfo? timeZone = null,
        bool ownsTodoViewModel = false)
    {
        _todo = todoViewModel ?? throw new ArgumentNullException(nameof(todoViewModel));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _settingsService = settingsService;
        _now = clock ?? (() => DateTimeOffset.Now);
        _tz = timeZone ?? TimeZoneInfo.Local;
        _ownsTodoViewModel = ownsTodoViewModel;
        _dispatcherQueue = TryGetDispatcherQueue();

        var localNow = TimeZoneInfo.ConvertTime(_now(), _tz);
        MonthAnchor = new DateOnly(localNow.Year, localNow.Month, 1);
        SelectedDate = DateOnly.FromDateTime(localNow.DateTime);

        _todo.PropertyChanged += Todo_PropertyChanged;
        _todo.Items.CollectionChanged += TodoItems_CollectionChanged;
    }

    public async Task InitializeAsync()
    {
        await _todo.InitializeAsync();
        RebuildCells();
    }

    public Task ShowMonthAsync(DateOnly month)
    {
        MonthAnchor = new DateOnly(month.Year, month.Month, 1);
        RebuildCells();
        return Task.CompletedTask;
    }

    public Task SelectDateAsync(DateOnly date)
    {
        SelectedDate = date;
        foreach (var cell in Cells)
        {
            cell.IsSelected = cell.Date == date;
        }
        UpdateSelectedDayTasks();
        return Task.CompletedTask;
    }

    public async Task GoToTodayAsync()
    {
        var localNow = TimeZoneInfo.ConvertTime(_now(), _tz);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        await ShowMonthAsync(today);
        await SelectDateAsync(today);
    }

    /// <summary>
    /// The calendar view model outlives the tab it lives in; without this
    /// re-entry the panel keeps showing whatever month was last browsed,
    /// even days later in the same app session.
    /// </summary>
    public async Task GoToTodayIfAnchorStaleAsync()
    {
        var localNow = TimeZoneInfo.ConvertTime(_now(), _tz);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        if (MonthAnchor.Year != today.Year || MonthAnchor.Month != today.Month)
        {
            await GoToTodayAsync();
        }
    }

    public async Task MoveTaskToDateAsync(string taskId, DateOnly date)
    {
        var current = _todo.Items.FirstOrDefault(i => i.Id == taskId)?.Item.DueDate;
        TimeOnly time = current is { } d
            ? TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(d, _tz).DateTime)
            : new TimeOnly(9, 0);
        var local = date.ToDateTime(time);
        var target = new DateTimeOffset(local, _tz.GetUtcOffset(local));
        await _todo.SetDueDateAsync(taskId, target);
        RebuildCells();
    }

    public async Task CreateTaskOnDateAsync(DateOnly date, string text)
    {
        var localNow = TimeZoneInfo.ConvertTime(_now(), _tz);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        TimeOnly time = date == today
            ? TimeOnly.FromDateTime(localNow.DateTime)
            : new TimeOnly(9, 0);
        var local = date.ToDateTime(time);
        var due = new DateTimeOffset(local, _tz.GetUtcOffset(local));
        await _todo.AddItemAsync(text, false, due);
        RebuildCells();
    }

    public async Task ToggleCompletedAsync(string taskId)
    {
        var item = _todo.Items.FirstOrDefault(i => i.Id == taskId);
        if (item is not null)
        {
            await _todo.SetCompletedAsync(taskId, !item.IsCompleted);
            RebuildCells();
        }
    }

    private void Todo_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TodoWidgetViewModel.Items))
        {
            ScheduleRebuildCells();
        }
    }

    private void TodoItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScheduleRebuildCells();
    }

    partial void OnMaxTasksPerCellChanged(int value)
    {
        RebuildCells();
    }

    partial void OnMonthAnchorChanged(DateOnly value)
    {
        OnPropertyChanged(nameof(MonthTitle));
    }

    public void RebuildCells()
    {
        if (_disposed) return;

        CultureInfo culture;
        try
        {
            culture = !string.IsNullOrWhiteSpace(_localizationService.CurrentCultureName)
                ? CultureInfo.GetCultureInfo(_localizationService.CurrentCultureName)
                : CultureInfo.CurrentCulture;
        }
        catch
        {
            culture = CultureInfo.CurrentCulture;
        }

        var weekStart = TodoCalendarProjection.ResolveWeekStart(culture);
        var grid = TodoCalendarProjection.BuildMonthGrid(MonthAnchor.Year, MonthAnchor.Month, weekStart);
        var buckets = TodoCalendarProjection.BucketByLocalDate(
            _todo.Items.Select(vm => vm.Item),
            _tz);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_now(), _tz).DateTime);

        if (Cells.Count != grid.Count)
        {
            Cells.Clear();
            foreach (var day in grid)
            {
                var cell = new TodoCalendarDayCell(
                    day,
                    MonthAnchor,
                    isToday: day == today,
                    isSelected: day == SelectedDate);
                PopulateCell(cell, day, buckets);
                Cells.Add(cell);
            }
        }
        else
        {
            for (int i = 0; i < grid.Count; i++)
            {
                var day = grid[i];
                var cell = Cells[i];
                if (cell.Date != day || cell.IsCurrentMonth != (day.Year == MonthAnchor.Year && day.Month == MonthAnchor.Month))
                {
                    Cells[i] = new TodoCalendarDayCell(
                        day,
                        MonthAnchor,
                        isToday: day == today,
                        isSelected: day == SelectedDate);
                    cell = Cells[i];
                }
                else
                {
                    cell.IsSelected = day == SelectedDate;
                }
                PopulateCell(cell, day, buckets);
            }
        }

        UpdateSelectedDayTasks();
        // Same-month rebuilds reuse the existing cell instances, so
        // Cells.CollectionChanged stays silent; this notification is the
        // view's only signal for those rebuilds.
        OnPropertyChanged(nameof(Cells));
        OnPropertyChanged(nameof(MonthTitle));
        OnPropertyChanged(nameof(IsEmptyStateVisible));
    }

    private void PopulateCell(
        TodoCalendarDayCell cell,
        DateOnly day,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<TodoItem>> buckets)
    {
        if (buckets.TryGetValue(day, out var items))
        {
            var chips = items
                .Select(item => new TodoCalendarTaskChip(
                    item.Id,
                    item.Text,
                    item.IsCompleted,
                    TodoItem.GetColorMarkerHex(item.ColorMarker),
                    item.IsImportant,
                    item.Steps.Count > 0
                        ? $"{item.Steps.Count(step => step.IsCompleted)}/{item.Steps.Count}"
                        : null))
                .ToList();
            var (visible, overflow) = TodoCalendarProjection.AssignSlots(chips, MaxTasksPerCell);
            cell.SetCell(visible, overflow);
        }
        else
        {
            cell.SetCell(Array.Empty<TodoCalendarTaskChip>(), 0);
        }
    }

    private void UpdateSelectedDayTasks()
    {
        SelectedDayTasks.Clear();
        if (SelectedDate is not { } selected) return;

        var matching = _todo.Items
            .Where(vm => vm.Item.DueDate is { } due &&
                         DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(due, _tz).DateTime) == selected)
            .OrderBy(vm => vm.IsCompleted)
            .ThenBy(vm => vm.Item.DueDate)
            .ThenBy(vm => vm.Item.SortOrder)
            .ThenBy(vm => vm.Item.CreatedAt);

        foreach (var task in matching)
        {
            SelectedDayTasks.Add(task);
        }
    }

    private void ScheduleRebuildCells()
    {
        if (_disposed) return;

        if (Interlocked.Exchange(ref _rebuildGate, 1) == 0)
        {
            void Action()
            {
                Interlocked.Exchange(ref _rebuildGate, 0);
                RebuildCells();
            }

            if (_dispatcherQueue is not null)
            {
                _dispatcherQueue.TryEnqueue(Action);
            }
            else
            {
                Action();
            }
        }
    }

    private static DispatcherQueue? TryGetDispatcherQueue()
    {
        try
        {
            return DispatcherQueue.GetForCurrentThread();
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _todo.PropertyChanged -= Todo_PropertyChanged;
        _todo.Items.CollectionChanged -= TodoItems_CollectionChanged;

        if (_ownsTodoViewModel)
        {
            _todo.Dispose();
        }
    }
}
