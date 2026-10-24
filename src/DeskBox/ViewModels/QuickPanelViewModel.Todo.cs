using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBox.Models;
using DeskBox.Services;

namespace DeskBox.ViewModels;

public sealed partial class QuickPanelViewModel
{
    [ObservableProperty]
    private QuickPanelTodoViewMode _todoViewMode = QuickPanelTodoViewMode.Today;

    [ObservableProperty]
    private TodoItem? _selectedTodo;

    [ObservableProperty]
    private int _todayCount;

    [ObservableProperty]
    private int _overdueCount;

    [ObservableProperty]
    private int _unscheduledCount;

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private bool _isCreatingTodo;

    [ObservableProperty]
    private bool _isEditingTodo;

    [ObservableProperty]
    private string? _editingTodoId;

    [ObservableProperty]
    private string _composerTitle = string.Empty;

    [ObservableProperty]
    private string _composerNotes = string.Empty;

    [ObservableProperty]
    private DateTimeOffset? _composerDueDate = DateTimeOffset.Now;

    [ObservableProperty]
    private bool _composerIsImportant;

    [ObservableProperty]
    private string _composerActionText = "添加";

    public ObservableCollection<TodoItem> CurrentViewTodos { get; } = [];

    private readonly List<TodoItem> _allTodos = [];
    private string? _activeTodoWidgetId;

    private void InitializeTodo()
    {
    }

    private void DisposeTodo()
    {
    }

    private string GetActiveTodoWidgetId()
    {
        if (_activeTodoWidgetId is not null) return _activeTodoWidgetId;

        var widget = _settingsService.Settings.WidgetLayout.Widgets
            .FirstOrDefault(w => w.WidgetKind == WidgetKind.Todo && !w.IsDisabled);

        _activeTodoWidgetId = widget?.Id ?? "todo";
        return _activeTodoWidgetId;
    }

    public async Task LoadTodosAsync()
    {
        try
        {
            string widgetId = GetActiveTodoWidgetId();
            var store = _todoStoreFactory(widgetId);
            var data = await store.LoadAsync();

            _allTodos.Clear();
            if (data.Items is { Count: > 0 })
            {
                _allTodos.AddRange(data.Items);
            }

            ComputeTodoCounts();
            ApplyTodoFilter();
        }
        catch (Exception ex)
        {
            App.Log($"[QuickPanel] Failed to load todos: {ex.Message}");
        }
    }

    private void ComputeTodoCounts()
    {
        var today = DateTime.Today;

        TodayCount = _allTodos.Count(item =>
            !item.IsCompleted && item.DueDate.HasValue && item.DueDate.Value.Date == today);

        OverdueCount = _allTodos.Count(item =>
            !item.IsCompleted && item.DueDate.HasValue && item.DueDate.Value.Date < today);

        UnscheduledCount = _allTodos.Count(item =>
            !item.IsCompleted && !item.DueDate.HasValue);

        CompletedCount = _allTodos.Count(item => item.IsCompleted);
    }

    [RelayCommand]
    public void SetTodoViewMode(QuickPanelTodoViewMode mode)
    {
        TodoViewMode = mode;
        ApplyTodoFilter();
    }

