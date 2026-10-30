namespace WinSticker.Tests;

public sealed class TodoDetailHeaderLayoutContractTests
{
    [Fact]
    public void DetailActions_AreDirectlyAccessibleAndTitleUsesFullWidth()
    {
        string xaml = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoWidgetContent.xaml"));
        string code = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoWidgetContent.xaml.cs"));
        string masterDetailCode = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoWidgetContent.MasterDetail.cs"));

        Assert.Contains("x:Name=\"DetailHeaderActions\"", xaml, StringComparison.Ordinal);
        Assert.Contains(
            "x:Name=\"DetailHeaderActions\"\n                            Grid.Column=\"2\"\n                            HorizontalAlignment=\"Right\"",
            xaml.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"DetailCompletionCheckBox\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"DetailCompletionButton\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DetailCompletionButton_Click", code, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DetailSaveButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"DetailSaveButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{Binding CanSaveEdit}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DetailImportantButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DetailDeleteButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"DetailDeleteButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DetailMoreButton", xaml, StringComparison.Ordinal);
        Assert.Contains("DetailDeleteMenuItem", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DetailDeleteMenuItem_Click", code, StringComparison.Ordinal);
        Assert.Contains("ViewModel.FinalizeDetailAsync(", code, StringComparison.Ordinal);
        Assert.Contains("ShowTodoStatus(\"Todo.Status.Saved\")", code, StringComparison.Ordinal);
        Assert.True(xaml.IndexOf("x:Name=\"DetailDraftActions\"", StringComparison.Ordinal) <
            xaml.IndexOf("x:Name=\"DetailSaveButton\"", StringComparison.Ordinal));
        Assert.Contains("DetailDraftActions.Visibility = creating", masterDetailCode, StringComparison.Ordinal);
        Assert.Contains(
            "if (e.PropertyName == nameof(TodoWidgetViewModel.SelectedDetailItem))",
            code,
            StringComparison.Ordinal);
        Assert.Contains("ApplyDetailSaveButtonVisibility();", code, StringComparison.Ordinal);
        Assert.Contains(
            "if (ViewModel.IsCreatingDetailItem)",
            code,
            StringComparison.Ordinal);
        Assert.Contains(
            "ViewModel.UpdateItemTextAsync(item.Id, DetailTitleTextBox.Text)",
            code,
            StringComparison.Ordinal);
        Assert.Contains("_isDualPane && !creating", masterDetailCode, StringComparison.Ordinal);
        Assert.Contains(
            "<Grid Grid.Row=\"2\" Margin=\"8,0,8,4\">",
            xaml.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(xaml, "Text=\"{Binding CreatedText}\""));

        Assert.Contains(
            "Style=\"{StaticResource TodoNativeCompletionCheckBoxStyle}\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains("x:Key=\"TodoDetailActionIconSize\">13", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DetailBackButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Glyph=\"&#xE72B;\"", xaml[xaml.IndexOf(
            "x:Name=\"DetailBackButton\"", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.Contains("Spacing=\"2\"", xaml[xaml.IndexOf(
            "x:Name=\"DetailHeaderActions\"", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.Contains("Width\" Value=\"28\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyDetailCompletionVisualState", code, StringComparison.Ordinal);
        Assert.DoesNotContain("<ColumnDefinition Width=\"30\" />\n                            <ColumnDefinition Width=\"*\" />\n                            <ColumnDefinition Width=\"30\" />", xaml.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public void CompletionControls_UseNativeNeutralCheckBoxes()
    {
        string xaml = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoWidgetContent.xaml"));
        string code = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoWidgetContent.EditingAndUndo.cs"));

        Assert.Contains("x:Key=\"TodoNativeCompletionCheckBoxStyle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BasedOn=\"{StaticResource DefaultCheckBoxStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TodoCompletionCheckBox\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"DetailCompletionCheckBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CheckBoxCheckBackgroundFillChecked", xaml, StringComparison.Ordinal);
        Assert.Contains("ResourceKey=\"TextFillColorPrimaryBrush\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"{ThemeResource TextFillColorSecondaryBrush}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"TodoCompletionBox\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"DetailCompletionBox\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("completionAccentBrush", code, StringComparison.Ordinal);
    }

    [Fact]
    public void FilterChange_ConfirmsDiscardBeforeChangingContext()
    {
        string code = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoWidgetContent.xaml.cs"));

        Assert.Contains(
            "private async void TodoFilterSegmented_SelectionChanged",
            code,
            StringComparison.Ordinal);
        Assert.Contains("if (ViewModel.IsCreatingDetailItem)", code, StringComparison.Ordinal);
        Assert.Contains("return await DiscardCreatingDetailAsync();", code, StringComparison.Ordinal);
        Assert.Contains("SelectFilter(filter);", code, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateConfirmation_ImmediatelyExitsSinglePaneDetailAfterSaving()
    {
        string code = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoWidgetContent.xaml.cs"));
        int handlerStart = code.IndexOf(
            "private async void DetailSaveButton_Click",
            StringComparison.Ordinal);
        int handlerEnd = code.IndexOf(
            "private async void DetailImportantButton_Click",
            StringComparison.Ordinal);

        Assert.True(handlerStart >= 0 && handlerEnd > handlerStart);
        string handler = code[handlerStart..handlerEnd];
        Assert.Contains("if (!_isDualPane)", handler, StringComparison.Ordinal);
        Assert.Contains("ViewModel?.CloseDetail();", handler, StringComparison.Ordinal);
        Assert.Contains("ClearTodoListContainerSelection();", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("PlayDetailExitAnimationAsync", handler, StringComparison.Ordinal);
        Assert.Contains("ShowTodoStatus(\"Todo.Status.Saved\")", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void ListCompletionAndRetainedDetailActions_ReportSpecificStatusFeedback()
    {
        string contentCode = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoWidgetContent.xaml.cs"));
        string interactionCode = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoWidgetContent.DragDrop.cs"));
        string feedbackCode = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoWidgetContent.EditingAndUndo.cs"));

        Assert.Contains("SetImportantWithFeedbackAsync", contentCode, StringComparison.Ordinal);
        Assert.Contains("SetCompletedWithFeedbackAsync", interactionCode, StringComparison.Ordinal);
        Assert.Contains("SetImportantWithFeedbackAsync", interactionCode, StringComparison.Ordinal);
        Assert.Contains("Todo.Status.Saved", feedbackCode, StringComparison.Ordinal);
        Assert.Contains("Todo.Status.MarkedCompleted", feedbackCode, StringComparison.Ordinal);
        Assert.Contains("Todo.Status.MarkedActive", feedbackCode, StringComparison.Ordinal);
        Assert.Contains("Todo.Status.MarkedImportant", feedbackCode, StringComparison.Ordinal);
        Assert.Contains("Todo.Status.UnmarkedImportant", feedbackCode, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickCapture_EditAndSaveUseTheLeftActionAndCreateRequiresExplicitCommit()
    {
        string xaml = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/QuickCaptureSurfaceContent.xaml"));
        string code = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/QuickCaptureSurfaceContent.xaml.cs"));

        Assert.DoesNotContain("DetailCreateSaveButton", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DetailDoneButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"DetailDoneButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DetailEditSaveHost\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Column=\"1\"", xaml[xaml.IndexOf(
            "x:Name=\"DetailEditSaveHost\"", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.True(
            xaml.IndexOf("x:Name=\"DetailDoneButton\"", StringComparison.Ordinal) <
            xaml.IndexOf("x:Name=\"DetailHeaderActions\"", StringComparison.Ordinal));
        Assert.DoesNotContain("showDualPaneCreateSave", code, StringComparison.Ordinal);
        Assert.Contains(
            "DetailBackColumn.Width = new GridLength(8)",
            code,
            StringComparison.Ordinal);
        Assert.Contains("private void ScheduleDetailAutoSave()", code, StringComparison.Ordinal);
        Assert.Contains("if (!_isCreatingDetail && _detailHasUnsavedChanges)", code, StringComparison.Ordinal);
        Assert.Contains(
            "if (_isCreatingDetail)\n        {\n            ClearDetailForViewChange();",
            code.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
        Assert.Contains(
            "if (string.IsNullOrWhiteSpace(body) && _pendingDetailAttachments.Count == 0)",
            code,
            StringComparison.Ordinal);
        string timestampMarkup = xaml[xaml.IndexOf(
            "x:Name=\"DetailTimestampText\"",
            StringComparison.Ordinal)..];
        Assert.Contains("Grid.Row=\"2\"", timestampMarkup, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"QuickCaptureDetailActionIconSize\">13", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"QuickCaptureSharedDetailIconButtonStyle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width\" Value=\"28\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Foreground\" Value=\"{ThemeResource TextFillColorSecondaryBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"13\" Height=\"13\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Spacing=\"2\"", xaml[xaml.IndexOf(
            "x:Name=\"DetailHeaderActions\"", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.Contains("DetailBackColumn.Width = new GridLength(28)", code, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string value) =>
        text.Split(value, StringSplitOptions.None).Length - 1;
}
