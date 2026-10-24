using Microsoft.UI.Xaml;
using WinSticker.Contracts;
using WinSticker.Models;
using WinSticker.Services;
using WinSticker.ViewModels;

namespace WinSticker.Controls.WidgetContents;

public sealed class TodoCalendarContentAdapter :
    WidgetContentAdapterBase,
    IWidgetResponsiveLayoutContent,
    IWidgetInteractiveResizeContent,
    IWidgetFeedbackSource,
    IWidgetAddActionContent
{
    public TodoCalendarContentAdapter(
        WidgetConfig config,
        TodoWidgetStore store,
        LocalizationService localizationService,
        SettingsService? settingsService = null)
        : this(
            config,
            new TodoCalendarViewModel(
                store,
                localizationService,
                config,
                settingsService))
    {
    }

    internal TodoCalendarContentAdapter(
        WidgetConfig config,
        TodoCalendarViewModel viewModel,
        Func<TodoCalendarViewModel, FrameworkElement>? viewFactory = null)
        : base(
            config,
            () => (viewFactory ?? (vm => new TodoCalendarContent(vm)))(viewModel))
    {
        if (config.WidgetKind != WidgetKind.Calendar)
        {
            throw new ArgumentException("Calendar content requires a Calendar widget config.", nameof(config));
        }

        ViewModel = viewModel;
    }

    public TodoCalendarViewModel ViewModel { get; }

    public event EventHandler<WidgetFeedbackRequestedEventArgs>? FeedbackRequested;

    public override Task InitializeAsync()
    {
        return ViewModel.InitializeAsync();
    }

    public override Task RefreshAsync()
    {
        return ViewModel.InitializeAsync();
    }

    public void BeginResponsiveLayoutTransition(
        double targetContentWidth,
        double targetContentHeight,
        bool isCollapsing)
    {
    }

    public void CompleteResponsiveLayoutTransition(
        double finalContentWidth,
        double finalContentHeight)
    {
    }

    public void CancelResponsiveLayoutTransition()
    {
    }

    public void BeginInteractiveResize(double contentWidth, double contentHeight)
    {
    }

    public void CompleteInteractiveResize(double contentWidth, double contentHeight)
    {
    }

    public Task AddFromTitleButtonAsync()
    {
        if (MaterializedView is TodoCalendarContent content)
        {
            content.OpenInlineCreateForSelectedDate();
        }

        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (ViewModel is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
