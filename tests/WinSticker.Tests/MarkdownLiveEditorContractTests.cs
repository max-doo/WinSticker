namespace WinSticker.Tests;

public sealed class MarkdownLiveEditorContractTests
{
    private static string ReadEditor() => File.ReadAllText(TestPaths.FromRepository(
        "src/WinSticker/Controls/MarkdownLiveEditor.cs"));

    [Fact]
    public void Editor_UsesDebouncedSerializationAndBlockReconciliation()
    {
        string code = ReadEditor();

        Assert.Contains("MarkdownSerializationScheduler", code, StringComparison.Ordinal);
        Assert.Contains("MarkdownBlockReconciler.Plan", code, StringComparison.Ordinal);
        Assert.Contains("_renderedViews", code, StringComparison.Ordinal);
        Assert.Contains("_committedBlocks", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_DoesNotSerializeOnTheKeystrokePath()
    {
        string code = ReadEditor();

        // The keystroke path is Model_DocumentChanged -> scheduler.NotifyChanged;
        // serializing or assigning Text there would reintroduce the WorkCat
        // per-keystroke O(document) rebuild.
        Assert.DoesNotContain("Text = _model.Serialize()", code, StringComparison.Ordinal);
        Assert.DoesNotContain("RebuildBlocks", code, StringComparison.Ordinal);
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
