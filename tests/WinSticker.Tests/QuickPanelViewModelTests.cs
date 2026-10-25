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

    [Fact]
    public async Task NotesSave_PreservesStickerAppearanceAndCreatesMarkdownNotes()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-test-qp-preserve-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, quickCapture, _) = CreateTestEnvironment(testDir);

            await vm.LoadNotesAsync();
            Assert.Single(vm.Notes);
            await quickCapture.SetAppearanceAsync([vm.SelectedNote!.Id], QuickCaptureAppearancePreset.StickyYellow);

            vm.NoteBody = "面板里改了正文";
            await vm.SaveCurrentNoteAsync();

            QuickCaptureItem saved = Assert.Single((await quickCapture.GetDataAsync()).Items);
            Assert.Equal(QuickCaptureAppearancePreset.StickyYellow, saved.AppearancePreset);
            Assert.Equal(TextContentFormat.Markdown, saved.ContentFormat);
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
    public async Task NotesTitle_SurvivesStickerSideBodyEdit()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-test-qp-title-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, quickCapture, _) = CreateTestEnvironment(testDir);

            await vm.LoadNotesAsync();
            vm.NoteTitle = "我的命名";
            await vm.SaveCurrentNoteAsync();

            // The sticker detail editor saves the body with a null title (patch = keep).
            await quickCapture.UpdateItemDetailsAsync(
                vm.SelectedNote!.Id,
                title: null,
                body: "贴纸侧改了正文",
                appearancePreset: null);

            await vm.LoadNotesAsync();
            Assert.Equal("我的命名", vm.SelectedNote!.Title);
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
    public async Task NotesList_IncludesLinkTypeNotesSoTheyDoNotVanish()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-test-qp-link-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, quickCapture, _) = CreateTestEnvironment(testDir);

            await quickCapture.AddDetailedItemAsync(
                "链接便签",
                "https://example.com/",
                QuickCaptureAppearancePreset.Default);
            await vm.LoadNotesAsync();

            QuickPanelNoteItem note = Assert.Single(vm.Notes);
            Assert.Equal("https://example.com/", note.Body);
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
    public async Task CloseNote_SoftClosesInsteadOfDeletingAndKeepsContent()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-test-qp-close-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, quickCapture, _) = CreateTestEnvironment(testDir);

            await vm.LoadNotesAsync();
            await vm.CreateNewNoteCommand.ExecuteAsync(null);
            var second = vm.Notes[^1];
            Assert.Equal(2, vm.Notes.Count);

            vm.SelectedNote = second;
            vm.NoteBody = "必须保留的内容";
            await vm.CloseNoteCommand.ExecuteAsync(second);

            Assert.Single(vm.Notes);
            Assert.Single(vm.ClosedNotes);
            Assert.Equal(second.Id, vm.ClosedNotes[0].Id);

            QuickCaptureStoreData data = await quickCapture.GetDataAsync();
            QuickCaptureItem stored = data.Items.First(i => i.Id == second.Id);
            Assert.False(stored.IsDeleted);
            Assert.Null(stored.ArchivedAt);
            Assert.Contains(second.Id, data.ClosedQuickPanelNotes!.Keys);
            Assert.Equal("必须保留的内容", stored.Body);
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
    public async Task ReopenNote_ReturnsSoftClosedNoteToTabs()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-test-qp-reopen-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, quickCapture, _) = CreateTestEnvironment(testDir);

            await vm.LoadNotesAsync();
            await vm.CreateNewNoteCommand.ExecuteAsync(null);
            var second = vm.Notes[^1];
            await vm.CloseNoteCommand.ExecuteAsync(second);

            await vm.ReopenNoteCommand.ExecuteAsync(vm.ClosedNotes[0]);

            Assert.Equal(2, vm.Notes.Count);
            Assert.Empty(vm.ClosedNotes);
            Assert.Equal(second.Id, vm.SelectedNote?.Id);

            QuickCaptureStoreData data = await quickCapture.GetDataAsync();
            QuickCaptureItem stored = data.Items.First(i => i.Id == second.Id);
            Assert.Null(stored.ArchivedAt);
            Assert.False(stored.IsDeleted);
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
    public async Task DeleteNote_TombstonesStoredItemAndReplacesLastNote()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-test-qp-delete-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, quickCapture, _) = CreateTestEnvironment(testDir);

            await vm.LoadNotesAsync();
            var only = vm.Notes[0];
            await vm.DeleteNoteCommand.ExecuteAsync(only);

            QuickCaptureStoreData data = await quickCapture.GetDataAsync();
            Assert.True(data.Items.First(i => i.Id == only.Id).IsDeleted);

            // Deleting the last open note with no closed notes recreates one.
            Assert.Single(vm.Notes);
            Assert.NotEqual(only.Id, vm.Notes[0].Id);
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
    public async Task CloseNote_AllowsClosingLastViewAndKeepsLibrary()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-test-qp-lastnote-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, _) = CreateTestEnvironment(testDir);

            await vm.LoadNotesAsync();
            await vm.CloseNoteCommand.ExecuteAsync(vm.Notes[0]);

            Assert.Empty(vm.Notes);
            Assert.Single(vm.ClosedNotes);
            Assert.Single(vm.AllNotes);
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
    public async Task LoadNotes_DoesNotAutoCreateWhenOnlyClosedNotesRemain()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-test-qp-closedonly-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (vm, _, _) = CreateTestEnvironment(testDir);

            await vm.LoadNotesAsync();
            await vm.CreateNewNoteCommand.ExecuteAsync(null);
            var second = vm.Notes[^1];
            await vm.CloseNoteCommand.ExecuteAsync(second);
            await vm.DeleteNoteCommand.ExecuteAsync(vm.Notes[0]);

            // Last open note deleted while a closed note survives: no fresh
            // empty note is created, and a reload keeps that state stable.
            Assert.Empty(vm.Notes);
            Assert.Single(vm.ClosedNotes);
            await vm.LoadNotesAsync();
            Assert.Empty(vm.Notes);
            Assert.Single(vm.ClosedNotes);
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
    public async Task NativeEditorFlush_PersistsPendingTextToFormerNoteBeforeSwitch()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-native-note-switch-" + Guid.NewGuid().ToString("N"));
        var (vm, store, _) = CreateTestEnvironment(testDir);
        using (vm)
        {
            await vm.LoadNotesAsync();
            var original = Assert.IsType<QuickPanelNoteItem>(vm.SelectedNote);
            vm.FlushNoteEditor = () =>
            {
                if (ReferenceEquals(vm.SelectedNote, original)) vm.NoteBody = "last native input\n\n**kept**";
            };
            await vm.CreateNewNoteAsync();
            await vm.SaveCurrentNoteAsync();
            var data = await store.GetDataAsync();
            Assert.Equal("last native input\n\n**kept**", data.Items.Single(item => item.Id == original.Id).Body);
            Assert.NotEqual(original.Id, vm.SelectedNote!.Id);
            Assert.Equal(string.Empty, vm.NoteBody);
            Assert.Equal(string.Empty, data.Items.Single(item => item.Id == vm.SelectedNote.Id).Body);
        }
    }

    [Fact]
    public async Task NativeEditorFlush_ClosingSelectedNoteKeepsItsLatestText()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-native-note-close-" + Guid.NewGuid().ToString("N"));
        var (vm, store, _) = CreateTestEnvironment(testDir);
        using (vm)
        {
            await vm.LoadNotesAsync();
            await vm.CreateNewNoteAsync();
            var closing = Assert.IsType<QuickPanelNoteItem>(vm.SelectedNote);
            vm.FlushNoteEditor = () =>
            {
                if (ReferenceEquals(vm.SelectedNote, closing)) vm.NoteBody = "pending before close";
            };
            await vm.CloseNoteAsync(closing);
            var data = await store.GetDataAsync();
            var saved = data.Items.Single(item => item.Id == closing.Id);
            Assert.Equal("pending before close", saved.Body);
            Assert.Null(saved.ArchivedAt);
            Assert.Contains(closing.Id, (await store.GetDataAsync()).ClosedQuickPanelNotes!.Keys);
            Assert.NotEqual(closing.Id, vm.SelectedNote!.Id);
        }
    }
    [Fact]
    public async Task CleanQuickPanelRefresh_DoesNotOverwriteAnExternalDesktopEdit()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-native-note-refresh-" + Guid.NewGuid().ToString("N"));
        var (vm, store, _) = CreateTestEnvironment(testDir);
        using (vm)
        {
            await vm.LoadNotesAsync();
            string id = vm.SelectedNote!.Id;
            vm.NoteBody = "old quick-panel content";
            await vm.SaveCurrentNoteAsync();
            await store.UpdateItemDetailsAsync(id, vm.NoteTitle, "new desktop content");
            await vm.LoadNotesAsync();
            Assert.Equal("new desktop content", vm.NoteBody);
        }
    }

    [Fact]
    public async Task MarkdownNotesWithAttachments_RemainInQuickPanelNotes()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "winsticker-native-note-attachment-" + Guid.NewGuid().ToString("N"));
        var (vm, store, _) = CreateTestEnvironment(testDir);
        using (vm)
        {
            await vm.LoadNotesAsync();
            string id = vm.SelectedNote!.Id;
            string attachment = Path.Combine(testDir, "attachment.txt");
            await File.WriteAllTextAsync(attachment, "attachment fixture");
            await store.AddAttachmentsAsync(id, [attachment], copyToManagedStorage: true);
            await store.UpdateItemDetailsAsync(id, vm.NoteTitle, "# Markdown note with attachment");
            await vm.LoadNotesAsync();
            var loaded = Assert.Single(vm.Notes);
            Assert.Equal(id, loaded.Id);
            Assert.Single(loaded.Attachments);
            Assert.Equal(TextContentFormat.Markdown, loaded.ContentFormat);
            Assert.Equal("# Markdown note with attachment", vm.NoteBody);
        }
    }
}