    public void ApplyTodoFilter()
    {
        RunOnUi(() =>
        {
            var today = DateTime.Today;
            IEnumerable<TodoItem> query = _allTodos;

            query = TodoViewMode switch
            {
                QuickPanelTodoViewMode.Today => query.Where(item =>
                    !item.IsCompleted && item.DueDate.HasValue && item.DueDate.Value.Date == today),

                QuickPanelTodoViewMode.Overdue => query.Where(item =>
                    !item.IsCompleted && item.DueDate.HasValue && item.DueDate.Value.Date < today),

                QuickPanelTodoViewMode.Unscheduled => query.Where(item =>
                    !item.IsCompleted && !item.DueDate.HasValue),

                QuickPanelTodoViewMode.Timeline => query.Where(item =>
                    !item.IsCompleted && item.DueDate.HasValue).OrderBy(item => item.DueDate),

                QuickPanelTodoViewMode.Completed => query.Where(item => item.IsCompleted),

                _ => query
            };

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.Trim();
                query = query.Where(item =>
                    (!string.IsNullOrEmpty(item.Text) && item.Text.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(item.Notes) && item.Notes.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            var results = query.ToList();

            CurrentViewTodos.Clear();
            foreach (var item in results)
            {
                CurrentViewTodos.Add(item);
            }
        });
    }

    [RelayCommand]
    public void BeginCreateTodo()
    {
        IsCreatingTodo = true;
        IsEditingTodo = false;
        EditingTodoId = null;
        ComposerTitle = string.Empty;
        ComposerNotes = string.Empty;
        ComposerDueDate = DateTimeOffset.Now;
        ComposerIsImportant = false;
        ComposerActionText = "添加";
    }

    [RelayCommand]
    public void BeginEditTodo(TodoItem? item)
    {
        if (item is null) return;

        IsCreatingTodo = true;
        IsEditingTodo = true;
        EditingTodoId = item.Id;
        ComposerTitle = item.Text;
        ComposerNotes = item.Notes ?? string.Empty;
        ComposerDueDate = item.DueDate;
        ComposerIsImportant = item.IsImportant;
        ComposerActionText = "保存";
    }

    [RelayCommand]
    public void CancelComposer()
    {
        IsCreatingTodo = false;
        IsEditingTodo = false;
        EditingTodoId = null;
        ComposerTitle = string.Empty;
        ComposerNotes = string.Empty;
    }

    [RelayCommand]
    public void SetComposerDateOption(string option)
    {
        ComposerDueDate = option switch
        {
            "Today" => DateTimeOffset.Now,
            "Tomorrow" => DateTimeOffset.Now.AddDays(1),
            "Unscheduled" => null,
            _ => ComposerDueDate
        };
    }

    [RelayCommand]
    public async Task SaveComposerTodoAsync()
    {
        if (string.IsNullOrWhiteSpace(ComposerTitle))
        {
            ShowToast("请输入任务标题");
            return;
        }

        string widgetId = GetActiveTodoWidgetId();
        var store = _todoStoreFactory(widgetId);
        var data = await store.LoadAsync();

        if (IsEditingTodo && EditingTodoId is not null)
        {
            var existing = data.Items.FirstOrDefault(i => i.Id == EditingTodoId);
            if (existing is not null)
            {
                existing.Text = ComposerTitle.Trim();
                existing.Notes = string.IsNullOrWhiteSpace(ComposerNotes) ? null : ComposerNotes.Trim();
                existing.DueDate = ComposerDueDate;
                existing.IsImportant = ComposerIsImportant;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
            ShowToast("已保存任务修改");
        }
        else
        {
            var newItem = new TodoItem
            {
                Text = ComposerTitle.Trim(),
                Notes = string.IsNullOrWhiteSpace(ComposerNotes) ? null : ComposerNotes.Trim(),
                DueDate = ComposerDueDate,
                IsImportant = ComposerIsImportant,
                SortOrder = data.Items.Count,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            data.Items.Insert(0, newItem);
            ShowToast("已添加新待办");
        }

        await store.SaveAsync(data);
        CancelComposer();
        await LoadTodosAsync();
    }

    [RelayCommand]
    public async Task ToggleCompleteTodoAsync(TodoItem? item)
    {
        if (item is null) return;

        string widgetId = GetActiveTodoWidgetId();
        var store = _todoStoreFactory(widgetId);
        var data = await store.LoadAsync();

        var existing = data.Items.FirstOrDefault(i => i.Id == item.Id);
        if (existing is not null)
        {
            existing.IsCompleted = !existing.IsCompleted;
            existing.CompletedAt = existing.IsCompleted ? DateTimeOffset.UtcNow : null;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await store.SaveAsync(data);
        }

        await LoadTodosAsync();
    }

    [RelayCommand]
    public async Task DeleteTodoAsync(TodoItem? item)
    {
        if (item is null) return;

        string widgetId = GetActiveTodoWidgetId();
        var store = _todoStoreFactory(widgetId);
        var data = await store.LoadAsync();

        data.Items.RemoveAll(i => i.Id == item.Id);
        await store.SaveAsync(data);

        await LoadTodosAsync();
        ShowToast("已删除任务");
    }
}
