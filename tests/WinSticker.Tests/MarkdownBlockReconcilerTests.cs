using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class MarkdownBlockReconcilerTests
{
    [Fact]
    public void Plan_IdenticalLists_IsUnchanged()
    {
        var blocks = new[] { "# 标题", "第一段", "- 甲", "尾段" };
        var plan = MarkdownBlockReconciler.Plan(blocks, blocks);
        Assert.True(plan.Unchanged);
        Assert.Equal(4, plan.Reuses.Count);
        Assert.Equal(-1, plan.FirstChangedNewIndex);
        Assert.Equal(-1, plan.LastChangedNewIndex);
    }

    [Fact]
    public void Plan_EditingMiddleBlock_ReusesPrefixAndSuffix()
    {
        var oldBlocks = new[] { "甲", "乙", "丙", "丁" };
        var newBlocks = new[] { "甲", "乙改", "丙", "丁" };
        var plan = MarkdownBlockReconciler.Plan(oldBlocks, newBlocks);
        Assert.False(plan.Unchanged);
        Assert.Equal(3, plan.Reuses.Count);
        Assert.Equal(1, plan.FirstChangedNewIndex);
        Assert.Equal(1, plan.LastChangedNewIndex);
        Assert.Contains(new MarkdownBlockReuse(0, 0), plan.Reuses);
        Assert.Contains(new MarkdownBlockReuse(2, 2), plan.Reuses);
        Assert.Contains(new MarkdownBlockReuse(3, 3), plan.Reuses);
    }

    [Fact]
    public void Plan_SplittingBlock_MarksSplitRangeChanged()
    {
        var oldBlocks = new[] { "甲", "乙", "丙" };
        var newBlocks = new[] { "甲", "乙上半", "乙下半", "丙" };
        var plan = MarkdownBlockReconciler.Plan(oldBlocks, newBlocks);
        Assert.False(plan.Unchanged);
        Assert.Equal(1, plan.FirstChangedNewIndex);
        Assert.Equal(2, plan.LastChangedNewIndex);
        Assert.Contains(new MarkdownBlockReuse(0, 0), plan.Reuses);
        Assert.Contains(new MarkdownBlockReuse(2, 3), plan.Reuses);
    }

    [Fact]
    public void Plan_MergingBlocks_MarksMergedRangeChanged()
    {
        var oldBlocks = new[] { "甲", "乙上半", "乙下半", "丙" };
        var newBlocks = new[] { "甲", "乙", "丙" };
        var plan = MarkdownBlockReconciler.Plan(oldBlocks, newBlocks);
        Assert.False(plan.Unchanged);
        Assert.Equal(1, plan.FirstChangedNewIndex);
        Assert.Equal(1, plan.LastChangedNewIndex);
        Assert.Contains(new MarkdownBlockReuse(0, 0), plan.Reuses);
        Assert.Contains(new MarkdownBlockReuse(3, 2), plan.Reuses);
    }

    [Fact]
    public void Plan_DeletingMiddleBlock_HasNoChangedRegionButIsNotUnchanged()
    {
        var oldBlocks = new[] { "甲", "乙", "丙" };
        var newBlocks = new[] { "甲", "丙" };
        var plan = MarkdownBlockReconciler.Plan(oldBlocks, newBlocks);
        Assert.False(plan.Unchanged);
        Assert.Equal(-1, plan.FirstChangedNewIndex);
        Assert.Equal(-1, plan.LastChangedNewIndex);
        Assert.Contains(new MarkdownBlockReuse(0, 0), plan.Reuses);
        Assert.Contains(new MarkdownBlockReuse(2, 1), plan.Reuses);
    }

    [Fact]
    public void Plan_PrependingBlock_MarksOnlyHeadChanged()
    {
        var oldBlocks = new[] { "乙", "丙" };
        var newBlocks = new[] { "甲", "乙", "丙" };
        var plan = MarkdownBlockReconciler.Plan(oldBlocks, newBlocks);
        Assert.False(plan.Unchanged);
        Assert.Equal(0, plan.FirstChangedNewIndex);
        Assert.Equal(0, plan.LastChangedNewIndex);
        Assert.Contains(new MarkdownBlockReuse(0, 1), plan.Reuses);
        Assert.Contains(new MarkdownBlockReuse(1, 2), plan.Reuses);
    }

    [Fact]
    public void Plan_AppendingBlock_MarksOnlyTailChanged()
    {
        var oldBlocks = new[] { "甲", "乙" };
        var newBlocks = new[] { "甲", "乙", "丙" };
        var plan = MarkdownBlockReconciler.Plan(oldBlocks, newBlocks);
        Assert.False(plan.Unchanged);
        Assert.Equal(2, plan.FirstChangedNewIndex);
        Assert.Equal(2, plan.LastChangedNewIndex);
        Assert.Contains(new MarkdownBlockReuse(0, 0), plan.Reuses);
        Assert.Contains(new MarkdownBlockReuse(1, 1), plan.Reuses);
    }

    [Fact]
    public void Plan_EmptyOldList_MarksEverythingChanged()
    {
        var plan = MarkdownBlockReconciler.Plan(
            Array.Empty<string>(),
            new[] { "甲", "乙" });
        Assert.False(plan.Unchanged);
        Assert.Empty(plan.Reuses);
        Assert.Equal(0, plan.FirstChangedNewIndex);
        Assert.Equal(1, plan.LastChangedNewIndex);
    }

    [Fact]
    public void Plan_EmptyNewList_MarksNothingChangedAndNotUnchanged()
    {
        var plan = MarkdownBlockReconciler.Plan(
            new[] { "甲", "乙" },
            Array.Empty<string>());
        Assert.False(plan.Unchanged);
        Assert.Empty(plan.Reuses);
        Assert.Equal(-1, plan.FirstChangedNewIndex);
        Assert.Equal(-1, plan.LastChangedNewIndex);
    }

    [Fact]
    public void Plan_DuplicateContentBlocks_CompareByFullOrdinalString()
    {
        var blocks = new[] { "同文", "异文", "同文" };
        var plan = MarkdownBlockReconciler.Plan(blocks, blocks);
        Assert.True(plan.Unchanged);
    }

    [Fact]
    public void Plan_SuffixDoesNotOverlapPrefix()
    {
        // old=[A], new=[A,A]: the prefix already consumed old[0], so the
        // suffix must not match new[1] against it again.
        var plan = MarkdownBlockReconciler.Plan(
            new[] { "甲" },
            new[] { "甲", "甲" });
        Assert.False(plan.Unchanged);
        Assert.Equal(1, plan.Reuses.Count);
        Assert.Contains(new MarkdownBlockReuse(0, 0), plan.Reuses);
        Assert.Equal(1, plan.FirstChangedNewIndex);
        Assert.Equal(1, plan.LastChangedNewIndex);
    }

    [Fact]
    public void Plan_ReuseIndicesAreStrictlyMonotonic()
    {
        var oldBlocks = new[] { "甲", "乙", "丙", "丁", "戊", "己" };
        var newBlocks = new[] { "甲改", "乙", "丙", "丙丁合并", "戊", "己", "庚" };
        var plan = MarkdownBlockReconciler.Plan(oldBlocks, newBlocks);
        int lastOld = -1;
        int lastNew = -1;
        foreach (MarkdownBlockReuse reuse in plan.Reuses)
        {
            Assert.True(reuse.OldIndex > lastOld);
            Assert.True(reuse.NewIndex > lastNew);
            lastOld = reuse.OldIndex;
            lastNew = reuse.NewIndex;
            Assert.Equal(oldBlocks[reuse.OldIndex], newBlocks[reuse.NewIndex]);
        }
    }

    [Fact]
    public void Plan_ReusesOnlyReferenceOrdinalEqualSources()
    {
        var plan = MarkdownBlockReconciler.Plan(
            new[] { "case", "CASE" },
            new[] { "case", "CASE" });
        Assert.True(plan.Unchanged);
    }

    [Fact]
    public void Plan_BlockCountOverThreshold_FallsBackToFullReplace()
    {
        int count = MarkdownBlockReconciler.MaxBlocksForReconcile + 1;
        string block = "同块";
        var oldBlocks = new string[count];
        var newBlocks = new string[count];
        Array.Fill(oldBlocks, block);
        Array.Fill(newBlocks, block);
        newBlocks[count - 1] = "末块改";
        var plan = MarkdownBlockReconciler.Plan(oldBlocks, newBlocks);
        Assert.False(plan.Unchanged);
        Assert.Empty(plan.Reuses);
        Assert.Equal(0, plan.FirstChangedNewIndex);
        Assert.Equal(count - 1, plan.LastChangedNewIndex);
    }
}
