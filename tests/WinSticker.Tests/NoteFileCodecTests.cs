using System.IO;
using System.Text;
using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class NoteFileCodecTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "WinSticker.Tests", Guid.NewGuid().ToString("N"));

    private string NotesDirectory
    {
        get
        {
            string dir = Path.Combine(_root, "quick-capture", "notes");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    [Fact]
    public void RoundTrip_PreservesBodyVerbatim()
    {
        string[] bodies =
        [
            string.Empty,
            "\n",
            "空白草稿之前先有一个换行",
            "中文与 emoji 🎉 混排",
            "line1\r\nline2\nline3",
            "# 标题\n\n- [ ] 待办\n\n```csharp\nvar x = 1;\n```\n",
            "<!-- winsticker-note\n{\"fake\":true}\n-->\n正文以假头部开头",
            "--> 正文以结束符开头",
            "尾随空格  \n\t制表符",
        ];

        foreach (string body in bodies)
        {
            var item = new QuickCaptureItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Body = body,
                ContentFormat = TextContentFormat.Markdown
            };

            byte[] bytes = NoteFileCodec.EncodeUtf8(item, NotesDirectory);
            NoteFileDecodeResult decoded = NoteFileCodec.Decode(
                bytes, item.Id, NotesDirectory);

            Assert.Equal(NoteFileDecodeStatus.Loaded, decoded.Status);
            Assert.Equal(body, decoded.Item!.Body);
            Assert.Equal(body, decoded.Body);
        }
    }

    [Fact]
    public void RoundTrip_PreservesBusinessFields()
    {
        var now = DateTimeOffset.Parse("2026-10-25T12:00:00+08:00");
        var item = new QuickCaptureItem
        {
            Id = "739530025dc24e37983ec328fbff9d142",
            Title = "项目 --> 记录",
            Body = "# 内容",
            ContentFormat = TextContentFormat.Markdown,
            Type = QuickCaptureItemType.Text,
            Url = "https://example.com",
            IsNote = true,
            IsPinned = true,
            SortOrder = 3,
            PinnedSortOrder = 1,
            AppearancePreset = QuickCaptureAppearancePreset.StickyYellow,
            SourceKind = QuickCaptureSourceKind.Manual,
            Tags = ["a", "b"],
            CreatedAt = now,
            UpdatedAt = now.AddMinutes(10),
            DeviceId = "device-1",
            Attachments =
            [
                new TodoAttachment
                {
                    Id = "att1", FilePath = Path.Combine(_root, "file.txt"),
                    DisplayName = "file.txt", Type = "file",
                    StorageMode = TodoAttachment.LinkedStorageMode, AddedAt = now
                }
            ]
        };

        byte[] bytes = NoteFileCodec.EncodeUtf8(item, NotesDirectory);
        NoteFileDecodeResult decoded = NoteFileCodec.Decode(bytes, item.Id, NotesDirectory);
        QuickCaptureItem round = decoded.Item!;

        Assert.Equal(NoteFileDecodeStatus.Loaded, decoded.Status);
        Assert.Equal(item.Id, round.Id);
        Assert.Equal(item.Title, round.Title);
        Assert.Equal(item.Body, round.Body);
        Assert.Equal(item.ContentFormat, round.ContentFormat);
        Assert.Equal(item.Type, round.Type);
        Assert.Equal(item.Url, round.Url);
        Assert.True(round.IsNote);
        Assert.Equal(item.IsPinned, round.IsPinned);
        Assert.Equal(item.SortOrder, round.SortOrder);
        Assert.Equal(item.PinnedSortOrder, round.PinnedSortOrder);
        Assert.Equal(item.AppearancePreset, round.AppearancePreset);
        Assert.Equal(item.SourceKind, round.SourceKind);
        Assert.Equal(item.Tags, round.Tags);
        Assert.Equal(item.CreatedAt, round.CreatedAt);
        Assert.Equal(item.UpdatedAt, round.UpdatedAt);
        Assert.Equal(item.DeviceId, round.DeviceId);
        Assert.Equal(item.Attachments.Single().FilePath, round.Attachments.Single().FilePath);
        Assert.False(round.IsDeleted);
    }

    [Fact]
    public void PlainTextBody_IsNotReinterpreted()
    {
        var item = new QuickCaptureItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Body = "# 不是标题 *也不是强调* `更不是代码`",
            ContentFormat = TextContentFormat.PlainText
        };

        byte[] bytes = NoteFileCodec.EncodeUtf8(item, NotesDirectory);
        NoteFileDecodeResult decoded = NoteFileCodec.Decode(bytes, item.Id, NotesDirectory);

        Assert.Equal(TextContentFormat.PlainText, decoded.Item!.ContentFormat);
        Assert.Equal(item.Body, decoded.Item.Body);
    }

    [Fact]
    public void TitleWithCommentTerminator_CannotEndHeaderEarly()
    {
        var item = new QuickCaptureItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "evil --> title",
            Body = "正文"
        };

        byte[] bytes = NoteFileCodec.EncodeUtf8(item, NotesDirectory);
        // The JSON header line itself must not contain an unescaped "-->".
        string text = Encoding.UTF8.GetString(bytes);
        string jsonLine = text.Split('\n')[1];
        Assert.DoesNotContain("-->", jsonLine);
        NoteFileDecodeResult decoded = NoteFileCodec.Decode(bytes, item.Id, NotesDirectory);
        Assert.Equal(NoteFileDecodeStatus.Loaded, decoded.Status);
        Assert.Equal("evil --> title", decoded.Item!.Title);
    }

    [Fact]
    public void HeaderMarker_IsOnlyRecognizedAtFileStart()
    {
        string body = "前言\n<!-- winsticker-note\n{\"id\":\"x\"}\n-->\n后续";
        var item = new QuickCaptureItem { Id = Guid.NewGuid().ToString("N"), Body = body };

        byte[] bytes = NoteFileCodec.EncodeUtf8(item, NotesDirectory);
        NoteFileDecodeResult decoded = NoteFileCodec.Decode(bytes, item.Id, NotesDirectory);

        // The file's own header comes first; the body's lookalike stays body.
        Assert.Equal(NoteFileDecodeStatus.Loaded, decoded.Status);
        Assert.Equal(body, decoded.Item!.Body);
    }

    [Fact]
    public async Task HeaderlessFile_DecoWholeTextAsBody()
    {
        string path = Path.Combine(NotesDirectory, "abc123.md");
        await File.WriteAllTextAsync(path, "没有头部的正文");

        var store = new NoteFileStore(Path.Combine(_root, "quick-capture"));
        NoteFileLoadResult loaded = await store.LoadAsync("abc123");

        Assert.Equal(NoteFileLoadStatus.NoHeader, loaded.Status);
        Assert.Equal("没有头部的正文", loaded.Item!.Body);
        Assert.Equal("abc123", loaded.Item.Id);
    }

    [Fact]
    public void BomPrefixedFile_Decodes()
    {
        var item = new QuickCaptureItem { Id = Guid.NewGuid().ToString("N"), Body = "内容" };
        byte[] encoded = NoteFileCodec.EncodeUtf8(item, NotesDirectory);
        byte[] withBom = [0xEF, 0xBB, 0xBF, .. encoded];

        NoteFileDecodeResult decoded = NoteFileCodec.Decode(withBom, item.Id, NotesDirectory);

        Assert.Equal(NoteFileDecodeStatus.Loaded, decoded.Status);
        Assert.Equal("内容", decoded.Item!.Body);
    }

    [Fact]
    public async Task InvalidUtf8_IsAnErrorNotSilentReplacement()
    {
        byte[] invalid = [0x23, 0xFF, 0xFE, 0x74];

        Assert.Throws<DecoderFallbackException>(
            () => NoteFileCodec.Decode(invalid, "abc", NotesDirectory));

        string path = Path.Combine(NotesDirectory, "badutf8.md");
        await File.WriteAllBytesAsync(path, invalid);
        var store = new NoteFileStore(Path.Combine(_root, "quick-capture"));
        NoteFileLoadResult loaded = await store.LoadAsync("badutf8");

        Assert.Equal(NoteFileLoadStatus.CorruptHeader, loaded.Status);
        // The source file survives untouched for manual recovery.
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task FutureVersion_IsUnsupportedAndPreserved()
    {
        string notesDirectory = NotesDirectory;
        string json = """{"version":2,"id":"futurenote","title":"t"}""";
        string path = Path.Combine(notesDirectory, "futurenote.md");
        await File.WriteAllTextAsync(path, $"<!-- winsticker-note\n{json}\n-->\n正文");

        var store = new NoteFileStore(Path.Combine(_root, "quick-capture"));
        NoteFileLoadResult loaded = await store.LoadAsync("futurenote");

        Assert.Equal(NoteFileLoadStatus.UnsupportedVersion, loaded.Status);
        Assert.Contains("\"version\":2", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task HeaderIdMismatchingFileName_IsRejected()
    {
        string notesDirectory = NotesDirectory;
        string json = """{"version":1,"id":"otherid","title":"t"}""";
        await File.WriteAllTextAsync(
            Path.Combine(notesDirectory, "filenote.md"),
            $"<!-- winsticker-note\n{json}\n-->\n正文");

        var store = new NoteFileStore(Path.Combine(_root, "quick-capture"));
        NoteFileLoadResult loaded = await store.LoadAsync("filenote");

        Assert.Equal(NoteFileLoadStatus.IdMismatch, loaded.Status);
    }

    [Fact]
    public async Task UnknownMetadataFields_SurviveStoreRoundTrip()
    {
        string notesDirectory = NotesDirectory;
        var item = new QuickCaptureItem
        {
            Id = "unknownfieldnote",
            Title = "带未知字段",
            Body = "正文"
        };
        var store = new NoteFileStore(Path.Combine(_root, "quick-capture"));
        Assert.Equal(NoteFileCommitStatus.Saved, (await store.SaveAsync(item)).Status);

        // Simulate a future version having added a field: splice it into the
        // header's JSON object, right before its closing brace.
        string path = Path.Combine(notesDirectory, "unknownfieldnote.md");
        string[] lines = (await File.ReadAllTextAsync(path)).Split('\n');
        lines[1] = lines[1].Insert(lines[1].Length - 1, ",\"futureField\":{\"nested\":42}");
        await File.WriteAllTextAsync(path, string.Join('\n', lines));

        // Committing the stale in-memory copy without reloading is a
        // conflict; the external edit must not be clobbered.
        Assert.Equal(NoteFileCommitStatus.Conflict, (await store.SaveAsync(item)).Status);

        // After a reload the external version is the baseline; the unknown
        // field must survive the next commit through the store.
        NoteFileLoadResult reloaded = await store.LoadAsync(item.Id);
        Assert.Equal(NoteFileLoadStatus.Loaded, reloaded.Status);
        QuickCaptureItem loaded = reloaded.Item!;
        loaded.Body = "编辑后的正文";
        Assert.Equal(
            NoteFileCommitStatus.Saved,
            (await store.SaveAsync(loaded)).Status);
        string saved = await File.ReadAllTextAsync(path);
        Assert.Contains("futureField", saved);
        Assert.Equal("编辑后的正文", NoteFileCodec.Decode(
            await File.ReadAllBytesAsync(path), item.Id, notesDirectory).Item!.Body);
    }

    [Fact]
    public async Task SaveRotatesBackupAndCommitIsAtomicStyle()
    {
        var store = new NoteFileStore(Path.Combine(_root, "quick-capture"));
        var item = new QuickCaptureItem { Id = "rotatednote", Body = "v1" };
        await store.SaveAsync(item);

        item.Body = "v2";
        await store.SaveAsync(item);

        NoteFileLoadResult current = await store.LoadAsync("rotatednote");
        Assert.Equal("v2", current.Item!.Body);
        string backupPath = store.GetNoteFilePath("rotatednote") + ".bak";
        Assert.True(File.Exists(backupPath));
        Assert.Contains("v1", await File.ReadAllTextAsync(backupPath));
        Assert.DoesNotContain(".tmp", Path.GetFileName(backupPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(backupPath)!, "*.tmp"));
    }

    [Fact]
    public async Task ExternalModification_BlocksCommitAsConflict()
    {
        var store = new NoteFileStore(Path.Combine(_root, "quick-capture"));
        var item = new QuickCaptureItem { Id = "conflictnote", Body = "原始" };
        await store.SaveAsync(item);

        // External editor rewrites the file behind the store's back.
        string path = store.GetNoteFilePath("conflictnote");
        string external = NoteFileCodec.Encode(
            new QuickCaptureItem { Id = "conflictnote", Body = "外部修改" },
            Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, external);

        item.Body = "内存草稿";
        NoteFileCommitResult result = await store.SaveAsync(item);

        Assert.Equal(NoteFileCommitStatus.Conflict, result.Status);
        // Neither version is lost: the disk still holds the external edit.
        Assert.Contains("外部修改", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public void HostileNoteIds_GetSafeReversibleFileNames()
    {
        string[] hostile =
        [
            "..",
            "a/b",
            "a\\b",
            "CON",
            "nul.txt",
            "endsindot.",
            "space name",
            "你好",
            "~tilde",
        ];

        foreach (string id in hostile)
        {
            string fileName = NoteFileStore.BuildNoteFileName(id);
            Assert.NotEqual(id + ".md", fileName);
            Assert.DoesNotContain("..", fileName);
            Assert.True(NoteFileStore.TryGetNoteIdFromFileName(fileName, out string roundTrip));
            Assert.Equal(id, roundTrip);
        }

        // Long but legal ids stay literal; only ids whose reversible
        // encoding cannot fit a file name surface as an error.
        string longId = new string('x', 96);
        Assert.Equal(longId + ".md", NoteFileStore.BuildNoteFileName(longId));
        Assert.ThrowsAny<InvalidOperationException>(
            () => NoteFileStore.BuildNoteFileName(new string('x', 200)));

        // Regular guid-style ids stay literal.
        string guid = Guid.NewGuid().ToString("N");
        Assert.Equal(guid + ".md", NoteFileStore.BuildNoteFileName(guid));
        Assert.True(NoteFileStore.TryGetNoteIdFromFileName(guid + ".md", out string parsed));
        Assert.Equal(guid, parsed);

        // Non-note files never map to an id.
        Assert.False(NoteFileStore.TryGetNoteIdFromFileName("x.md.bak", out _));
        Assert.False(NoteFileStore.TryGetNoteIdFromFileName("x.md.abc.tmp", out _));
        Assert.False(NoteFileStore.TryGetNoteIdFromFileName("migration.json", out _));
    }

    [Fact]
    public async Task ManagedAttachmentPaths_TravelRelativeExternalStayAbsolute()
    {
        string quickCapture = Directory.CreateDirectory(Path.Combine(_root, "quick-capture")).FullName;
        string notesDirectory = Path.Combine(quickCapture, "notes");
        Directory.CreateDirectory(notesDirectory);
        Directory.CreateDirectory(Path.Combine(quickCapture, "attachments", "pathnote"));
        string managed = Path.Combine(quickCapture, "attachments", "pathnote", "img.png");
        await File.WriteAllTextAsync(managed, "png");
        string external = Path.Combine(_root, "outside.txt");
        await File.WriteAllTextAsync(external, "ext");

        var item = new QuickCaptureItem
        {
            Id = "pathnote",
            Attachments =
            [
                new TodoAttachment { FilePath = managed, StorageMode = TodoAttachment.ManagedStorageMode },
                new TodoAttachment { FilePath = external, StorageMode = TodoAttachment.LinkedStorageMode }
            ]
        };

        var store = new NoteFileStore(quickCapture);
        await store.SaveAsync(item);
        string file = await File.ReadAllTextAsync(store.GetNoteFilePath("pathnote"));
        // Raw file text: the relative reference appears backslash-escaped in
        // the JSON header; the managed absolute path never does.
        Assert.Contains("..\\\\attachments", file);
        Assert.DoesNotContain(managed.Replace("\\", "\\\\"), file);

        NoteFileLoadResult loaded = await store.LoadAsync("pathnote");
        Assert.Equal(
            Path.GetFullPath(managed),
            Path.GetFullPath(loaded.Item!.Attachments[0].FilePath),
            StringComparer.OrdinalIgnoreCase);
        Assert.Equal(
            Path.GetFullPath(external),
            Path.GetFullPath(loaded.Item.Attachments[1].FilePath),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TombstoneFile_KeepsOnlyMinimalRecord()
    {
        var store = new NoteFileStore(Path.Combine(_root, "quick-capture"));
        var item = new QuickCaptureItem
        {
            Id = "tombnote",
            Title = "将删除",
            Body = "秘密内容",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        await store.SaveAsync(item);

        item.Title = null;
        item.Body = string.Empty;
        item.Attachments.Clear();
        item.IsDeleted = true;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await store.SaveAsync(item);

        NoteFileLoadResult loaded = await store.LoadAsync("tombnote");
        Assert.True(loaded.Item!.IsDeleted);
        Assert.Empty(loaded.Item.Body);
        Assert.Null(loaded.Item.Title);
        Assert.Equal(item.CreatedAt, loaded.Item.CreatedAt);
        string file = await File.ReadAllTextAsync(store.GetNoteFilePath("tombnote"));
        Assert.DoesNotContain("秘密内容", file);
    }

    [Fact]
    public async Task Enumeration_ExcludesRecoveryArtifacts()
    {
        string notesDirectory = NotesDirectory;
        var store = new NoteFileStore(Path.Combine(_root, "quick-capture"));
        await store.SaveAsync(new QuickCaptureItem { Id = "aaa", Body = "1" });
        await File.WriteAllTextAsync(Path.Combine(notesDirectory, "zzz.md.bak"), "bak");
        await File.WriteAllTextAsync(Path.Combine(notesDirectory, "yyy.md.abc.tmp"), "tmp");
        await File.WriteAllTextAsync(Path.Combine(notesDirectory, "readme.txt"), "txt");

        IReadOnlyList<NoteFileLoadResult> all = await store.LoadAllAsync();

        Assert.Single(all);
        Assert.Equal("aaa", all[0].NoteId);
    }

    [Fact]
    public async Task CorruptHeader_IsAnErrorNotAWipe()
    {
        string notesDirectory = NotesDirectory;
        var store = new NoteFileStore(Path.Combine(_root, "quick-capture"));
        await store.SaveAsync(new QuickCaptureItem { Id = "goodnote", Body = "好便签" });
        await File.WriteAllTextAsync(Path.Combine(notesDirectory, "badnote.md"), "<!-- winsticker-note\n{broken json\n-->\n正文");

        IReadOnlyList<NoteFileLoadResult> all = await store.LoadAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal(NoteFileLoadStatus.CorruptHeader, all[0].Status);
        Assert.Equal(NoteFileLoadStatus.Loaded, all[1].Status);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { }
    }
}
