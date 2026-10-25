using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class NoteWorkspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinSticker.Tests", Guid.NewGuid().ToString("N"));
    private QuickCaptureStore Store => new(_root);
    private QuickCaptureService Service => new(Store);

    [Fact]
    public async Task BlankNotesSurviveReloadWithoutInventedTitle()
    {
        var service = Service;
        var created = await service.CreateNoteAsync();
        var loaded = Assert.Single((await Service.GetDataAsync()).Items);
        Assert.Equal(created.Id, loaded.Id);
        Assert.True(loaded.IsNote);
        Assert.Null(loaded.Title);
        Assert.Empty(loaded.Body);
        Assert.Equal(TextContentFormat.Markdown, loaded.ContentFormat);
    }

    [Fact]
    public async Task HostsShareDraftAndFlushPreviousOwnerOnFocusTransfer()
    {
        var service = Service;
        var item = await service.CreateNoteAsync();
        var workspace = NoteWorkspace.For(service);
        var first = workspace.Acquire(item, () => new HeadlessMarkdownSessionTimer());
        var second = workspace.Acquire(item, () => new HeadlessMarkdownSessionTimer());
        Assert.Same(first, second);
        var owner = new object();
        first.Activate(owner, () => first.SetBody("尚未防抖提交的中文输入"));
        second.Activate(new object(), () => { });
        Assert.Equal("尚未防抖提交的中文输入", second.Body);
        second.SetTitle("共享标题");
        second.SetAppearance(QuickCaptureAppearancePreset.Mint);
        Assert.True(await first.FlushAsync());
        var saved = Assert.Single((await Service.GetDataAsync()).Items);
        Assert.Equal(second.Body, saved.Body);
        Assert.Equal("共享标题", saved.Title);
        Assert.Equal(QuickCaptureAppearancePreset.Mint, saved.AppearancePreset);
        workspace.Release(first);
        workspace.Release(second);
    }

    [Fact]
    public async Task MetadataPatchPreservesBodyWrittenByAnotherEntry()
    {
        var service = Service;
        var note = await service.CreateNoteAsync();
        await service.PatchNoteAsync(note.Id, body: "latest body");
        await service.PatchNoteAsync(note.Id, title: "new title", appearancePreset: QuickCaptureAppearancePreset.Rose);
        var saved = Assert.Single((await Service.GetDataAsync()).Items);
        Assert.Equal("latest body", saved.Body);
        Assert.Equal("new title", saved.Title);
        Assert.Equal(QuickCaptureAppearancePreset.Rose, saved.AppearancePreset);
    }

    [Fact]
    public async Task InputArrivingDuringSaveIsPersistedBeforeFlushReturns()
    {
        var service = Service;
        var note = await service.CreateNoteAsync();
        var workspace = NoteWorkspace.For(service);
        var session = workspace.Acquire(note, () => new HeadlessMarkdownSessionTimer());
        session.SetBody("first");
        bool injected = false;
        service.Changed += () => { if (!injected) { injected = true; session.SetBody("typed during save"); } };
        Assert.True(await session.FlushAsync());
        Assert.False(session.HasUnsavedChanges);
        Assert.Equal("typed during save", Assert.Single((await Service.GetDataAsync()).Items).Body);
        workspace.Release(session);
    }

    [Fact]
    public async Task FailedSaveRetainsDraftAndRetryWritesIt()
    {
        var store = Store;
        var service = new QuickCaptureService(store);
        var note = await service.CreateNoteAsync();
        var workspace = NoteWorkspace.For(service);
        var session = workspace.Acquire(note, () => new HeadlessMarkdownSessionTimer());
        session.SetBody("must survive failed storage");
        string original = store.StorePath + ".preserved";
        File.Move(store.StorePath, original);
        Directory.CreateDirectory(store.StorePath);
        try
        {
            Assert.False(await session.FlushAsync());
            Assert.True(session.SaveFailed);
            Assert.True(session.HasUnsavedChanges);
            Assert.Equal("must survive failed storage", session.Body);
            workspace.Release(session);
            Assert.Same(session, workspace.Acquire(note, () => new HeadlessMarkdownSessionTimer()));
        }
        finally { Directory.Delete(store.StorePath); File.Move(original, store.StorePath); }
        Assert.True(await session.FlushAsync());
        Assert.False(session.SaveFailed);
        Assert.Equal(session.Body, Assert.Single((await Service.GetDataAsync()).Items).Body);
        workspace.Release(session);
    }

    [Fact]
    public async Task CloseIsHostStateAndDeleteCanBeUndoneWithAttachments()
    {
        var service = Service;
        var note = await service.CreateNoteAsync();
        await service.PatchNoteAsync(note.Id, body: "retained");
        note = Assert.Single((await service.GetDataAsync()).Items);
        Assert.True(await service.SetItemClosedAsync(note.Id, true));
        var closed = await Service.GetDataAsync();
        Assert.Equal(note.UpdatedAt, Assert.Single(closed.Items).UpdatedAt);
        Assert.Null(closed.Items[0].ArchivedAt);
        Assert.Contains(note.Id, closed.ClosedQuickPanelNotes!.Keys);
        var workspace = NoteWorkspace.For(service);
        var session = workspace.Acquire(note, () => new HeadlessMarkdownSessionTimer());
        Assert.True(await workspace.DeleteAsync(session));
        Assert.True(Assert.Single((await Service.GetDataAsync()).Items).IsDeleted);
        Assert.True(await workspace.UndoDeleteAsync());
        var restored = Assert.Single((await Service.GetDataAsync()).Items);
        Assert.False(restored.IsDeleted);
        Assert.Equal("retained", restored.Body);
        workspace.Release(session);
    }

    [Fact]
    public async Task LegacyCloseHistoryMigratesWithoutChangingDocument()
    {
        var store = Store;
        const string stamp = "2026-10-24T10:00:00+00:00";
        await File.WriteAllTextAsync(store.StorePath, $$"""
            {"version":4,"items":[{"id":"old-note","body":"保留正文","title":"保留标题","archivedAt":"{{stamp}}","updatedAt":"{{stamp}}","createdAt":"{{stamp}}"}],"recentItems":[]}
            """);
        var migrated = await store.LoadAsync();
        var note = Assert.Single(migrated.Items);
        Assert.Equal("保留正文", note.Body);
        Assert.Equal("保留标题", note.Title);
        Assert.Null(note.ArchivedAt);
        Assert.Equal(DateTimeOffset.Parse(stamp), migrated.ClosedQuickPanelNotes![note.Id]);
        Assert.Equal(DateTimeOffset.Parse(stamp), note.UpdatedAt);
        Assert.True(NoteWorkspace.IsNote(note));
    }

    [Fact]
    public async Task TaskExtractionSkipsCheckedAndDeduplicatesRepeatedImport()
    {
        var candidates = NoteTodoExtraction.Candidates("- [ ] 中文任务\n- [x] 已完成\n- [ ] 中文任务\n2. 第二项");
        Assert.Equal(new[] { "中文任务", "第二项" }, candidates);
        var repository = TodoRepository.ForStore(new TodoWidgetStore(_root, "tasks"));
        Assert.Equal(2, await NoteTodoExtraction.AddAsync(repository, candidates));
        Assert.Equal(0, await NoteTodoExtraction.AddAsync(repository, candidates));
        Assert.Equal(2, (await repository.LoadAsync()).Items.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
