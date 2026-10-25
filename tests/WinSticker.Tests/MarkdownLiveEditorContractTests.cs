namespace WinSticker.Tests;

public sealed class MarkdownLiveEditorContractTests
{
    private static string ReadEditor() => File.ReadAllText(TestPaths.FromRepository(
        "src/WinSticker/Controls/MarkdownLiveEditor.cs"));

    [Fact]
    public void Editor_UsesDebouncedCaptureAndSourceOnlyHistory()
    {
        string code = ReadEditor();

        Assert.Contains("MarkdownSerializationScheduler", code, StringComparison.Ordinal);
        Assert.Contains("MarkdownNativeBuffer", code, StringComparison.Ordinal);
        Assert.Contains("Document.UndoLimit = 0", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_UsesOneContinuousNativeEditingSurface()
    {
        string code = ReadEditor();

        Assert.Contains("RichEditBox", code, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsRepeater", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_ChangesPresentationWithoutReplacingSource()
    {
        string code = ReadEditor();

        Assert.Contains("CharacterFormat.Hidden", code, StringComparison.Ordinal);
        Assert.Contains("BatchDisplayUpdates", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_DoesNotSerializeOnTheKeystrokePath()
    {
        string code = ReadEditor();

        int start = code.IndexOf("private void EditorTextChanging", StringComparison.Ordinal);
        int end = code.IndexOf("private string CaptureSource", start, StringComparison.Ordinal);
        string keystrokePath = code[start..end];
        Assert.DoesNotContain("GetText(", keystrokePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Parse(", keystrokePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Text = _model.Serialize()", code, StringComparison.Ordinal);
        Assert.DoesNotContain("RebuildBlocks", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_LeavesContinuousArrowSelectionToNativeControl()
    {
        string code = ReadEditor();

        Assert.DoesNotContain("CommitAndNavigateToBlock", code, StringComparison.Ordinal);
        Assert.DoesNotContain("VirtualKey.Up", code, StringComparison.Ordinal);
        Assert.DoesNotContain("VirtualKey.Down", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_ExposesFlushAndGetTextForAutosave()
    {
        string code = ReadEditor();

        Assert.Contains("public string FlushAndGetText()", code, StringComparison.Ordinal);
    }

    [Fact]
    public void TodoDetailNotes_AutosaveReadsFlushedText()
    {
        string code = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoWidgetContent.DetailNotesAndSteps.cs"));

        Assert.Contains("DetailNotesEditor.FlushAndGetText()", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DetailNotesEditor.Text)", code, StringComparison.Ordinal);
    }
}
