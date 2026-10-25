namespace WinSticker.Tests;

public sealed class TodoMasterStepsCardContractTests
{
    [Fact]
    public void MasterCard_ExposesStepProgressChipAndInlineCheckSurface()
    {
        string xaml = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoWidgetContent.xaml"));

        // Progress chip in the secondary metadata row, sharing it with the
        // due status and the attachment summary.
        Assert.Contains("x:Name=\"TodoStepProgressButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"TodoStepProgressButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains(
            "Visibility=\"{Binding StepProgressVisibility}\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains("Glyph=\"{Binding StepProgressListGlyph}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Glyph=\"{Binding StepProgressChevronGlyph}\"", xaml, StringComparison.Ordinal);
        Assert.Contains(
            "Visibility=\"{Binding StepProgressPartialVisibility}\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Visibility=\"{Binding StepProgressAllVisibility}\"",
            xaml,
            StringComparison.Ordinal);

        // Inline expansion region: progress bar plus check-only step rows.
        Assert.Contains("x:Name=\"TodoCardStepsPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains(
            "Visibility=\"{Binding StepsExpandedVisibility}\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding CompletedStepCount}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"{Binding TotalStepCount}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"CardStepCheckBox_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains(
            "IsChecked=\"{Binding IsCompleted, Mode=OneWay}\"",
            xaml,
            StringComparison.Ordinal);

        // Completion nudge only while expanded with every step done.
        Assert.Contains("x:Name=\"TodoStepNudgeButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains(
            "Visibility=\"{Binding StepNudgeVisibility}\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains("Click=\"TodoStepNudgeButton_Click\"", xaml, StringComparison.Ordinal);

        // The chip and the expanded region keep clear of the hover-only
        // copy/important action host floating over the card's right edge.
        Assert.Contains("Margin=\"0,0,58,0\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MasterStepsSurface_WritesThroughTheSharedStepOperations()
    {
        string code = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoWidgetContent.MasterSteps.cs"));

        // Completion writes funnel through the same entry point the detail
        // page uses, so both surfaces can never disagree.
        Assert.Contains("ViewModel.SetStepCompletedAsync(", code, StringComparison.Ordinal);
        Assert.Contains("SetCompletedWithFeedbackAsync(item, isCompleted: true)", code, StringComparison.Ordinal);
        Assert.Contains("item.IsStepsExpanded = !item.IsStepsExpanded;", code, StringComparison.Ordinal);

        // The card surface is check-only by design: structural step editing
        // stays in the detail page.
        Assert.DoesNotContain("AddStepAsync", code, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateStepTextAsync", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteStepAsync", code, StringComparison.Ordinal);
    }

    [Fact]
    public void CalendarChips_CarryReadOnlyStepProgress()
    {
        string viewModel = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/ViewModels/TodoCalendarViewModel.cs"));
        string calendarCode = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/TodoCalendarContent.xaml.cs"));

        Assert.Contains("item.Steps.Count > 0", viewModel, StringComparison.Ordinal);
        Assert.Contains("chip.StepProgress", calendarCode, StringComparison.Ordinal);
    }
}
