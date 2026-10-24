using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Controls;
using WinSticker.Helpers;
using WinSticker.Models;
using WinSticker.Platform;
using WinSticker.Services;
using WinSticker.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using Windows.System;
using WinRT;
using WinRT.Interop;
namespace WinSticker.Views;
public sealed partial class QuickPanelWindow : Window
{
    private readonly QuickPanelViewModel _viewModel;
    private readonly SettingsService _settingsService;
    private readonly ThemeService? _themeService;
    private AppWindow? _appWindow;
    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _lastActiveHwnd = IntPtr.Zero;
    private const uint QuickPanelSubclassId = 101;
    private Win32Helper.SubclassProc? _subclassProc;
    private bool _isSubclassInstalled;
    private bool _hasPositioned;
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }
    // Native backdrop controllers
    private MicaController? _micaController;
    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _backdropConfiguration;
    private ICompositionSupportsSystemBackdrop? _backdropTarget;
    private bool _micaControllerAttached;
    private bool _acrylicControllerAttached;
    private const int DefaultPanelWidth = 520;
    private const int DefaultPanelHeight = 620;
    public bool IsPanelVisible { get; private set; }
    public QuickPanelViewModel ViewModel => _viewModel;
    public event EventHandler? SettingsRequested;
    #region Win32 Constants
