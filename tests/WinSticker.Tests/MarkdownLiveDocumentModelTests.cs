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
        Assert.Equal("``` \ncode\n\nstill code\n```", model.Blocks[2]);
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
    public void Load_PreservesLeadingAndTrailingBlankSeparators()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("\n\n内容\n\n");
        var block = Assert.Single(model.Blocks);
        Assert.Equal("内容", block);
        Assert.Equal("\n\n内容\n\n", model.Serialize());
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
    public void UpdateActiveSource_DefersSplittingUntilCommit()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("一段");
        model.SetActive(0);
        int newIndex = model.UpdateActiveSource("前半\n\n后半");
        Assert.Equal(0, newIndex);
        Assert.Single(model.Blocks);
        Assert.Equal("前半\n\n后半", model.Serialize());
        model.Deactivate();
        Assert.Equal("前半", model.Blocks[0]);
        Assert.Equal("后半", model.Blocks[1]);
        Assert.Equal(-1, model.ActiveIndex);
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

    [Fact]
    public void TypingAfterBlankLine_DoesNotDuplicateEarlierText()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("开头\n\n结尾");
        model.SetActive(0);
        foreach (string source in new[] { "开头\n", "开头\n\n", "开头\n\n新", "开头\n\n新段" })
        {
            model.UpdateActiveSource(source);
            Assert.Equal(source + "\n\n结尾", model.Serialize());
            Assert.Equal(0, model.ActiveIndex);
        }

        model.SetActive(1);
        Assert.Equal(2, model.ActiveIndex);
        Assert.Equal("开头\n\n新段\n\n结尾", model.Serialize());
    }

    [Theory]
    [InlineData("\r\n\r\n# 标题\r\n\r\n文字  \r\n换行\r\n\r\n\r\n")]
    [InlineData("~~~cs\ncode\n\nmore\n~~~\n\n结尾\n")]
    [InlineData("````md\n```\n\ncode\n```\n````\n")]
    [InlineData("- 第一项\n\n  延续\n\n  - 嵌套\n\n- 第二项\n")]
    [InlineData("\t \n \n")]
    public void LoadAndCommit_PreserveEverySourceCharacter(string source)
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load(source);
        Assert.Equal(source, model.Serialize());
        int count = model.Blocks.Count;
        for (int index = 0; index < count; index++)
        {
            model.SetActive(index);
            model.Deactivate();
            Assert.Equal(source, model.Serialize());
        }
    }

    [Fact]
    public void Commit_PreservesEditedTrailingBlankLinesAndHardBreaks()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("甲\n\n乙");
        model.SetActive(0);
        model.UpdateActiveSource("甲  \n换行\n\n\n");
        string expected = model.Serialize();
        model.Deactivate();
        Assert.Equal(expected, model.Serialize());
        model.SetActive(model.Blocks.Count - 1);
        model.UpdateActiveSource("乙改");
        Assert.Equal("甲  \n换行\n\n\n\n\n乙改", model.Serialize());
    }

    [Fact]
    public void Load_AdjacentHeadingsAreSeparateSyntaxFragments()
    {
        var model = new MarkdownLiveDocumentModel();
        model.Load("# 一级\n## 二级\n正文\n");
        Assert.Equal(3, model.Blocks.Count);
        Assert.Equal("# 一级\n## 二级\n正文\n", model.Serialize());
    }

    [Fact]
    public void Load_BeyondRenderLimitDoesNotDiscardTail()
    {
        string source = "# 标题\n\n" + new string('a', MarkdownDocumentService.MaxCharacters) + "\n\n尾部";
        var model = new MarkdownLiveDocumentModel();
        model.Load(source);
        Assert.Equal(source, model.Serialize());
    }
}
