using DeskBox.Models;
using DeskBox.Services;
using DeskBox.ViewModels;
using Xunit;

namespace DeskBox.Tests;

public sealed class QuickPanelViewModelTests
{
    private static (QuickPanelViewModel vm, QuickCaptureService quickCapture, SettingsService settings) CreateTestEnvironment(string testDir)
    {
        Directory.CreateDirectory(testDir);
        var quickCaptureStore = new QuickCaptureStore(Path.Combine(testDir, "quick-capture"));
        var quickCaptureService = new QuickCaptureService(quickCaptureStore);
        var settingsService = new SettingsService(testDir);
        var localizationService = new LocalizationService(settingsService);

        string widgetsDir = Path.Combine(testDir, "widgets");
        Directory.CreateDirectory(widgetsDir);

        var vm = new QuickPanelViewModel(
            quickCaptureService,
            settingsService,
            localizationService,
            clipboardService: null,
            todoStoreFactory: id => new TodoWidgetStore(widgetsDir, id),
            dispatcherQueue: null);

        return (vm, quickCaptureService, settingsService);
    }

    [Fact]
    public async Task ClipboardHistory_FilteringAndSearch_WorkCorrectly()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "deskbox-test-qp-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, quickCapture, _) = CreateTestEnvironment(testDir);

            // Add text, link, and image items
            await quickCapture.AddRecentClipboardItemAsync("Hello World text", 20);
            await quickCapture.AddRecentClipboardItemAsync("https://github.com/microsoft", 20);

            await vm.LoadClipboardHistoryAsync();
            Assert.NotEmpty(vm.FilteredClipboardItems);

            // Test text filter
            vm.SetClipboardFilter(QuickPanelFilter.Text);
            Assert.All(vm.FilteredClipboardItems, item => Assert.Equal(QuickCaptureItemType.Text, item.Type));

            // Test search
            vm.SearchText = "Hello";
            Assert.Single(vm.FilteredClipboardItems);
            Assert.Contains("Hello", vm.FilteredClipboardItems[0].Body);

            vm.SearchText = "nonexistent_query_xyz";
            Assert.Empty(vm.FilteredClipboardItems);
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task TodoManagement_CreateEditAndComplete_WorkCorrectly()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "deskbox-test-qp-todo-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, settings) = CreateTestEnvironment(testDir);

            // Configure a test Todo widget
            settings.Settings.Widgets.Add(new WidgetConfig
            {
                Id = "test-todo-widget",
                WidgetKind = WidgetKind.Todo,
                IsDisabled = false
            });

            await vm.LoadTodosAsync();

            // Create a new todo
            vm.BeginCreateTodo();
            Assert.True(vm.IsCreatingTodo);
            vm.ComposerTitle = "Test Todo Item";
            vm.ComposerNotes = "Important details";
            vm.ComposerDueDate = DateTimeOffset.Now;
            vm.ComposerIsImportant = true;

            await vm.SaveComposerTodoAsync();
            Assert.False(vm.IsCreatingTodo);

            // Verify loaded in Today view
            vm.SetTodoViewMode(QuickPanelTodoViewMode.Today);
            Assert.Single(vm.CurrentViewTodos);
            Assert.Equal("Test Todo Item", vm.CurrentViewTodos[0].Text);
            Assert.True(vm.CurrentViewTodos[0].IsImportant);

            // Toggle complete
            var createdItem = vm.CurrentViewTodos[0];
            await vm.ToggleCompleteTodoAsync(createdItem);

            vm.SetTodoViewMode(QuickPanelTodoViewMode.Completed);
            Assert.Single(vm.CurrentViewTodos);
            Assert.True(vm.CurrentViewTodos[0].IsCompleted);
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task NotesManagement_AutosaveAndExtractTodos_WorkCorrectly()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "deskbox-test-qp-notes-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, settings) = CreateTestEnvironment(testDir);

            settings.Settings.Widgets.Add(new WidgetConfig
            {
                Id = "test-todo-widget",
                WidgetKind = WidgetKind.Todo,
                IsDisabled = false
            });

            await vm.LoadNotesAsync();
            Assert.NotEmpty(vm.Notes);

            // Edit note content with checklist items
            vm.NoteTitle = "Sprint Planning";
            vm.NoteBody = "- [ ] Task 1: Design UI\r\n* Task 2: Implement logic\r\n1. Task 3: Write tests";
            await vm.SaveCurrentNoteAsync();
            Assert.Equal("已保存", vm.NoteSaveStatus);

            // Extract todos from note
            await vm.ExtractTodosFromNoteAsync();

            // Verify extracted todos appeared in Todo list
            await vm.LoadTodosAsync();
            vm.SetTodoViewMode(QuickPanelTodoViewMode.Today);
            Assert.Equal(3, vm.CurrentViewTodos.Count);
            Assert.Contains(vm.CurrentViewTodos, t => t.Text == "Task 1: Design UI");
            Assert.Contains(vm.CurrentViewTodos, t => t.Text == "Task 2: Implement logic");
            Assert.Contains(vm.CurrentViewTodos, t => t.Text == "Task 3: Write tests");
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, recursive: true); } catch { }
            }
        }
    }
}
