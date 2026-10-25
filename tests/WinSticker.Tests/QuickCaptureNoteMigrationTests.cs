using System.Text.Json;
using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class QuickCaptureNoteMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "WinSticker.Tests", Guid.NewGuid().ToString("N"));

    private string StoreRoot
    {
        get { Directory.CreateDirectory(_root); return _root; }
    }

    private QuickCaptureStore Store => new(StoreRoot);

    private static QuickCaptureItem Note(
        string id,
        string body,
        string? title = null,
        TextContentFormat format = TextContentFormat.Markdown,
        int sortOrder = 0) =>
        new()
        {
            Id = id,
            Body = body,
            Title = title,
            ContentFormat = format,
            IsNote = true,
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.Parse("2026-10-20T08:00:00+08:00"),
            UpdatedAt = DateTimeOffset.Parse("2026-10-21T08:00:00+08:00")
        };

    /// <summary>Writes an old-format aggregate exactly like pre-migration builds did.</summary>
    private async Task WriteLegacyStoreAsync(QuickCaptureStoreData data)
    {
        data.Version = 5;
        data.NotesStorageVersion = 0;
        Directory.CreateDirectory(StoreRoot);
        await File.WriteAllTextAsync(
            Path.Combine(StoreRoot, "quick-capture.json"),
            JsonSerializer.Serialize(data, QuickCaptureJsonContext.Default.StoreData));
    }

    private static async Task<QuickCaptureStoreData> ReadRawStoreAsync(string path) =>
        JsonSerializer.Deserialize(
            await File.ReadAllTextAsync(path),
            QuickCaptureJsonContext.Default.StoreData)!;

    [Fact]
    public async Task LegacyMixedStore_MigratesNotesToFilesAndKeepsRecordsInJson()
    {
        string imagePath = Path.Combine(StoreRoot, "images", "abc.png");
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        await File.WriteAllTextAsync(imagePath, "png");

        var legacy = new QuickCaptureStoreData
        {
            ClosedQuickPanelNotes = new() { ["note1"] = DateTimeOffset.Parse("2026-10-22T09:00:00+08:00") },
            Items =
            [
                Note("note1", "# 第一条", title: "标题一", sortOrder: 0),
                Note("note2", "纯文本 # 不是标题", format: TextContentFormat.PlainText, sortOrder: 1),
                Note("blankdraft", "", sortOrder: 2),
                new QuickCaptureItem
                {
                    Id = "imgrec", Body = "Image", Type = QuickCaptureItemType.Image,
                    ImagePath = imagePath, ContentHash = "abc",
                    Attachments = [new() { FilePath = imagePath, Type = "image", StorageMode = TodoAttachment.ManagedStorageMode }],
                    SortOrder = 3
                },
                new QuickCaptureItem
                {
                    Id = "deleteme", IsDeleted = true, SortOrder = 4,
                    CreatedAt = DateTimeOffset.Parse("2026-10-19T08:00:00+08:00"),
                    UpdatedAt = DateTimeOffset.Parse("2026-10-19T09:00:00+08:00")
                },
            ],
            RecentItems =
            [
                new QuickCaptureItem { Id = "clip1", Body = "剪贴板文本", IsRecent = true, SortOrder = 0 }
            ]
        };
        await WriteLegacyStoreAsync(legacy);

        var service = new QuickCaptureService(Store);
        QuickCaptureStoreData migrated = await service.GetDataAsync();

        string notesDirectory = Path.Combine(StoreRoot, "notes");
        Assert.True(File.Exists(Path.Combine(notesDirectory, "note1.md")));
        Assert.True(File.Exists(Path.Combine(notesDirectory, "note2.md")));
        Assert.True(File.Exists(Path.Combine(notesDirectory, "blankdraft.md")));

        QuickCaptureStoreData raw = await ReadRawStoreAsync(Path.Combine(StoreRoot, "quick-capture.json"));
        Assert.Equal(QuickCaptureStore.CurrentNotesStorageVersion, raw.NotesStorageVersion);
        // Records and the legacy tombstone stay in the aggregate; notes are gone.
        Assert.DoesNotContain(raw.Items, item => item.Id is "note1" or "note2" or "blankdraft");
        Assert.Contains(raw.Items, item => item.Id == "imgrec");
        Assert.Contains(raw.Items, item => item.Id == "deleteme" && item.IsDeleted);
        Assert.Contains(raw.RecentItems, item => item.Id == "clip1");
        Assert.Equal(legacy.ClosedQuickPanelNotes, raw.ClosedQuickPanelNotes);

        // The consumer projection merges both stores back into one list with
        // the original order and content.
        Assert.Equal(5, migrated.Items.Count);
        Assert.Equal("note1", migrated.Items[0].Id);
        Assert.Equal("# 第一条", migrated.Items[0].Body);
        Assert.Equal("标题一", migrated.Items[0].Title);
        Assert.Equal("纯文本 # 不是标题", migrated.Items[1].Body);
        Assert.Equal(TextContentFormat.PlainText, migrated.Items[1].ContentFormat);
        Assert.Empty(migrated.Items[2].Body);
        Assert.True(migrated.Items[2].IsNote);
        Assert.Equal("imgrec", migrated.Items[3].Id);
        Assert.Equal("clip1", migrated.RecentItems.Single().Id);

        // An independent recovery copy of the source was kept.
        Assert.True(File.Exists(Path.Combine(StoreRoot, "quick-capture.json.pre-notes-migration")));
        Assert.False(Directory.Exists(Path.Combine(StoreRoot, NoteFileStore.MigrationDirectoryName)));
    }

    [Fact]
    public async Task EditingOneNote_CommitsOnlyThatFile()
    {
        var legacy = new QuickCaptureStoreData
        {
            Items = [Note("notea", "A 内容", sortOrder: 0), Note("noteb", "B 内容", sortOrder: 1)]
        };
        await WriteLegacyStoreAsync(legacy);

        var service = new QuickCaptureService(Store);
        await service.GetDataAsync();

        string jsonPath = Path.Combine(StoreRoot, "quick-capture.json");
        byte[] jsonBefore = await File.ReadAllBytesAsync(jsonPath);
        string pathA = Path.Combine(StoreRoot, "notes", "notea.md");
        string pathB = Path.Combine(StoreRoot, "notes", "noteb.md");
        DateTime writeB = File.GetLastWriteTimeUtc(pathB);

        await Task.Delay(20);
        QuickCaptureWriteResult result = await service.PatchNoteAsync("notea", body: "A 新内容");

        Assert.True(result.Saved);
        Assert.Equal("A 新内容", (await File.ReadAllTextAsync(pathA)).Split('\n')[^1]);
        Assert.True(File.GetLastWriteTimeUtc(pathB) <= writeB);
        Assert.Equal(jsonBefore, await File.ReadAllBytesAsync(jsonPath));

        QuickCaptureStoreData reloaded = await new QuickCaptureService(Store).GetDataAsync();
        Assert.Equal("A 新内容", reloaded.Items.Single(item => item.Id == "notea").Body);
        Assert.Equal("B 内容", reloaded.Items.Single(item => item.Id == "noteb").Body);
    }

    [Fact]
    public async Task NoteSave_RaisesTargetedNotificationWithoutLegacyChanged()
    {
        var service = new QuickCaptureService(Store);
        var created = await service.CreateNoteAsync();

        QuickCaptureChange? detailed = null;
        int legacy = 0;
        service.ChangeDetailed += change => detailed = change;
        service.Changed += () => legacy++;

        await service.PatchNoteAsync(created.Id, body: "新正文");

        Assert.NotNull(detailed);
        Assert.Equal(QuickCaptureChangeKind.NoteSaved, detailed!.Kind);
        Assert.Contains(created.Id, detailed.NoteIds);
        Assert.Equal(0, legacy);
    }

    [Fact]
    public async Task HostStateSave_LeavesNoteFilesUntouched()
    {
        var legacy = new QuickCaptureStoreData
        {
            Items = [Note("notea", "A 内容")]
        };
        await WriteLegacyStoreAsync(legacy);

        var service = new QuickCaptureService(Store);
        await service.GetDataAsync();

        string notePath = Path.Combine(StoreRoot, "notes", "notea.md");
        DateTime noteWrite = File.GetLastWriteTimeUtc(notePath);
        await Task.Delay(20);

        await service.SetItemClosedAsync("notea", true);

        Assert.True(File.GetLastWriteTimeUtc(notePath) <= noteWrite);
        QuickCaptureStoreData raw = await ReadRawStoreAsync(Path.Combine(StoreRoot, "quick-capture.json"));
        Assert.Contains("notea", raw.ClosedQuickPanelNotes!.Keys);
    }

    [Fact]
    public async Task InterruptedAfterPublish_RetryCompletesWithoutDuplicates()
    {
        var legacy = new QuickCaptureStoreData
        {
            Items = [Note("notea", "A 内容"), Note("noteb", "B 内容")]
        };
        await WriteLegacyStoreAsync(legacy);

        // First run migrates fully.
        var first = new QuickCaptureService(Store);
        await first.GetDataAsync();

        // Simulate "published the directory but the marker write never
        // happened": roll the aggregate back to the legacy document while
        // keeping the published notes directory.
        await WriteLegacyStoreAsync(legacy);

        var second = new QuickCaptureService(Store);
        QuickCaptureStoreData migrated = await second.GetDataAsync();

        Assert.Equal(2, migrated.Items.Count);
        Assert.Equal(
            ["notea.md", "noteb.md"],
            Directory.GetFiles(Path.Combine(StoreRoot, "notes"))
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
        QuickCaptureStoreData raw = await ReadRawStoreAsync(Path.Combine(StoreRoot, "quick-capture.json"));
        Assert.Equal(QuickCaptureStore.CurrentNotesStorageVersion, raw.NotesStorageVersion);
        Assert.Empty(raw.Items.Where(item => item.Id is "notea" or "noteb"));
    }

    [Fact]
    public async Task StaleMigrationStaging_IsRegeneratedNotTrusted()
    {
        var legacy = new QuickCaptureStoreData { Items = [Note("notea", "A 内容")] };
        await WriteLegacyStoreAsync(legacy);

        string staging = Path.Combine(StoreRoot, NoteFileStore.MigrationDirectoryName);
        Directory.CreateDirectory(staging);
        await File.WriteAllTextAsync(Path.Combine(staging, "ghost.md"), "陈旧残留");

        var service = new QuickCaptureService(Store);
        QuickCaptureStoreData migrated = await service.GetDataAsync();

        Assert.Single(migrated.Items);
        Assert.Equal(["notea.md"], Directory.GetFiles(Path.Combine(StoreRoot, "notes")).Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public async Task DeleteAndUndo_RoundTripThroughNoteFiles()
    {
        var service = new QuickCaptureService(Store);
        var created = await service.CreateNoteAsync();
        await service.PatchNoteAsync(created.Id, title: "标题", body: "要删的内容");

        QuickCaptureDeletedItemSnapshot? snapshot = await service.DeleteItemAsync(created.Id);
        Assert.NotNull(snapshot);

        string notePath = Path.Combine(StoreRoot, "notes", $"{created.Id}.md");
        Assert.True(File.Exists(notePath));
        Assert.DoesNotContain("要删的内容", await File.ReadAllTextAsync(notePath));

        QuickCaptureStoreData afterDelete = await new QuickCaptureService(Store).GetDataAsync();
        Assert.Contains(afterDelete.Items, item => item.Id == created.Id && item.IsDeleted);

        Assert.True(await service.RestoreDeletedItemAsync(snapshot));
        QuickCaptureStoreData afterUndo = await new QuickCaptureService(Store).GetDataAsync();
        QuickCaptureItem restored = afterUndo.Items.Single(item => item.Id == created.Id);
        Assert.False(restored.IsDeleted);
        Assert.Equal("要删的内容", restored.Body);
        Assert.Equal("标题", restored.Title);
    }

    [Fact]
    public async Task NewTextCaptures_BecomeIndependentNotes()
    {
        var service = new QuickCaptureService(Store);

        var captured = await service.AddItemAsync("快速捕获的文本");

        Assert.True(captured.IsNote);
        Assert.True(File.Exists(Path.Combine(StoreRoot, "notes", $"{captured.Id}.md")));
        QuickCaptureStoreData raw = await ReadRawStoreAsync(Path.Combine(StoreRoot, "quick-capture.json"));
        Assert.DoesNotContain(raw.Items, item => item.Id == captured.Id);

        QuickCaptureStoreData reloaded = await new QuickCaptureService(Store).GetDataAsync();
        QuickCaptureItem item = reloaded.Items.Single(entry => entry.Id == captured.Id);
        Assert.Equal("快速捕获的文本", item.Body);
        Assert.Equal(0, item.SortOrder);
    }

    [Fact]
    public async Task UnreadableNoteFile_FailsAlone()
    {
        var legacy = new QuickCaptureStoreData { Items = [Note("notea", "A 内容"), Note("noteb", "B 内容")] };
        await WriteLegacyStoreAsync(legacy);
        var service = new QuickCaptureService(Store);
        await service.GetDataAsync();

        string badPath = Path.Combine(StoreRoot, "notes", "badnote.md");
        await File.WriteAllTextAsync(badPath, "<!-- winsticker-note\n{broken\n-->\n内容");

        var reloaded = new QuickCaptureService(Store);
        QuickCaptureStoreData data = await reloaded.GetDataAsync();

        Assert.Contains(data.Items, item => item.Id == "notea");
        Assert.Contains(data.Items, item => item.Id == "noteb");
        Assert.Single(reloaded.NoteFileErrors, error => error.NoteId == "badnote");
        // The corrupt source file is preserved, not wiped.
        Assert.True(File.Exists(badPath));
    }

    [Fact]
    public async Task MoveItem_PersistsOrderAcrossNoteFiles()
    {
        var legacy = new QuickCaptureStoreData
        {
            Items = [Note("notea", "A", sortOrder: 0), Note("noteb", "B", sortOrder: 1), Note("notec", "C", sortOrder: 2)]
        };
        await WriteLegacyStoreAsync(legacy);
        var service = new QuickCaptureService(Store);
        await service.GetDataAsync();

        string pathA = Path.Combine(StoreRoot, "notes", "notea.md");
        string pathC = Path.Combine(StoreRoot, "notes", "notec.md");
        DateTime writeA = File.GetLastWriteTimeUtc(pathA);
        await Task.Delay(20);

        await service.MoveItemAsync("notec", 0);

        // Reordering persists through the note files themselves and
        // round-trips on reload as dense consumer-facing positions.
        Assert.True(File.GetLastWriteTimeUtc(pathA) > writeA);
        Assert.True(File.GetLastWriteTimeUtc(pathC) > writeA);
        QuickCaptureStoreData reloaded = await new QuickCaptureService(Store).GetDataAsync();
        Assert.Equal(["notec", "notea", "noteb"], reloaded.Items.Select(item => item.Id).ToArray());
        Assert.Equal([0, 1, 2], reloaded.Items.Select(item => item.SortOrder).ToArray());
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { }
    }
}