// VK_CONTROL provided by Win32Helper
    private const byte VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    #endregion
    public QuickPanelWindow(
        QuickPanelViewModel viewModel,
        SettingsService settingsService)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        _themeService = (App.Current as App)?.ThemeService;
        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        RootGrid.DataContext = _viewModel;
        ClipboardView.DataContext = _viewModel;
        TodoView.DataContext = _viewModel;
        NotesView.DataContext = _viewModel;
        ConfigureWindow();
        ApplyTheme();
        _viewModel.RequestPasteItem += OnViewModelRequestPasteItem;
        _viewModel.RequestClose += OnViewModelRequestClose;
        _viewModel.RequestOpenSettings += OnViewModelRequestOpenSettings;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _settingsService.SettingsChanged += OnAppearanceSettingsChanged;
        _settingsService.AppearancePreviewChanged += OnAppearanceSettingsChanged;
        if (_themeService is not null)
        {
            _themeService.AppearanceChanged += OnThemeServiceAppearanceChanged;
        }
        Activated += OnWindowActivated;
        Closed += OnWindowClosed;
        UpdateTabPresenter();
    }
    private void ConfigureWindow()
    {
        if (_appWindow is null) return;
        WindowShellState.TryApplyBorderlessResizableOverlappedPresenter(_appWindow);
        _appWindow.Resize(new SizeInt32(DefaultPanelWidth, DefaultPanelHeight));
        int extendedStyle = Win32Helper.GetWindowLong(_hwnd, Win32Helper.GWL_EXSTYLE);
        extendedStyle |= Win32Helper.WS_EX_TOOLWINDOW;
        extendedStyle &= ~Win32Helper.WS_EX_NOACTIVATE;
        Win32Helper.SetWindowLongPtr(_hwnd, Win32Helper.GWL_EXSTYLE, new IntPtr(extendedStyle));
        int style = Win32Helper.GetWindowLong(_hwnd, Win32Helper.GWL_STYLE);
        style &= ~(Win32Helper.WS_CAPTION | Win32Helper.WS_BORDER | Win32Helper.WS_DLGFRAME);
        style |= Win32Helper.WS_THICKFRAME;
        Win32Helper.SetWindowLong(_hwnd, Win32Helper.GWL_STYLE, style);
        Win32Helper.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
            Win32Helper.SWP_NOMOVE | Win32Helper.SWP_NOSIZE |
            Win32Helper.SWP_NOACTIVATE | Win32Helper.SWP_FRAMECHANGED);
        Win32Helper.SetWindowBorderColor(_hwnd, unchecked((int)0xFFFFFFFE));
        Win32Helper.ApplyFullWindowFrame(_hwnd);
        InstallSubclass();
        ApplyWindowCornerPreference();
        UpdateTopMost();
    }
    private void InstallSubclass()
    {
        if (_isSubclassInstalled || _hwnd == IntPtr.Zero) return;
        _subclassProc = CallbackDepth.WrapSubclassProc(SubclassProc);
        _isSubclassInstalled = Win32Helper.SetWindowSubclass(_hwnd, _subclassProc, QuickPanelSubclassId, UIntPtr.Zero);
    }
    private void RemoveSubclass()
    {
        if (!_isSubclassInstalled || _hwnd == IntPtr.Zero || _subclassProc is null) return;
        Win32Helper.RemoveWindowSubclass(_hwnd, _subclassProc, QuickPanelSubclassId);
        _isSubclassInstalled = false;
    }
    private IntPtr SubclassProc(IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (uMsg == (uint)Win32Helper.WM_NCHITTEST)
        {
            if (Win32Helper.GetWindowRect(hWnd, out var rect))
            {
                int x = unchecked((short)(long)lParam);
                int y = unchecked((short)((long)lParam >> 16));
                double scale = Win32Helper.GetDpiScaleForWindow(hWnd, RootGrid.XamlRoot);
                int border = Math.Max(6, (int)Math.Round(8 * scale));
                bool onLeft = x >= rect.Left && x < rect.Left + border;
                bool onRight = x <= rect.Right && x > rect.Right - border;
                bool onTop = y >= rect.Top && y < rect.Top + border;
                bool onBottom = y <= rect.Bottom && y > rect.Bottom - border;
                if (onTop && onLeft) return (IntPtr)Win32Helper.HTTOPLEFT;
                if (onTop && onRight) return (IntPtr)Win32Helper.HTTOPRIGHT;
                if (onBottom && onLeft) return (IntPtr)Win32Helper.HTBOTTOMLEFT;
                if (onBottom && onRight) return (IntPtr)Win32Helper.HTBOTTOMRIGHT;
                if (onLeft) return (IntPtr)Win32Helper.HTLEFT;
                if (onRight) return (IntPtr)Win32Helper.HTRIGHT;
                if (onTop) return (IntPtr)Win32Helper.HTTOP;
                if (onBottom) return (IntPtr)Win32Helper.HTBOTTOM;
            }
        }
        else if (uMsg == (uint)Win32Helper.WM_GETMINMAXINFO)
        {
            var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            double scale = Win32Helper.GetDpiScaleForWindow(hWnd, RootGrid.XamlRoot);
            minMaxInfo.MinTrackSize.X = Math.Max(minMaxInfo.MinTrackSize.X, (int)Math.Round(380 * scale));
            minMaxInfo.MinTrackSize.Y = Math.Max(minMaxInfo.MinTrackSize.Y, (int)Math.Round(420 * scale));
            Marshal.StructureToPtr(minMaxInfo, lParam, false);
            return IntPtr.Zero;
        }
        else if (uMsg == 0x0082 /* WM_NCDESTROY */)
        {
            RemoveSubclass();
        }
        return Win32Helper.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }
    private void UpdateTopMost()
    {
        bool isPinned = _viewModel.IsPinned;
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = isPinned;
        }
        if (_hwnd != IntPtr.Zero)
        {
            IntPtr insertAfter = isPinned ? Win32Helper.HWND_TOPMOST : Win32Helper.HWND_NOTOPMOST;
            Win32Helper.SetWindowPos(_hwnd, insertAfter, 0, 0, 0, 0,
                Win32Helper.SWP_NOMOVE | Win32Helper.SWP_NOSIZE | Win32Helper.SWP_NOACTIVATE);
        }
    }
    private void ApplyWindowCornerPreference()
    {
        string effectivePreference = WindowsCompatibilityService.ResolveEffectiveWidgetCornerPreference(
            _settingsService.Settings.WidgetShell.WidgetCornerPreference);
        int cornerPreference = effectivePreference switch
        {
            SettingsService.WidgetCornerPreferenceSquare => Win32Helper.DWMWCP_DONOTROUND,
            SettingsService.WidgetCornerPreferenceSmall => Win32Helper.DWMWCP_ROUNDSMALL,
            _ => Win32Helper.DWMWCP_ROUND
        };
        Win32Helper.TrySetDwmWindowAttribute(
            _hwnd, Win32Helper.DWMWA_WINDOW_CORNER_PREFERENCE,
            ref cornerPreference);
        PopupBorderOverlay.CornerRadius = cornerPreference switch
        {
            Win32Helper.DWMWCP_DONOTROUND => new CornerRadius(0),
            Win32Helper.DWMWCP_ROUNDSMALL => new CornerRadius(4),
            _ => new CornerRadius(8)
        };
    }
    public async void ShowPanel()
    {
        var fg = Win32Helper.GetForegroundWindow();
        if (fg != IntPtr.Zero && fg != _hwnd)
        {
            _lastActiveHwnd = fg;
        }
        if (!_hasPositioned)
        {
            PositionOnScreen();
            _hasPositioned = true;
        }
        ApplyMaterialFromSettings();
        RootGrid.Opacity = 0;
        PopupTranslateTransform.Y = 4;
        _appWindow?.Show();
        IsPanelVisible = true;
        UpdateTopMost();
        Activate();
        Win32Helper.SetForegroundWindow(_hwnd);
        PopupShowStoryboard.Begin();
        (App.Current as App)?.RefreshQuickCaptureClipboardService(captureCurrent: true);
        await _viewModel.RefreshAllAsync();
    }
    public void HidePanel()
    {
        if (!IsPanelVisible) return;
        IsPanelVisible = false;
        PopupHideStoryboard.Completed += OnPopupHideCompleted;
        PopupHideStoryboard.Begin();
    }
    private void OnPopupHideCompleted(object? sender, object e)
    {
        PopupHideStoryboard.Completed -= OnPopupHideCompleted;
        if (!IsPanelVisible)
        {
            _appWindow?.Hide();
        }
    }
    public void TogglePanel()
    {
        if (IsPanelVisible)
        {
            HidePanel();
        }
        else
        {
            ShowPanel();
        }
    }
    private void PositionOnScreen()
    {
        if (_appWindow is null) return;
        var displayArea = DisplayArea.GetFromWindowId(
            _appWindow.Id, DisplayAreaFallback.Primary);
        int workWidth = displayArea.WorkArea.Width;
        int workHeight = displayArea.WorkArea.Height;
        int workLeft = displayArea.WorkArea.X;
        int workTop = displayArea.WorkArea.Y;
        int currentWidth = _appWindow.Size.Width > 0 ? _appWindow.Size.Width : DefaultPanelWidth;
        int x = workLeft + (workWidth - currentWidth) / 2;
        int y = workTop + (int)(workHeight * 0.20);
        _appWindow.Move(new PointInt32(x, y));
    }
    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_backdropConfiguration is not null)
        {
            _backdropConfiguration.IsInputActive =
                args.WindowActivationState != WindowActivationState.Deactivated;
        }
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            if (!_viewModel.IsPinned && IsPanelVisible)
            {
                HidePanel();
            }
        }
    }
    private async Task<bool> PasteToTargetWindowAsync(IntPtr targetHwnd)
    {
        if (targetHwnd == IntPtr.Zero || targetHwnd == _hwnd || !Win32Helper.IsWindow(targetHwnd))
        {
            return false;
        }
        if (Win32Helper.IsIconic(targetHwnd))
        {
            Win32Helper.ShowWindow(targetHwnd, 9); // SW_RESTORE
        }
        var currentThreadId = Win32Helper.GetCurrentThreadId();
        var targetThreadId = Win32Helper.GetWindowThreadProcessId(targetHwnd, out _);
        var attached = targetThreadId != 0 && targetThreadId != currentThreadId &&
                       Win32Helper.AttachThreadInput(currentThreadId, targetThreadId, true);
        try
        {
            Win32Helper.BringWindowToTop(targetHwnd);
            Win32Helper.SetForegroundWindow(targetHwnd);
        }
        finally
        {
            if (attached)
            {
                Win32Helper.AttachThreadInput(currentThreadId, targetThreadId, false);
            }
        }
        await Task.Delay(80);
        if (Win32Helper.GetForegroundWindow() != targetHwnd)
        {
            return false;
        }
        return Win32Helper.TrySendKeyChord(Win32Helper.VK_CONTROL, VK_V);
    }
    private async void OnViewModelRequestPasteItem(object? sender, QuickCaptureItem item)
    {
        var targetHwnd = _lastActiveHwnd;
        if (!_viewModel.IsPinned)
        {
            HidePanel();
            await Task.Delay(50);
        }
        bool success = await PasteToTargetWindowAsync(targetHwnd);
        if (!success)
        {
            _viewModel.ShowToast("未找到原目标窗口，请重试");
        }
    }
    private void OnViewModelRequestClose(object? sender, EventArgs e)
    {
        HidePanel();
    }
    private void OnViewModelRequestOpenSettings(object? sender, EventArgs e)
    {
        HidePanel();
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuickPanelViewModel.StatusMessage) && !string.IsNullOrEmpty(_viewModel.StatusMessage))
        {
            StatusInfoBar.Message = _viewModel.StatusMessage;
            StatusInfoBar.IsOpen = true;
            var timer = DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(2.5);
            timer.IsRepeating = false;
            timer.Tick += (s, args) =>
            {
                StatusInfoBar.IsOpen = false;
                timer.Stop();
            };
            timer.Start();
        }
        else if (e.PropertyName == nameof(QuickPanelViewModel.ActiveTab))
        {
            UpdateTabPresenter();
        }
        else if (e.PropertyName == nameof(QuickPanelViewModel.IsPinned))
        {
            if (PinButton.IsChecked != _viewModel.IsPinned)
            {
                PinButton.IsChecked = _viewModel.IsPinned;
            }
            UpdateTopMost();
        }
    }
    private void UpdateTabPresenter()
    {
        MainTabSegmented.SelectedIndex = _viewModel.ActiveTab switch
        {
            QuickPanelTab.Clipboard => 0,
            QuickPanelTab.Todo => 1,
            QuickPanelTab.Notes => 2,
            _ => 0
        };
        ClipboardView.Visibility = _viewModel.ActiveTab == QuickPanelTab.Clipboard ? Visibility.Visible : Visibility.Collapsed;
        TodoView.Visibility = _viewModel.ActiveTab == QuickPanelTab.Todo ? Visibility.Visible : Visibility.Collapsed;
        NotesView.Visibility = _viewModel.ActiveTab == QuickPanelTab.Notes ? Visibility.Visible : Visibility.Collapsed;
    }
    private void MainTabSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.ActiveTab = MainTabSegmented.SelectedIndex switch
        {
            0 => QuickPanelTab.Clipboard,
            1 => QuickPanelTab.Todo,
            2 => QuickPanelTab.Notes,
            _ => QuickPanelTab.Clipboard
        };
    }
    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsPinned = PinButton.IsChecked == true;
        UpdateTopMost();
    }
    private void Header_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;
        if (e.OriginalSource is DependencyObject dep)
        {
            if (TryFindAncestor<ButtonBase>(dep, out _) || TryFindAncestor<TextBox>(dep, out _))
            {
                return;
            }
        }
        Win32Helper.ReleaseCapture();
        Win32Helper.SendMessage(_hwnd, 0x00A1 /* WM_NCLBUTTONDOWN */, (IntPtr)2 /* HTCAPTION */, IntPtr.Zero);
    }
    private static bool TryFindAncestor<T>(DependencyObject? source, out T result) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T typed)
            {
                result = typed;
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        result = null!;
        return false;
    }
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenSettings();
    }
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        HidePanel();
    }
    private void RootGrid_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            HidePanel();
            return;
        }
        if (e.Key == VirtualKey.Tab && Win32Helper.IsKeyPressed(VirtualKey.Control))
        {
            e.Handled = true;
            bool backward = Win32Helper.IsKeyPressed(VirtualKey.Shift);
            _viewModel.CycleTab(backward);
        }
    }
    #region Theming & Backdrop
    private void OnAppearanceSettingsChanged()
    {
        void ApplyAppearance()
        {
            ApplyWindowCornerPreference();
            if (IsPanelVisible)
            {
                ApplyMaterialFromSettings();
            }
        }
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyAppearance();
        }
        else
        {
            DispatcherQueue.TryEnqueue(ApplyAppearance);
        }
    }
    private void OnThemeServiceAppearanceChanged()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyTheme();
        }
        else
        {
            DispatcherQueue.TryEnqueue(ApplyTheme);
        }
    }
    private void ApplyTheme()
    {
        ElementTheme desiredTheme = _themeService?.CurrentTheme ?? ElementTheme.Default;
        RootGrid.RequestedTheme = desiredTheme;
        ApplyMaterialFromSettings();
    }
    private void ApplyMaterialFromSettings()
    {
        if (_hwnd == IntPtr.Zero) return;
        bool isDark = RootGrid.ActualTheme == ElementTheme.Dark;
        var accentColor = _themeService?.GetEffectiveAccentColor()
                          ?? AccentColorHelper.DefaultAccentColor;
        string materialType = WindowsCompatibilityService.ResolveWidgetMaterialType(
            _settingsService.Settings.WidgetShell.WidgetMaterialType);
        double surfaceOpacity = Math.Clamp(_settingsService.Settings.WidgetShell.WidgetOpacity, 0.0, 1.0);
        double materialIntensity = Math.Clamp(_settingsService.Settings.WidgetShell.WidgetMaterialIntensity, 0.0, 1.0);
        try
        {
            Win32Helper.SetWindowTheme(_hwnd, isDark);
            Win32Helper.ApplyFullWindowFrame(_hwnd);
            int backdropType;
            bool controllerApplied = false;
            if (SettingsService.IsMicaMaterial(materialType))
            {
                controllerApplied = ApplyMicaController(
                    isDark,
                    BuildNativeBackdropTintColor(isDark, accentColor, materialIntensity),
                    materialType == SettingsService.WidgetMaterialTypeMicaAlt);
            }
            if (!controllerApplied && SettingsService.IsAcrylicMaterial(materialType))
            {
                controllerApplied = ApplyAcrylicController(
                    isDark,
                    BuildNativeBackdropTintColor(isDark, accentColor, materialIntensity),
                    surfaceOpacity,
                    materialType == SettingsService.WidgetMaterialTypeAcrylicBase);
            }
            if (controllerApplied)
            {
                backdropType = Win32Helper.DWMSBT_NONE;
                Win32Helper.TrySetDwmWindowAttribute(_hwnd, Win32Helper.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType);
                Win32Helper.DisableAccentPolicy(_hwnd);
                RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x01, 0x00, 0x00, 0x00));
            }
            else if (materialType is SettingsService.WidgetMaterialTypeSolid)
            {
                DetachAcrylicControllerTarget();
                DetachMicaControllerTarget();
                backdropType = Win32Helper.DWMSBT_NONE;
                Win32Helper.TrySetDwmWindowAttribute(_hwnd, Win32Helper.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType);
                Win32Helper.DisableAccentPolicy(_hwnd);
                RootGrid.Background = new SolidColorBrush(
                    BuildOpaquePopupSurfaceColor(isDark, accentColor, materialIntensity));
            }
            else
            {
                backdropType = Win32Helper.DWMSBT_TRANSIENTWINDOW;
                Win32Helper.TrySetDwmWindowAttribute(_hwnd, Win32Helper.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType);
                DetachAcrylicControllerTarget();
                DetachMicaControllerTarget();
                double accentOpacity = Math.Min(surfaceOpacity, 0.52);
                Win32Helper.ApplyAccentBlur(
                    _hwnd,
                    BuildNativeBackdropTintColor(isDark, accentColor, materialIntensity),
                    accentOpacity,
                    true);
                RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x01, 0x00, 0x00, 0x00));
            }
        }
        catch (Exception ex)
        {
            App.Log($"[QuickPanel] ApplyMaterialFromSettings fallback: {ex.Message}");
            DisposeAcrylicController();
            DisposeMicaController();
            RootGrid.Background = new SolidColorBrush(
                BuildOpaquePopupSurfaceColor(isDark, accentColor, materialIntensity));
        }
        var (thickness, borderColor) = GetPopupBorderVisuals(isDark, accentColor);
        PopupBorderOverlay.BorderThickness = new Thickness(thickness);
        PopupBorderOverlay.BorderBrush = borderColor.A == 0 ? null : new SolidColorBrush(borderColor);
    }
    private static Windows.UI.Color BuildNativeBackdropTintColor(
        bool isDark, Windows.UI.Color accentColor, double materialIntensity)
    {
        var baseColor = isDark
            ? Windows.UI.Color.FromArgb(0xFF, 0x20, 0x22, 0x26)
            : Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        double accentMix = 0.07 * materialIntensity;
        return BlendColors(baseColor, accentColor, accentMix);
    }
    private bool ApplyMicaController(bool isDark, Windows.UI.Color tintColor, bool useAlt)
    {
        if (!WindowsCompatibilityService.SupportsMica)
        {
            DisposeMicaController();
            return false;
        }
        _backdropTarget ??= this.As<ICompositionSupportsSystemBackdrop>();
        _backdropConfiguration ??= new SystemBackdropConfiguration();
        _backdropConfiguration.IsInputActive = true;
        _backdropConfiguration.Theme = isDark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light;
        if (_micaController is not null)
        {
            DisposeMicaController();
        }
        _micaController = new MicaController
        {
            Kind = useAlt ? MicaKind.BaseAlt : MicaKind.Base
        };
        DetachAcrylicControllerTarget();
        if (!_micaControllerAttached)
        {
            if (!_micaController.AddSystemBackdropTarget(_backdropTarget))
            {
                DisposeMicaController();
                return false;
            }
            _micaControllerAttached = true;
            _micaController.SetSystemBackdropConfiguration(_backdropConfiguration);
        }
        _micaController.TintColor = tintColor;
        _micaController.FallbackColor = useAlt
            ? isDark ? Windows.UI.Color.FromArgb(0xFF, 0x16, 0x18, 0x1D) : Windows.UI.Color.FromArgb(0xFF, 0xE8, 0xEA, 0xEF)
            : isDark ? Windows.UI.Color.FromArgb(0xFF, 0x20, 0x22, 0x26) : Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        double intensity = Math.Clamp(_settingsService.Settings.WidgetShell.WidgetMaterialIntensity, 0.0, 1.0);
        double tintOpacity = useAlt ? Lerp(0.28, 0.82, intensity) : Lerp(0.04, 0.46, intensity);
        double luminosityOpacity = useAlt
            ? Lerp(isDark ? 0.34 : 0.42, isDark ? 0.72 : 0.76, intensity)
            : Lerp(isDark ? 0.78 : 0.82, isDark ? 0.94 : 0.96, intensity);
        _micaController.TintOpacity = (float)tintOpacity;
        _micaController.LuminosityOpacity = (float)luminosityOpacity;
        return true;
    }
    private bool ApplyAcrylicController(bool isDark, Windows.UI.Color tintColor, double surfaceOpacity, bool useBase)
    {
        if (!WindowsCompatibilityService.SupportsDesktopAcrylic)
        {
            DisposeAcrylicController();
            return false;
        }
        _backdropTarget ??= this.As<ICompositionSupportsSystemBackdrop>();
        _backdropConfiguration ??= new SystemBackdropConfiguration();
        _backdropConfiguration.IsInputActive = true;
        _backdropConfiguration.Theme = isDark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light;
        if (_acrylicController is not null && !_acrylicController.IsClosed)
        {
            DisposeAcrylicController();
        }
        _acrylicController = new DesktopAcrylicController
        {
            Kind = useBase ? DesktopAcrylicKind.Base : DesktopAcrylicKind.Thin
        };
        DetachMicaControllerTarget();
        if (!_acrylicControllerAttached)
        {
            if (!_acrylicController.AddSystemBackdropTarget(_backdropTarget))
            {
                DisposeAcrylicController();
                return false;
            }
            _acrylicControllerAttached = true;
            _acrylicController.SetSystemBackdropConfiguration(_backdropConfiguration);
        }
        _acrylicController.TintColor = tintColor;
        _acrylicController.FallbackColor = tintColor;
        double intensity = Math.Clamp(_settingsService.Settings.WidgetShell.WidgetMaterialIntensity, 0.0, 1.0);
        double surfaceStrength = Lerp(0.08, 1.0, Math.Clamp(surfaceOpacity, 0.0, 1.0));
        double tintOpacity = useBase
            ? Lerp(isDark ? 0.18 : 0.12, isDark ? 0.72 : 0.62, intensity)
            : Lerp(isDark ? 0.04 : 0.02, isDark ? 0.42 : 0.34, intensity);
        double luminosityOpacity = useBase
            ? Lerp(isDark ? 0.38 : 0.46, isDark ? 0.82 : 0.90, intensity)
            : Lerp(isDark ? 0.16 : 0.22, isDark ? 0.56 : 0.64, intensity);
        _acrylicController.TintOpacity = (float)Math.Clamp(tintOpacity * surfaceStrength, 0.0, 1.0);
        _acrylicController.LuminosityOpacity = (float)Math.Clamp(luminosityOpacity * surfaceStrength, 0.0, 1.0);
        return true;
    }
    private void DisposeMicaController()
    {
        if (_micaController is null) return;
        try
        {
            _micaController.RemoveAllSystemBackdropTargets();
            _micaController.Dispose();
        }
        catch { }
        finally
        {
            _micaController = null;
            _micaControllerAttached = false;
        }
    }
    private void DisposeAcrylicController()
    {
        if (_acrylicController is null) return;
        try
        {
            _acrylicController.RemoveAllSystemBackdropTargets();
            _acrylicController.Dispose();
        }
        catch { }
        finally
        {
            _acrylicController = null;
            _acrylicControllerAttached = false;
        }
    }
    private void DetachMicaControllerTarget()
    {
        if (_micaController is not null && _micaControllerAttached)
        {
            try { _micaController.RemoveAllSystemBackdropTargets(); } catch { }
            _micaControllerAttached = false;
        }
    }
    private void DetachAcrylicControllerTarget()
    {
        if (_acrylicController is not null && _acrylicControllerAttached)
        {
            try { _acrylicController.RemoveAllSystemBackdropTargets(); } catch { }
            _acrylicControllerAttached = false;
        }
    }
    private (double Thickness, Windows.UI.Color BorderColor) GetPopupBorderVisuals(
        bool isDark, Windows.UI.Color accentColor)
    {
        string borderStyle = _settingsService.Settings.WidgetShell.WidgetBorderStyle;
        string colorMode = _settingsService.Settings.WidgetShell.WidgetBorderColorMode;
        var (thickness, alpha) = borderStyle switch
        {
            SettingsService.WidgetBorderStyleMedium => (1.2d, (byte)0x30),
            SettingsService.WidgetBorderStyleThick => (1.6d, (byte)0x48),
            SettingsService.WidgetBorderStyleNone => (0d, (byte)0),
            _ => (0.8d, (byte)0x18)
        };
        if (colorMode == SettingsService.WidgetBorderColorModeNone)
        {
            return (0d, Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }
        bool useAccent = colorMode == SettingsService.WidgetBorderColorModeAccent;
        byte borderAlpha = useAccent ? (byte)Math.Clamp(Math.Round(alpha * 1.35), 0, 255) : alpha;
        byte red = useAccent ? accentColor.R : isDark ? (byte)0xFF : (byte)0x00;
        byte green = useAccent ? accentColor.G : isDark ? (byte)0xFF : (byte)0x00;
        byte blue = useAccent ? accentColor.B : isDark ? (byte)0xFF : (byte)0x00;
        return (thickness, Windows.UI.Color.FromArgb(borderAlpha, red, green, blue));
    }
    private static Windows.UI.Color BuildOpaquePopupSurfaceColor(
        bool isDark, Windows.UI.Color accentColor, double materialIntensity)
    {
        var baseColor = isDark
            ? Windows.UI.Color.FromArgb(0xFF, 0x21, 0x24, 0x2A)
            : Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        double accentMix = 0.05 * materialIntensity;
        return BlendColors(baseColor, accentColor, accentMix);
    }
    private static Windows.UI.Color BlendColors(Windows.UI.Color from, Windows.UI.Color to, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);
        return Windows.UI.Color.FromArgb(
            (byte)(from.A + (to.A - from.A) * amount),
            (byte)(from.R + (to.R - from.R) * amount),
            (byte)(from.G + (to.G - from.G) * amount),
            (byte)(from.B + (to.B - from.B) * amount));
    }
    private static double Lerp(double a, double b, double t) => a + (b - a) * Math.Clamp(t, 0.0, 1.0);
    #endregion
    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        RemoveSubclass();
        _viewModel.RequestPasteItem -= OnViewModelRequestPasteItem;
        _viewModel.RequestClose -= OnViewModelRequestClose;
        _viewModel.RequestOpenSettings -= OnViewModelRequestOpenSettings;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _settingsService.SettingsChanged -= OnAppearanceSettingsChanged;
        _settingsService.AppearancePreviewChanged -= OnAppearanceSettingsChanged;
        if (_themeService is not null)
        {
            _themeService.AppearanceChanged -= OnThemeServiceAppearanceChanged;
        }
        DisposeMicaController();
        DisposeAcrylicController();
    }
}
