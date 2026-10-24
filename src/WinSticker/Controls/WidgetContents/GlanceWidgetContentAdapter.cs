using WinSticker.Contracts;
using WinSticker.Models;
using WinSticker.Services;
using WinSticker.ViewModels;
using Microsoft.UI.Xaml;

namespace WinSticker.Controls.WidgetContents;

public sealed class GlanceWidgetContentAdapter :
    WidgetContentAdapterBase,
    IWidgetResponsiveLayoutContent,
    IWidgetSurfaceAppearanceSource
{
    public GlanceWidgetContentAdapter(
        WidgetConfig config,
        LocalizationService localizationService,
        GlanceWidgetStore? store = null,
        Func<GlanceWidgetViewModel, FrameworkElement>? viewFactory = null,
        SettingsService? settingsService = null)
        : this(
            config,
            new GlanceWidgetViewModel(
                config,
                localizationService,
                store,
                settingsService: settingsService),
            viewFactory)
    {
    }

    private GlanceWidgetContentAdapter(
        WidgetConfig config,
        GlanceWidgetViewModel viewModel,
        Func<GlanceWidgetViewModel, FrameworkElement>? viewFactory)
        : base(
            config,
            () => (viewFactory ?? (vm => new GlanceWidgetContent(vm)))(viewModel))
    {
        ViewModel = viewModel;
    }

    public GlanceWidgetViewModel ViewModel { get; }

    public override Task InitializeAsync() => ViewModel.InitializeAsync();

    public override Task RefreshAsync() => ViewModel.RefreshWeatherAsync();

    public override void ApplyAppearance() => ViewModel.ApplyAppearance();

    public override void OnActivated() => ViewModel.OnActivated();

    public override void OnDeactivated() => ViewModel.OnDeactivated();

    public override void OnWindowVisibilityChanged(bool visible) => ViewModel.OnWindowVisibilityChanged(visible);

    public override void OnWindowRevealCompleted() => ViewModel.OnWindowRevealCompleted();

    public override void OnCompactStateChanged(bool collapsed) => ViewModel.OnCompactStateChanged(collapsed);

    public void BeginResponsiveLayoutTransition(double targetContentWidth, double targetContentHeight, bool isCollapsing)
        => ViewModel.UpdateAvailableSize(targetContentWidth, targetContentHeight);

    public void CompleteResponsiveLayoutTransition(double finalContentWidth, double finalContentHeight)
        => ViewModel.UpdateAvailableSize(finalContentWidth, finalContentHeight);

    public void CancelResponsiveLayoutTransition()
    {
    }

    // ── IWidgetSurfaceAppearanceSource ─────────────────────────
    // The Glance clock is the only widget kind allowed to leave the global
    // material: its transparent style would be meaningless as a global setting
    // (every other surface needs its readable plate). The view model owns the
    // decision, this adapter only forwards it to the host window.

    public string? SurfaceMaterialTypeOverride => ViewModel.SurfaceMaterialTypeOverride;

    public double? SurfaceOpacityOverride => ViewModel.SurfaceOpacityOverride;

    public bool SuppressSurfaceChrome => ViewModel.SuppressSurfaceChrome;

    public event EventHandler? SurfaceAppearanceChanged
    {
        add => ViewModel.SurfaceAppearanceChanged += value;
        remove => ViewModel.SurfaceAppearanceChanged -= value;
    }

    protected override void Dispose(bool disposing)
    {
        ViewModel.Dispose();
    }
}
