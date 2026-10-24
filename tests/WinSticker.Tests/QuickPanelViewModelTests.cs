using WinSticker.Models;
using WinSticker.Services;
using WinSticker.ViewModels;
using Xunit;

namespace WinSticker.Tests;

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
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-test-qp-" + Guid.NewGuid().ToString("N"));
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
    public async Task TodoManagement_WritesThroughSharedRepository()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-test-qp-todo-" + Guid.NewGuid().ToString("N"));
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

            // The Quick Panel no longer owns todo logic — the shared
            // TodoWidgetContent component does. The panel's only todo-side
            // duty left is extracting tasks from notes, which must land in
            // the shared store through the repository.
            vm.NoteTitle = "Sprint Planning";
            vm.NoteBody = "- [ ] Task 1: Design UI";
            await vm.SaveCurrentNoteAsync();
            await vm.ExtractTodosFromNoteAsync();

            var repository = TodoRepository.ForStore(new TodoWidgetStore(
                Path.Combine(testDir, "widgets"), "test-todo-widget"));
            TodoWidgetData data = await repository.LoadAsync();
            Assert.Contains(data.Items, item => item.Text == "Task 1: Design UI");
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
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-test-qp-notes-" + Guid.NewGuid().ToString("N"));
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

            // Verify extracted todos landed in the shared todo store
            var repository = TodoRepository.ForStore(new TodoWidgetStore(
                Path.Combine(testDir, "widgets"), "test-todo-widget"));
            TodoWidgetData data = await repository.LoadAsync();
            Assert.Equal(3, data.Items.Count);
            Assert.Contains(data.Items, t => t.Text == "Task 1: Design UI");
            Assert.Contains(data.Items, t => t.Text == "Task 2: Implement logic");
            Assert.Contains(data.Items, t => t.Text == "Task 3: Write tests");
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
