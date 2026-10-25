using WinSticker.Models;
using WinSticker.Services;
using WinSticker.ViewModels;
using Microsoft.UI.Xaml;

namespace WinSticker.Tests;

public sealed class TodoMasterStepsExposureTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _widgetsDataRoot;

    public TodoMasterStepsExposureTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "WinSticker.Tests", Guid.NewGuid().ToString("N"));
        _widgetsDataRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "widgets")).FullName;
    }

    [Fact]
    public async Task MasterStepProgress_ReflectsDetailStepCompletion()
    {
        var viewModel = CreateViewModel();
        await viewModel.InitializeAsync();
        var item = await viewModel.AddItemAsync("master card task") ?? throw new InvalidOperationException();
        var first = await viewModel.AddStepAsync(item.Id, "first") ?? throw new InvalidOperationException();
        var second = await viewModel.AddStepAsync(item.Id, "second") ?? throw new InvalidOperationException();

        Assert.Equal("0/2", item.StepProgressText);
        Assert.Equal(Visibility.Visible, item.StepProgressVisibility);
        Assert.Equal(Visibility.Collapsed, item.StepProgressAllVisibility);
        Assert.Equal(Visibility.Visible, item.StepProgressPartialVisibility);
        Assert.Equal(2, item.TotalStepCount);

        await viewModel.SetStepCompletedAsync(item.Id, first.Id, true);

        Assert.Equal("1/2", item.StepProgressText);
        Assert.False(item.AreAllStepsCompleted);
        Assert.Equal(Visibility.Collapsed, item.StepNudgeVisibility);

        await viewModel.SetStepCompletedAsync(item.Id, second.Id, true);

        Assert.True(item.AreAllStepsCompleted);
        Assert.Equal(Visibility.Collapsed, item.StepProgressPartialVisibility);
        Assert.Equal(Visibility.Visible, item.StepProgressAllVisibility);
        // The completion nudge appears only once the region is expanded.
        Assert.Equal(Visibility.Collapsed, item.StepNudgeVisibility);
        item.IsStepsExpanded = true;
        Assert.Equal(Visibility.Visible, item.StepNudgeVisibility);
    }

    [Fact]
    public async Task MasterStepNudge_HidesWhenParentCompletes()
    {
        var viewModel = CreateViewModel();
        await viewModel.InitializeAsync();
        var item = await viewModel.AddItemAsync("nudge task") ?? throw new InvalidOperationException();
        var step = await viewModel.AddStepAsync(item.Id, "only") ?? throw new InvalidOperationException();
        await viewModel.SetStepCompletedAsync(item.Id, step.Id, true);

        item.IsStepsExpanded = true;
        Assert.Equal(Visibility.Visible, item.StepNudgeVisibility);

        await viewModel.SetCompletedAsync(item.Id, true);

        Assert.True(item.IsCompleted);
        Assert.Equal(Visibility.Collapsed, item.StepNudgeVisibility);
    }

    [Fact]
    public void MasterStepExpansion_TogglesDerivedCardState()
    {
        var item = new TodoItemViewModel(new TodoItem
        {
            Text = "expansion",
            Steps =
            [
                new TodoStep { Id = "s1", Text = "one", IsCompleted = true, SortOrder = 0 },
                new TodoStep { Id = "s2", Text = "two", SortOrder = 1 }
            ]
        });

        Assert.False(item.IsStepsExpanded);
        Assert.Equal(Visibility.Collapsed, item.StepsExpandedVisibility);
        string collapsedToggleText = item.StepProgressToggleText;
        string collapsedChevron = item.StepProgressChevronGlyph;
        Assert.Contains("1/2", collapsedToggleText, StringComparison.Ordinal);

        item.IsStepsExpanded = true;

        Assert.Equal(Visibility.Visible, item.StepsExpandedVisibility);
        Assert.Equal("1/2", item.StepProgressText);
        Assert.NotEqual(collapsedToggleText, item.StepProgressToggleText, StringComparer.Ordinal);
        Assert.NotEqual(collapsedChevron, item.StepProgressChevronGlyph, StringComparer.Ordinal);
    }

    [Fact]
    public async Task CollapseAllExpanded_AlsoCollapsesMasterStepExpansion()
    {
        var viewModel = CreateViewModel();
        await viewModel.InitializeAsync();
        var item = await viewModel.AddItemAsync("collapse task") ?? throw new InvalidOperationException();
        await viewModel.AddStepAsync(item.Id, "step");

        item.IsStepsExpanded = true;
        viewModel.CollapseAllExpanded();

        Assert.False(item.IsStepsExpanded);
    }

    [Fact]
    public async Task MasterStepProgress_PersistsAcrossReloadAsCollapsedUiState()
    {
        string storeId = "master-steps-reload";
        var viewModel = CreateViewModel(storeId);
        await viewModel.InitializeAsync();
        var item = await viewModel.AddItemAsync("reload task") ?? throw new InvalidOperationException();
        var step = await viewModel.AddStepAsync(item.Id, "persisted") ?? throw new InvalidOperationException();
        await viewModel.SetStepCompletedAsync(item.Id, step.Id, true);
        item.IsStepsExpanded = true;

        var reloaded = CreateViewModel(storeId);
        await reloaded.InitializeAsync();
        var reloadedItem = reloaded.Items.Single(existing => existing.Text == "reload task");

        Assert.Equal("1/1", reloadedItem.StepProgressText);
        // Expansion is UI state on the card and must not leak into storage.
        Assert.False(reloadedItem.IsStepsExpanded);
    }

    private TodoWidgetViewModel CreateViewModel(string widgetId = "master-steps")
    {
        return new TodoWidgetViewModel(
            new TodoWidgetStore(_widgetsDataRoot, widgetId),
            TestServices.CreateLocalizationService(),
            new WidgetConfig { Id = widgetId, Name = "Todo", WidgetKind = WidgetKind.Todo });
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}
