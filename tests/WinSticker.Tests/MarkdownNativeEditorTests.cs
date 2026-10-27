using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class MarkdownNativeBufferTests
{
    [Theory]
    [InlineData("")]
    [InlineData("\r\n# 标题\r\n\r\n硬换行  \r\n末尾\n\n")]
    [InlineData("emoji 😀\n\n~~~\n code\n~~~\n")]
    public void LoadingAndUnchangedCapture_PreserveEverySourceCharacter(string source)
    {
        var buffer = new MarkdownNativeBuffer();
        buffer.Load(source);
        Assert.False(buffer.Capture(MarkdownNativeBuffer.ToNative(source), 0, 0));
        Assert.Equal(source, buffer.Source);
        Assert.False(buffer.CanUndo);
    }

    [Fact]
    public void CaptureAndUndo_PreserveMixedOriginalNewlinesAndSelection()
    {
        const string source = "第一行\r\n第二行  \n\n末尾\r\n";
        var buffer = new MarkdownNativeBuffer();
        buffer.Load(source);
        string native = buffer.NativeText;
        Assert.True(buffer.Capture(native.Remove(4, 6).Insert(4, "新\r段"), 4, 7, 6));
        Assert.Contains("新\r\n段", buffer.Source);
        MarkdownNativeEdit edit = Assert.IsType<MarkdownNativeEdit>(buffer.Undo());
        Assert.Equal(source, buffer.Source);
        Assert.Equal(native, buffer.NativeText);
        Assert.Equal(4, edit.Caret);
        Assert.Equal(6, edit.SelectionLength);
        Assert.NotNull(buffer.Redo());
        Assert.Contains("新\r\n段", buffer.Source);
    }

    [Fact]
    public void ManySourceChanges_RoundTripAcrossUndoAndRedo()
    {
        var buffer = new MarkdownNativeBuffer();
        const string source = "# 初始\r\n\r\n正文  \n";
        buffer.Load(source);
        var snapshots = new List<string> { source };
        for (int i = 0; i < 40; i++)
        {
            string native = buffer.NativeText;
            int start = (i * 7) % (native.Length + 1);
            buffer.Capture(native.Insert(start, i % 3 == 0 ? "😀\r" : "文字"), start, start + 2);
            snapshots.Add(buffer.Source);
        }
        for (int i = 39; i >= 0; i--)
        {
            Assert.NotNull(buffer.Undo());
            Assert.Equal(snapshots[i], buffer.Source);
            Assert.Equal(MarkdownNativeBuffer.ToNative(buffer.Source), buffer.NativeText);
        }
        Assert.False(buffer.CanUndo);
        for (int i = 1; i <= 40; i++)
        {
            Assert.NotNull(buffer.Redo());
            Assert.Equal(snapshots[i], buffer.Source);
        }
    }

    [Fact]
    public void NewEditAfterUndo_ClearsRedoAndLoadingClearsBothHistories()
    {
        var buffer = new MarkdownNativeBuffer();
        buffer.Load("a");
        buffer.Capture("ab", 1, 2);
        buffer.Undo();
        Assert.True(buffer.CanRedo);
        buffer.Capture("ac", 1, 2);
        Assert.False(buffer.CanRedo);
        buffer.Load("other");
        Assert.False(buffer.CanUndo);
        Assert.False(buffer.CanRedo);
    }

    [Fact]
    public void UndoAndRedo_PreserveSelectionDirectionMetadata()
    {
        var buffer = new MarkdownNativeBuffer();
        buffer.Load("first\nsecond");
        buffer.Capture("first", 2, 5, 8, 0, 17, 2);
        Assert.Equal(17, buffer.Undo()!.SelectionOptions);
        Assert.Equal(2, buffer.Redo()!.SelectionOptions);
    }

    [Fact]
    public void HistoryIsBoundedAndSurrogateReplacementsAreAtomic()
    {
        var buffer = new MarkdownNativeBuffer();
        buffer.Load("😀");
        buffer.Capture("😃", 2, 2);
        Assert.Equal("😀", buffer.Undo()!.Replacement);
        Assert.Equal("😀", buffer.NativeText);
        for (int i = 0; i < 150; i++) buffer.Capture(i.ToString(), 0, 0);
        int count = 0;
        while (buffer.Undo() is not null) count++;
        Assert.Equal(128, count);
    }
}

public sealed class MarkdownNativeSyntaxTests
{
    [Theory]
    [InlineData("址：（https://ggfw.hrss.gd.gov.cn/employment/internet/portal/#/home）提", "https://ggfw.hrss.gd.gov.cn/employment/internet/portal/#/home")]
    [InlineData("https://docs.qq.com/form/page/DUFhveG5QUWR1TXRm，以上步骤请在11", "https://docs.qq.com/form/page/DUFhveG5QUWR1TXRm")]
    [InlineData("https://jyjzx.scut.edu.cn/2026/1027/c37785a638575/page.htm", "https://jyjzx.scut.edu.cn/2026/1027/c37785a638575/page.htm")]
    public void BareLinks_StopAtChinesePunctuation(string source, string expected)
    {
        var link = Assert.Single(MarkdownNativeSyntax.Parse(source).Links);
        Assert.Equal(expected, source.Substring(link.Start, link.Length));
        Assert.Equal(expected, link.Url);
    }

    [Fact]
    public void BareWebLinks_KeepSourceOffsetsAndIgnoreCode()
    {
        const string source = "Visit https://example.com/path.\r`https://code.example`\r<https://second.example>";
        var syntax = MarkdownNativeSyntax.Parse(source);
        Assert.Equal(2, syntax.Links.Count);
        Assert.Equal("https://example.com/path", source.Substring(syntax.Links[0].Start, syntax.Links[0].Length));
        Assert.Equal("https://second.example", source.Substring(syntax.Links[1].Start, syntax.Links[1].Length));
    }

