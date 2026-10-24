using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class MarkdownLiveDocumentModelTests
{
    [Fact]
    public void Load_SplitsOnBlankLinesOutsideCodeFences()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("# 标题\n\n第一段\n继续行\n\n``` \ncode\n\nstill code\n```\n\n尾段");
        Assert.Equal(4, model.Blocks.Count);
        Assert.Equal("# 标题", model.Blocks[0]);
        Assert.Equal("第一段\n继续行", model.Blocks[1]);
        Assert.Equal("```\ncode\n\nstill code\n```", model.Blocks[2]);
        Assert.Equal("尾段", model.Blocks[3]);
    }

    [Fact]
    public void Serialize_RoundTripsThroughLoad()
    {
        string source = "# 标题\n\n第一段\n\n- 甲\n- 乙\n\n尾段";
        var model = new MarkdownLiveDocumentModel();
        model.Load(source);
        Assert.Equal(source, model.Serialize());
    }

    [Fact]
    public void Load_EmptyDocumentYieldsSingleEmptyBlock()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load(string.Empty);
        var block = Assert.Single(model.Blocks);
        Assert.Equal(string.Empty, block);
    }

    [Fact]
    public void Load_TrimsLeadingAndTrailingBlankSeparators()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("\n\n内容\n\n");
        var block = Assert.Single(model.Blocks);
        Assert.Equal("内容", block);
    }

    [Fact]
    public void UpdateActiveSource_ReplacesBlockAndSerializes()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("甲\n\n乙");
        model.SetActive(1);
        model.UpdateActiveSource("乙改");
        Assert.Equal("甲\n\n乙改", model.Serialize());
        Assert.Equal("乙改", model.Blocks[1]);
    }

    [Fact]
    public void UpdateActiveSource_CanSplitBlockOnBlankLine()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("一段");
        model.SetActive(0);
        int newIndex = model.UpdateActiveSource("前半\n\n后半");
        Assert.Equal(1, newIndex);
        Assert.Equal("前半", model.Blocks[0]);
        Assert.Equal("后半", model.Blocks[1]);
        Assert.Equal(1, model.ActiveIndex);
    }

    [Fact]
    public void UpdateActiveSource_EmptySourceKeepsSingleEmptyBlock()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("甲\n\n乙");
        model.SetActive(0);
        int newIndex = model.UpdateActiveSource(string.Empty);
        Assert.Equal(0, newIndex);
        Assert.Equal(string.Empty, model.Blocks[0]);
        Assert.Equal("乙", model.Blocks[1]);
    }

    [Fact]
    public void SetActive_DeactivatesPrevious()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("甲\n\n乙");
        model.SetActive(0);
        Assert.Equal(0, model.ActiveIndex);
        model.SetActive(1);
        Assert.Equal(1, model.ActiveIndex);
        model.Deactivate();
        Assert.Equal(-1, model.ActiveIndex);
    }

    [Fact]
    public void TryToggleTask_TogglesWithinBlock()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("- [ ] 买牛奶\n- [x] 买菜");
        Assert.True(model.TryToggleTask(0, 0));
        Assert.Equal("- [x] 买牛奶\n- [x] 买菜", model.Blocks[0]);
        Assert.True(model.TryToggleTask(0, 1));
        Assert.Equal("- [x] 买牛奶\n- [ ] 买菜", model.Blocks[0]);
    }

    [Fact]
    public void TryToggleTask_ReturnsFalseWhenBlockHasNoTasks()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("普通文本\n\n- [ ] 任务");
        Assert.False(model.TryToggleTask(0, 0));
    }

    [Fact]
    public void DocumentChanged_RaisedOnMutationOnly()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("甲");
        int raised = 0;
        model.DocumentChanged += (_, _) => raised++;
        model.SetActive(0);
        Assert.Equal(0, raised);
        model.UpdateActiveSource("甲乙");
        Assert.Equal(1, raised);
        Assert.True(model.TryToggleTask(0, 0) is false);
        Assert.Equal(1, raised);
    }
}
