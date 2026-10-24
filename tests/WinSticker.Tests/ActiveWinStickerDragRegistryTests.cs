using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class ActiveWinStickerDragRegistryTests : IDisposable
{
    private readonly string _root;

    public ActiveWinStickerDragRegistryTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "winsticker-active-drag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void TryMatch_RecognizesAllPathsFromAnActiveDrag()
    {
        string first = Path.Combine(_root, "a.txt");
        string second = Path.Combine(_root, "b.txt");
        DateTime now = DateTime.UtcNow;
        ActiveWinStickerDragRegistry.Begin(
            "s1",
            "widget-1",
            [first, second],
            fromStackPopover: false,
            utcNow: now);
        try
        {
            Assert.True(ActiveWinStickerDragRegistry.TryMatch(
                [first, second],
                out string widgetId,
                out bool fromPopover,
                utcNow: now));
            Assert.Equal("widget-1", widgetId);
            Assert.False(fromPopover);
            // A subset of the dragged paths still identifies the same drag.
            Assert.True(ActiveWinStickerDragRegistry.TryMatch(
                [second],
                out _,
                out _,
                utcNow: now));
        }
        finally
        {
            ActiveWinStickerDragRegistry.End("s1");
        }
    }

    [Fact]
    public void TryMatch_RejectsForeignAndMixedPayloads()
    {
        string ours = Path.Combine(_root, "ours.txt");
        string theirs = Path.Combine(_root, "theirs.txt");
        DateTime now = DateTime.UtcNow;
        ActiveWinStickerDragRegistry.Begin(
            "s2",
            "widget-2",
            [ours],
            fromStackPopover: true,
            utcNow: now);
        try
        {
            // A foreign path is not a self-drag.
            Assert.False(ActiveWinStickerDragRegistry.TryMatch(
                [theirs],
                out _,
                out _,
                utcNow: now));
            // A mixed payload (ours + theirs) cannot be proven self-originated.
            Assert.False(ActiveWinStickerDragRegistry.TryMatch(
                [ours, theirs],
                out _,
                out _,
                utcNow: now));
            // Popover provenance survives the match.
            Assert.True(ActiveWinStickerDragRegistry.TryMatch(
                [ours],
                out _,
                out bool fromPopover,
                utcNow: now));
            Assert.True(fromPopover);
        }
        finally
        {
            ActiveWinStickerDragRegistry.End("s2");
        }
    }

    [Fact]
    public void TryMatch_DoesNotMatchAfterEnd()
    {
        string path = Path.Combine(_root, "ended.txt");
        ActiveWinStickerDragRegistry.Begin(
            "s3",
            "widget-3",
            [path],
            fromStackPopover: false);
        ActiveWinStickerDragRegistry.End("s3");
        Assert.False(ActiveWinStickerDragRegistry.TryMatch(
            [path],
            out _,
            out _));
    }

    [Fact]
    public void TryMatch_ExpiresStaleEntries()
    {
        string path = Path.Combine(_root, "stale.txt");
        DateTime registered = DateTime.UtcNow - TimeSpan.FromMinutes(20);
        DateTime now = DateTime.UtcNow;
        // Begin prunes on write: register via a backdated clock.
        ActiveWinStickerDragRegistry.Begin(
            "s4",
            "widget-4",
            [path],
            fromStackPopover: false,
            utcNow: registered);
        try
        {
            Assert.False(ActiveWinStickerDragRegistry.TryMatch(
                [path],
                out _,
                out _,
                utcNow: now));
        }
        finally
        {
            ActiveWinStickerDragRegistry.End("s4");
        }
    }

    [Fact]
    public void TryMatch_NormalizesEquivalentPathForms()
    {
        string path = Path.Combine(_root, "case.TXT");
        DateTime now = DateTime.UtcNow;
        ActiveWinStickerDragRegistry.Begin(
            "s5",
            "widget-5",
            [path],
            fromStackPopover: false,
            utcNow: now);
        try
        {
            Assert.True(ActiveWinStickerDragRegistry.TryMatch(
                [path.ToUpperInvariant()],
                out _,
                out _,
                utcNow: now));
            Assert.True(ActiveWinStickerDragRegistry.TryMatch(
                [Path.Combine(_root, ".", "case.TXT")],
                out _,
                out _,
                utcNow: now));
        }
        finally
        {
            ActiveWinStickerDragRegistry.End("s5");
        }
    }

    [Fact]
    public void Begin_RetiresEntriesFromPriorLostDrags()
    {
        // A modal OLE drag cannot coexist with a new one: starting a drag
        // must retire registrations whose DropCompleted was lost, or the
        // same file set dropped back within the entry lifetime would be
        // silently swallowed as a self-drag.
        string lostPath = Path.Combine(_root, "dropped-back.txt");
        string nextPath = Path.Combine(_root, "next-drag.txt");
        ActiveWinStickerDragRegistry.Begin(
            "s-lost",
            "widget-6",
            [lostPath],
            fromStackPopover: false);
        // A later WinSticker drag of different files begins and completes
        // normally; the lost registration must not survive it.
        ActiveWinStickerDragRegistry.Begin(
            "s-new",
            "widget-6",
            [nextPath],
            fromStackPopover: false);
        ActiveWinStickerDragRegistry.End("s-new");

        Assert.False(ActiveWinStickerDragRegistry.TryMatch(
            [lostPath],
            out _,
            out _));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
