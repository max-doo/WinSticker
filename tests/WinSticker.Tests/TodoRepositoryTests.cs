using WinSticker.Models;
using WinSticker.Services;
using Xunit;

namespace WinSticker.Tests;

public sealed class TodoRepositoryTests
{
    private static string NewTestDir() =>
        Path.Combine(Path.GetTempPath(), "winsticker-test-todorepo-" + Guid.NewGuid().ToString("N"));

    private static void Cleanup(string testDir)
    {
        if (Directory.Exists(testDir))
        {
            try { Directory.Delete(testDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ForStore_ReturnsSameInstancePerStorePath()
    {
        string testDir = NewTestDir();
        try
        {
            Directory.CreateDirectory(testDir);
            var storeA = new TodoWidgetStore(testDir, "widget-a");
            var storeB = new TodoWidgetStore(testDir, "widget-b");

            Assert.Same(TodoRepository.ForStore(storeA), TodoRepository.ForStore(new TodoWidgetStore(testDir, "widget-a")));
            Assert.NotSame(TodoRepository.ForStore(storeA), TodoRepository.ForStore(storeB));
        }
        finally
        {
            Cleanup(testDir);
        }
    }

    [Fact]
    public async Task MutateAsync_AppliesAgainstLatestDataAndPersists()
    {
        string testDir = NewTestDir();
        try
        {
            Directory.CreateDirectory(testDir);
            var repository = TodoRepository.ForStore(new TodoWidgetStore(testDir, "widget-mutate"));

            await repository.MutateAsync(data => data.Items.Add(new TodoItem { Text = "first" }));
            await repository.MutateAsync(data => data.Items.Add(new TodoItem { Text = "second" }));

            TodoWidgetData reloaded = await new TodoWidgetStore(testDir, "widget-mutate").LoadAsync();
            Assert.Equal(2, reloaded.Items.Count);
            Assert.Equal("first", reloaded.Items[1].Text);
            Assert.Equal("second", reloaded.Items[0].Text);
        }
        finally
        {
            Cleanup(testDir);
        }
    }

    [Fact]
    public async Task StoreChanged_RaisedForOtherOrigins_SkippedForSelf()
    {
        string testDir = NewTestDir();
        try
        {
            Directory.CreateDirectory(testDir);
            var repository = TodoRepository.ForStore(new TodoWidgetStore(testDir, "widget-changed"));
            var writer = new object();

            object? lastOrigin = null;
            int raised = 0;
            repository.StoreChanged += origin =>
            {
                lastOrigin = origin;
                raised++;
            };

            await repository.MutateAsync(data => { }, writer);
            Assert.Equal(1, raised);
            Assert.Same(writer, lastOrigin);

            await repository.MutateAsync(data => { }, null);
            Assert.Equal(2, raised);
            Assert.Null(lastOrigin);
        }
        finally
        {
            Cleanup(testDir);
        }
    }

    [Fact]
    public async Task MergeAsync_KeepsExternalAddsAndAppliesSnapshotState()
    {
        string testDir = NewTestDir();
        try
        {
            Directory.CreateDirectory(testDir);
            var widget = new object();
            var repository = TodoRepository.ForStore(new TodoWidgetStore(testDir, "widget-merge"));

            // Widget snapshot holds one live item plus a tombstone for an
            // item it deleted.
            var shared = new TodoItem { Text = "report", SortOrder = 0 };
            var deleted = new TodoItem { Text = "gone", SortOrder = 1, IsDeleted = true };
            var snapshot = new TodoWidgetData { Items = [shared, deleted] };
            await repository.MergeAsync(snapshot, widget);

            // Another surface adds an item the widget has never seen.
            await repository.MutateAsync(data =>
                data.Items.Add(new TodoItem { Text = "external add", SortOrder = 5 }), new object());

            // The widget saves again from its stale snapshot (shared item now
            // completed) — the external add must survive the merge.
            shared.IsCompleted = true;
            shared.CompletedAt = DateTimeOffset.UtcNow;
            await repository.MergeAsync(snapshot, widget);

            TodoWidgetData reloaded = await new TodoWidgetStore(testDir, "widget-merge").LoadAsync();
            Assert.True(reloaded.Items.Single(item => item.Text == "report").IsCompleted);
            Assert.Contains(reloaded.Items, item => item.Text == "external add");
            Assert.Contains(reloaded.Items, item => item.Text == "gone" && item.IsDeleted);
        }
        finally
        {
            Cleanup(testDir);
        }
    }

    [Fact]
    public async Task TryRemove_DropsCacheSoDeletedStoreStaysDeleted()
    {
        string testDir = NewTestDir();
        try
        {
            Directory.CreateDirectory(testDir);
            var store = new TodoWidgetStore(testDir, "widget-delete");
            var repository = TodoRepository.ForStore(store);
            await repository.MutateAsync(data => data.Items.Add(new TodoItem { Text = "doomed" }));

            await TodoWidgetStore.DeleteForWidgetAsync(testDir, "widget-delete");

            // A repository for the same path must come back empty, not from
            // the pre-delete cache.
            var freshRepository = TodoRepository.ForStore(new TodoWidgetStore(testDir, "widget-delete"));
            TodoWidgetData data = await freshRepository.LoadAsync();
            Assert.Empty(data.Items);
        }
        finally
        {
            Cleanup(testDir);
        }
    }
}