    [Fact]
    public void HiddenRanges_PreserveHeadingAndInlineContent()
    {
        const string source = "# Title ##\r\r**body**\r\r> quote";
        var syntax = MarkdownNativeSyntax.Parse(source);
        bool[] hidden = new bool[source.Length];
        foreach (var span in syntax.Spans.Where(s => s.Style.HasFlag(MarkdownNativeStyle.Hidden)))
            Array.Fill(hidden, true, span.Start, span.Length);
        string shown = new(source.Where((_, index) => !hidden[index]).ToArray());
        Assert.Contains("Title", shown);
        Assert.Contains("body", shown);
        Assert.Contains("quote", shown);
        Assert.DoesNotContain('#', shown);
        Assert.DoesNotContain('*', shown);
        Assert.DoesNotContain('>', shown);
    }

    [Fact]
    public void HeadingsAndInlineStyles_HideOnlySyntaxAndKeepSemanticFormatting()
    {
        const string source = "# 标题\r\r正文 **strong** 与 *italic*、`code`。";
        var syntax = MarkdownNativeSyntax.Parse(source);
        Assert.Contains(syntax.Spans, s => s.HeadingLevel == 1 && s.Style.HasFlag(MarkdownNativeStyle.Bold));
        Assert.Contains(syntax.Spans, s => s.Style.HasFlag(MarkdownNativeStyle.Italic));
        Assert.Contains(syntax.Spans, s => s.Style.HasFlag(MarkdownNativeStyle.Code));
        Assert.Contains(syntax.Spans, s => s.Start == 0 && s.Length == 2 && s.Style == MarkdownNativeStyle.Hidden);
        Assert.All(syntax.Spans, span => Assert.InRange(span.Start + span.Length, 1, source.Length));
    }

    [Fact]
    public void CodeFences_DoNotInterpretMarkdownInsideTheirBody()
    {
        var syntax = MarkdownNativeSyntax.Parse("~~~md\r**literal**\r\r- [ ] literal\r~~~");
        Assert.Empty(syntax.Tasks);
        Assert.DoesNotContain(syntax.Spans, s => s.Style.HasFlag(MarkdownNativeStyle.Bold));
        Assert.Contains(syntax.Spans, s => s.Style.HasFlag(MarkdownNativeStyle.Code));
    }

    [Fact]
    public void UnclosedFence_DoesNotHideALiteralNonClosingFenceLine()
    {
        const string source = "~~~md\rbody\r~~~not-a-closing-fence";
        var syntax = MarkdownNativeSyntax.Parse(source);
        int literalStart = source.LastIndexOf('\r') + 1;
        Assert.DoesNotContain(syntax.Spans, span => span.Style.HasFlag(MarkdownNativeStyle.Hidden) && span.Start >= literalStart);
    }

    [Fact]
    public void TaskMetadata_UsesNativeSourceOffsetsAndCheckedState()
    {
        const string source = "- [ ] first\r- [x] second\r";
        var syntax = MarkdownNativeSyntax.Parse(source);
        Assert.Equal(2, syntax.Tasks.Count);
        Assert.False(syntax.Tasks[0].IsChecked);
        Assert.True(syntax.Tasks[1].IsChecked);
        Assert.All(syntax.Tasks, task => Assert.Equal('[', source[task.Start]));
        Assert.Contains(syntax.Spans, s => s.Style.HasFlag(MarkdownNativeStyle.Strike));
    }

    [Fact]
    public void ComplexBlocks_KeepExactNativeOffsetsWithoutEmbeddingTableCellImagesTwice()
    {
        const string source = "before\r\r| A | B |\r| --- | --- |\r| ![cell](a.png) | value |\r\r![standalone](b.png)\r\rafter";
        var syntax = MarkdownNativeSyntax.Parse(source);
        Assert.Equal(2, syntax.EmbeddedBlocks.Count);
        Assert.All(syntax.EmbeddedBlocks, block => Assert.Equal(
            source.Substring(block.Start, block.Length).Replace('\r', '\n'), block.Source));
        Assert.StartsWith("| A | B |", syntax.EmbeddedBlocks[0].Source);
        Assert.Equal("![standalone](b.png)", syntax.EmbeddedBlocks[1].Source);
    }
}

public sealed class MarkdownNativeContinuationTests
{
    [Theory]
    [InlineData("- one", "- one\r- ")]
    [InlineData("  - [x] done", "  - [x] done\r  - [ ] ")]
    [InlineData("9) item", "9) item\r10) ")]
    [InlineData("> quote", "> quote\r> ")]
    [InlineData("- ", "")]
    [InlineData("- [ ] ", "")]
    [InlineData("> ", "")]
    public void Enter_ContinuesOrExitsMarkdownStructure(string source, string expected)
    {
        Assert.True(MarkdownNativeContinuation.TryCreate(source, source.Length, out var edit));
        Assert.Equal(expected, edit.Apply(source));
    }

    [Theory]
    [InlineData("~~~\r- literal")]
    [InlineData("````\r```\r- literal")]
    [InlineData("```")]
    public void Enter_DoesNotContinueInsideFences(string source) =>
        Assert.False(MarkdownNativeContinuation.TryCreate(source, source.Length, out _));

    [Fact]
    public void EnterInMiddle_SplitsContentWithoutDuplicatingIt()
    {
        Assert.True(MarkdownNativeContinuation.TryCreate("- first second", 7, out var edit));
        Assert.Equal("- first\r-  second", edit.Apply("- first second"));
    }
}
