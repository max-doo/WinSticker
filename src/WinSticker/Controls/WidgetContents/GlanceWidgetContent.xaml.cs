using System.ComponentModel;
using WinSticker.Services;
using WinSticker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace WinSticker.Controls.WidgetContents;

public sealed partial class GlanceWidgetContent : UserControl
{
    private readonly GlanceWidgetViewModel _viewModel;
    private readonly DispatcherTimer _layoutResizeTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly SolidColorBrush _contrastPlateBrush = new();
    private bool _isLoaded;
    private double _pendingAvailableWidth;
    private double _pendingAvailableHeight;
    private bool _hasPendingAvailableSize;

    public GlanceWidgetContent(GlanceWidgetViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        _layoutResizeTimer.Tick += LayoutResizeTimer_Tick;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        _hasPendingAvailableSize = false;
        _viewModel.UpdateAvailableSize(ActualWidth, ActualHeight);
        ApplyImageAwareTheme();
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        _layoutResizeTimer.Stop();
        _hasPendingAvailableSize = false;
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _pendingAvailableWidth = e.NewSize.Width;
        _pendingAvailableHeight = e.NewSize.Height;
        _hasPendingAvailableSize = true;
        // Apply only the latest size after a resize burst settles.
        _layoutResizeTimer.Stop();
        _layoutResizeTimer.Start();
    }

    private void LayoutResizeTimer_Tick(object? sender, object e)
    {
        _layoutResizeTimer.Stop();
        if (!_isLoaded || !_hasPendingAvailableSize)
        {
            return;
        }

        _hasPendingAvailableSize = false;
        _viewModel.UpdateAvailableSize(_pendingAvailableWidth, _pendingAvailableHeight);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (e.PropertyName is nameof(GlanceWidgetViewModel.ForegroundTheme) or
            nameof(GlanceWidgetViewModel.ShowContrastPlate))
        {
            // Transparent-surface contrast is resolved asynchronously against
            // the sampled wallpaper, so the text scope follows after the fact.
            ApplyImageAwareTheme();
        }
    }

    private void ApplyImageAwareTheme()
    {
        ElementTheme foregroundTheme = _viewModel.ForegroundTheme;
        ContentHost.RequestedTheme = foregroundTheme;
        // The plate always pushes contrast away from the text: light text gets
        // a dark plate, dark text a light one. It never stands in for the
        // surface, it only separates the glyphs from a wallpaper that cannot
        // do it alone. Its corner follows the same Win10/Win11 policy as every
        // other inner surface so the plate never paints outside the window.
        _contrastPlateBrush.Color = foregroundTheme == ElementTheme.Light
            ? Windows.UI.Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(0x2E, 0x00, 0x00, 0x00);
        ContrastPlate.Background = _contrastPlateBrush;
        string cornerPreference = WindowsCompatibilityService
            .ResolveEffectiveWidgetCornerPreference(null);
        ContrastPlate.CornerRadius = cornerPreference switch
        {
            SettingsService.WidgetCornerPreferenceRound => new CornerRadius(16),
            SettingsService.WidgetCornerPreferenceSmall => new CornerRadius(6),
            _ => new CornerRadius(0)
        };
    }
}

public sealed partial class GlanceBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

public sealed partial class GlanceStringNonEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>Daytime rows keep full opacity; night rows dim a step.</summary>
public sealed partial class GlanceDaytimeToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? 1.0 : 0.65;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
