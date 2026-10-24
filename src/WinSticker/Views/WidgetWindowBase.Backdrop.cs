// Copyright (c) WinSticker. All rights reserved.

using WinSticker.Helpers;
using WinSticker.Models;
using WinSticker.Platform;
using WinSticker.Services;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.CompilerServices;
using Windows.Graphics;
using WinRT;
using WinRT.Interop;

namespace WinSticker.Views;

public abstract partial class WidgetWindowBase
{
    private readonly record struct BackdropSignature(
        bool IsDark,
        string MaterialType,
        double SurfaceOpacity,
        Windows.UI.Color TintColor,
        double MaterialIntensity);

    /// <summary>
    /// Per-widget material override. <see langword="null"/> follows the global
    /// appearance setting; content that owns its surface (the Glance clock's
    /// transparent style) returns its own value instead.
    /// </summary>
    protected virtual string? MaterialTypeOverride => null;

    /// <summary>
    /// Per-widget surface opacity override. <see langword="null"/> follows the
    /// global <c>WidgetOpacity</c>.
    /// </summary>
    protected virtual double? SurfaceOpacityOverride => null;

    /// <summary>
    /// True when the surface is fully transparent and any border or divider
    /// would betray it.
    /// </summary>
    protected virtual bool SuppressSurfaceChrome => false;

    /// <summary>
    /// The opacity every surface path must use: the per-widget override when
    /// one exists, otherwise the global setting.
    /// </summary>
    protected double ResolveSurfaceOpacity() =>
        Math.Clamp(SurfaceOpacityOverride ?? WidgetOpacity, 0.0, 1.0);

    /// <summary>
    /// The material every surface path must use, before platform resolution.
    /// </summary>
    protected string ResolveRequestedMaterialType() =>
        MaterialTypeOverride ?? SettingsService.Settings.WidgetMaterialType;

    protected void ApplyBackdropPreference()
    {
        if (HWnd == IntPtr.Zero || IsClosing)
        {
            return;
        }

        bool isDark = RootElement.ActualTheme == ElementTheme.Dark;
        double surfaceOpacity = ResolveSurfaceOpacity();
        string requestedMaterialType = ResolveRequestedMaterialType();
        string materialType = WindowsCompatibilityService.ResolveWidgetMaterialType(
            requestedMaterialType);
        var tintColor = materialType == SettingsService.WidgetMaterialTypeSolid
            ? BuildSolidColorBackdropTintColor(isDark, surfaceOpacity)
            : BuildNativeBackdropTintColor(isDark);
        var signature = new BackdropSignature(
            isDark,
            materialType,
            Math.Round(surfaceOpacity, 3),
            tintColor,
            Math.Round(SettingsService.Settings.WidgetMaterialIntensity, 3));

        if (CanReuseAppliedBackdrop(signature))
        {
            ApplySurfaceStyle();
            return;
        }

        try
        {
            // The immersive-dark-mode attribute, sheet-of-glass frame and DWM
            // border depend only on the theme, not the material. Re-issuing
            // them on every material change churns DWM state natively, so they
            // only run when the theme flipped or when no signature was applied
            // yet (first apply / post-dispose / after a failed apply).
            bool dwmThemeChanged = _lastAppliedBackdropSignature is not { } lastSignature ||
                lastSignature.IsDark != isDark;
            if (dwmThemeChanged)
            {
                Win32Helper.SetWindowTheme(HWnd, isDark);
                Win32Helper.ApplyFullWindowFrame(HWnd);
                ApplyDwmBorderStyle(isDark);
            }

            int backdropType;
            bool controllerApplied = false;

            if (materialType == SettingsService.WidgetMaterialTypeNone)
            {
                // Transparent surface: tear every material down instead of
                // replacing it — no blur, no tint, no frame.
                // A backdrop target still has to exist: with no backdrop at
                // all the content island paints the theme's window fill, which
                // reads as a solid black tile in dark theme even though every
                // XAML layer above it is transparent. A zero-alpha tint brush
                // is the whole backdrop, so the desktop shows through.
                DisposeAcrylicController();
                DisposeMicaController();
                ApplyTransparentTintBackdrop();
                // Reuse the "no DWM backdrop" branch below: it stamps
                // DWMSBT_NONE, disables the legacy accent policy and clears the
                // legacy-flag bookkeeping in one place.
                controllerApplied = true;
            }
            else if (materialType == SettingsService.WidgetMaterialTypeSolid)
            {
                ClearTransparentBackdrop();
                controllerApplied = ApplySolidColorBackdrop(tintColor);
            }
            else
            {
                ClearTransparentBackdrop();
                ClearSolidColorBackdrop();

                if (SettingsService.IsMicaMaterial(materialType))
                {
                    controllerApplied = ApplyMicaController(
                        isDark,
                        tintColor,
                        materialType == SettingsService.WidgetMaterialTypeMicaAlt);
                }

                if (!controllerApplied && SettingsService.IsAcrylicMaterial(materialType))
                {
                    controllerApplied = ApplyAcrylicController(
                        isDark,
                        tintColor,
                        surfaceOpacity,
                        materialType == SettingsService.WidgetMaterialTypeAcrylicBase);
                }
            }

            if (controllerApplied)
            {
                LegacyAccentBackdropActive = false;
                backdropType = Win32Helper.DWMSBT_NONE;
                Win32Helper.TrySetDwmWindowAttribute(HWnd, Win32Helper.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType);
                Win32Helper.DisableAccentPolicy(HWnd);
            }
            else
            {
                backdropType = Win32Helper.DWMSBT_TRANSIENTWINDOW;
                Win32Helper.TrySetDwmWindowAttribute(HWnd, Win32Helper.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType);
                DetachAcrylicControllerTarget();
                DetachMicaControllerTarget();
                double accentOpacity = WindowsCompatibilityService.UsesLegacyWindowAcrylic &&
                    SettingsService.IsAcrylicMaterial(materialType)
                        ? WidgetMaterialVisualCalculator.CalculateLegacyAcrylicOpacity(
                            materialType == SettingsService.WidgetMaterialTypeAcrylicBase,
                            surfaceOpacity,
                            SettingsService.Settings.WidgetMaterialIntensity)
                        : Math.Min(surfaceOpacity, 0.52);
                if (WindowsCompatibilityService.UsesLegacyWindowAcrylic)
                {
                    DisposeAcrylicController();
                    DisposeMicaController();
                    SystemBackdrop = null;
                }
                LegacyAccentBackdropActive = ApplyLegacyAccentBackdrop(
                    tintColor,
                    accentOpacity);
                if (!LegacyAccentBackdropActive)
                {
                    App.Log(
                        $"[Backdrop] Legacy accent application failed hwnd=0x{HWnd.ToInt64():X} " +
                        $"material={materialType} opacity={accentOpacity:F3}");
                }
            }

            App.LogVerbose(
                $"[Backdrop] hwnd=0x{HWnd.ToInt64():X} requested={requestedMaterialType} " +
                $"material={materialType} isDark={isDark} " +
                $"opacity={surfaceOpacity:F3} tint=#{tintColor.A:X2}{tintColor.R:X2}{tintColor.G:X2}{tintColor.B:X2} " +
                $"dwmBackdropType={backdropType} " +
                $"acrylicController={AcrylicController is { IsClosed: false } && AcrylicControllerAttached} " +
                $"micaController={MicaController is not null && MicaControllerAttached} " +
                $"solidColorBackdrop={IsSolidColorBackdropActive} " +
                $"legacyAccent={LegacyAccentBackdropActive}");
            if (WindowsCompatibilityService.UsesLegacyWindowAcrylic)
            {
                App.Log(
                    $"[Backdrop.Win10] hwnd=0x{HWnd.ToInt64():X} requested={requestedMaterialType} " +
                    $"resolved={materialType} desktopAcrylicSupported={WindowsCompatibilityService.SupportsDesktopAcrylic} " +
                    $"controllerApplied={controllerApplied} legacyAccent={LegacyAccentBackdropActive} " +
                    $"tintBackdropActive={_solidColorBackdrop is not null && ReferenceEquals(SystemBackdrop, _solidColorBackdrop)} " +
                    $"opacity={surfaceOpacity:F3} intensity={SettingsService.Settings.WidgetMaterialIntensity:F3}");
            }

            _lastAppliedBackdropSignature = signature;
            ScheduleInactiveBackdropControllerCleanup(materialType);
        }
        catch (Exception ex)
        {
            App.Log($"ApplyBackdropPreference fallback: {ex}");
            _lastAppliedBackdropSignature = null;
            ClearSolidColorBackdrop();
            DisposeAcrylicController();
            DisposeMicaController();
            if (materialType == SettingsService.WidgetMaterialTypeSolid)
            {
                int backdropType = Win32Helper.DWMSBT_NONE;
                Win32Helper.TrySetDwmWindowAttribute(HWnd, Win32Helper.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType);
                Win32Helper.DisableAccentPolicy(HWnd);
                LegacyAccentBackdropActive = false;
            }
            else if (materialType == SettingsService.WidgetMaterialTypeNone)
            {
                // Even the failure path must not leave the theme's window fill
                // showing: a black tile is worse than a plain transparent one.
                try
                {
                    ApplyTransparentTintBackdrop();
                    int backdropType = Win32Helper.DWMSBT_NONE;
                    Win32Helper.TrySetDwmWindowAttribute(HWnd, Win32Helper.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType);
                    Win32Helper.DisableAccentPolicy(HWnd);
                    LegacyAccentBackdropActive = false;
                }
                catch (Exception fallbackEx)
                {
                    App.Log($"[Backdrop] Transparent fallback failed: {fallbackEx.Message}");
                }
            }
            else
            {
                double fallbackOpacity = WindowsCompatibilityService.UsesLegacyWindowAcrylic
                    ? WidgetMaterialVisualCalculator.CalculateLegacyAcrylicOpacity(
                        materialType == SettingsService.WidgetMaterialTypeAcrylicBase,
                        surfaceOpacity,
                        SettingsService.Settings.WidgetMaterialIntensity)
                    : Math.Min(surfaceOpacity, 0.52);
                LegacyAccentBackdropActive = ApplyLegacyAccentBackdrop(
                    tintColor,
                    fallbackOpacity);
            }
        }

        ApplySurfaceStyle();
    }

    private bool ApplyLegacyAccentBackdrop(Windows.UI.Color tintColor, double opacity)
    {
        _lastLegacyAccentTintColor = tintColor;
        _lastLegacyAccentOpacity = opacity;
        // A full preference application always restores the blurred accent
        // policy, so any interaction-time simplification ends here.
        _isInteractionBackdropDowngraded = false;
        return Win32Helper.ApplyAccentBlur(HWnd, tintColor, opacity, enabled: true);
    }

    /// <summary>
    /// Win10-only: temporarily switches the legacy acrylic accent policy to a
    /// tint without DWM blur while the window is being moved/resized/animated,
    /// so DWM stops re-sampling the blur on every frame. Keeps the frosted
    /// glass by default — the simplification only runs after recent frame
    /// ticks kept missing their budget (or in ResourceSaver mode).
    /// </summary>
    public void SimplifyBackdropForInteraction()
    {
        if (_isInteractionBackdropDowngraded ||
            !LegacyAccentBackdropActive ||
            !WindowsCompatibilityService.UsesLegacyWindowAcrylic ||
            HWnd == IntPtr.Zero)
        {
            return;
        }

        if (!InteractionBackdropSimplificationPolicy.ShouldSimplify(
                WidgetCompactAnimationCoordinator.RecentFrameOverrunMask,
                SettingsService.Settings.PerformanceMode))
        {
            return;
        }

        bool applied = Win32Helper.ApplyAccentBlur(
            HWnd,
            _lastLegacyAccentTintColor,
            _lastLegacyAccentOpacity,
            enabled: true,
            blurEnabled: false);
        if (!applied)
        {
            return;
        }

        _isInteractionBackdropDowngraded = true;
        App.LogVerbose(
            $"[Backdrop.Win10] hwnd=0x{HWnd.ToInt64():X} interaction tint-only accent applied " +
            $"(recent overrun bits={System.Numerics.BitOperations.PopCount((ulong)WidgetCompactAnimationCoordinator.RecentFrameOverrunMask)})");
    }

    /// <summary>
    /// Restores the full blurred legacy acrylic after an interaction ends.
    /// Deliberately does not go through the signature reuse check — the
    /// signature never changed, so ApplyBackdropPreference would short-circuit
    /// and leave the tint-only policy in place.
    /// </summary>
    public void RestoreBackdropAfterInteraction()
    {
        if (!_isInteractionBackdropDowngraded)
        {
            return;
        }

        _isInteractionBackdropDowngraded = false;
        if (!LegacyAccentBackdropActive || HWnd == IntPtr.Zero)
        {
            return;
        }

        Win32Helper.ApplyAccentBlur(
            HWnd,
            _lastLegacyAccentTintColor,
            _lastLegacyAccentOpacity,
            enabled: true,
            blurEnabled: true);
    }

    private bool CanReuseAppliedBackdrop(BackdropSignature signature)
    {        if (_lastAppliedBackdropSignature != signature)
        {
            return false;
        }

        return SettingsService.IsMicaMaterial(signature.MaterialType)
            ? MicaController is not null && MicaControllerAttached
            : SettingsService.IsAcrylicMaterial(signature.MaterialType)
                ? (AcrylicController is { IsClosed: false } && AcrylicControllerAttached) ||
                  LegacyAccentBackdropActive
                : signature.MaterialType == SettingsService.WidgetMaterialTypeNone
                    ? !IsSolidColorBackdropActive &&
                      _solidColorBackdrop is not null &&
                      ReferenceEquals(SystemBackdrop, _solidColorBackdrop) &&
                      AcrylicController is null &&
                      MicaController is null &&
                      !LegacyAccentBackdropActive
                    : signature.MaterialType == SettingsService.WidgetMaterialTypeSolid &&
                      IsSolidColorBackdropActive &&
                      _solidColorBackdrop is not null &&
                      ReferenceEquals(SystemBackdrop, _solidColorBackdrop);
    }

    protected virtual Windows.UI.Color BuildSolidColorBackdropTintColor(
        bool isDark,
        double surfaceOpacity)
    {
        Windows.UI.Color tintColor = BuildNativeBackdropTintColor(isDark);
        return Windows.UI.Color.FromArgb(
            (byte)Math.Clamp(Math.Round(surfaceOpacity * 255), 0, 255),
            tintColor.R,
            tintColor.G,
            tintColor.B);
    }

    private bool ApplySolidColorBackdrop(Windows.UI.Color tintColor)
    {
        DetachAcrylicControllerTarget();
        DetachMicaControllerTarget();

        if (_solidColorBackdrop is null)
        {
            _solidColorBackdrop = new WinUIEx.TransparentTintBackdrop(tintColor);
            SystemBackdrop = _solidColorBackdrop;
        }
        else
        {
            _solidColorBackdrop.TintColor = tintColor;
            if (!ReferenceEquals(SystemBackdrop, _solidColorBackdrop))
            {
                SystemBackdrop = _solidColorBackdrop;
            }
        }

        IsSolidColorBackdropActive = true;
        return true;
    }

    protected void ClearSolidColorBackdrop()
    {
        IsSolidColorBackdropActive = false;
        if (_solidColorBackdrop is null)
        {
            return;
        }

        if (ReferenceEquals(SystemBackdrop, _solidColorBackdrop))
        {
            SystemBackdrop = null;
        }

        _solidColorBackdrop = null;
    }

    /// <summary>
    /// Backdrop for the fully transparent surface.
    ///
    /// Clearing <c>SystemBackdrop</c> is not "nothing": the content island then
    /// falls back to the theme's window fill and the widget turns into a solid
    /// black tile (the exact field report for the transparent clock on Win10).
    /// A tint backdrop whose colour has zero alpha is what actually renders as
    /// "the desktop, untouched" at the composition layer.
    /// </summary>
    private void ApplyTransparentTintBackdrop()
    {
        IsSolidColorBackdropActive = false;
        if (_solidColorBackdrop is null)
        {
            _solidColorBackdrop = new WinUIEx.TransparentTintBackdrop(Colors.Transparent);
            SystemBackdrop = _solidColorBackdrop;
            return;
        }

        // Same instance as the Solid material: switching between them only
        // re-tints one brush instead of re-attaching a backdrop target.
        _solidColorBackdrop.TintColor = Colors.Transparent;
        if (!ReferenceEquals(SystemBackdrop, _solidColorBackdrop))
        {
            SystemBackdrop = _solidColorBackdrop;
        }
    }

    /// <summary>
    /// Drops the shared tint backdrop before a material that brings its own
    /// controller takes over. The Solid path re-tints the same instance itself,
    /// so only the transparent owner detaches it here.
    /// </summary>
    private void ClearTransparentBackdrop()
    {
        if (IsSolidColorBackdropActive)
        {
            return;
        }

        ClearSolidColorBackdrop();
    }

    protected static SolidColorBrush GetOrUpdateSolidColorBrush(Brush? current, Windows.UI.Color color)
    {
        if (current is SolidColorBrush brush && MutableBrushes.TryGetValue(brush, out _))
        {
            try
            {
                if (!brush.Color.Equals(color))
                {
                    brush.Color = color;
                }

                return brush;
            }
            catch (Exception)
            {
                MutableBrushes.Remove(brush);
            }
        }

        var replacement = new SolidColorBrush(color);
        MutableBrushes.Add(replacement, MutableBrushMarker);
        return replacement;
    }

    protected (double Thickness, Windows.UI.Color BorderColor, Windows.UI.Color DividerColor)
        GetWidgetBorderVisuals(bool isDark, Windows.UI.Color accentColor)
    {
        // The per-widget border override resolves before the calculator:
        // FollowGlobal drops the border for widgets with any image background
        // (their own or the global unified/panorama one), and an explicit
        // accent/neutral/none choice always wins.
        (string style, string colorMode) = WidgetBorderCustomization.ApplyOverride(
            WidgetBorderCustomization.GetModeOverride(Config),
            SettingsService.Settings.WidgetBorderStyle,
            SettingsService.Settings.WidgetBorderColorMode,
            WidgetBackgroundCustomization.HasCustomBackground(Config) ||
            WidgetBackgroundCustomization.IsGlobalImageBackgroundActive(
                SettingsService.Settings));
        WidgetBorderVisuals visuals = WidgetBorderVisualCalculator.Resolve(
            style,
            colorMode,
            isDark,
            accentColor);
        if (SuppressSurfaceChrome && !WindowsCompatibilityService.IsHighContrast)
        {
            // A transparent surface carries no frame and no divider — either one
            // would draw a rectangle around nothing. High contrast is exempt so
            // the window boundary stays findable for accessibility tooling.
            return (0, visuals.BorderColor, Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }

        return (
            visuals.Thickness,
            visuals.BorderColor,
            visuals.DividerColor);
    }

    private void ApplyCompactBorderVisuals(bool? isDarkOverride = null)
    {
        bool isDark = isDarkOverride ?? RootElement.ActualTheme == ElementTheme.Dark;
        var accentColor = App.Current.ThemeService?.GetEffectiveAccentColor()
            ?? AccentColorHelper.DefaultAccentColor;
        var (borderThickness, borderColor, _) = GetWidgetBorderVisuals(isDark, accentColor);

        try
        {
            if (WindowsCompatibilityService.IsHighContrast)
            {
                borderThickness = Math.Max(1, borderThickness);
                if (WindowsCompatibilityService.TryGetUiColor(
                        Windows.UI.ViewManagement.UIColorType.Foreground) is
                    { } highContrastForeground)
                {
                    borderColor = highContrastForeground;
                }
            }
        }
        catch
        {
            // Keep the configured border when accessibility APIs are unavailable.
        }

        var surface = WidgetShellControl.BackgroundSurface;
        surface.BorderThickness = new Thickness(borderThickness);
        surface.BorderBrush = GetOrUpdateSolidColorBrush(surface.BorderBrush, borderColor);
    }

    protected void ScheduleInactiveBackdropControllerCleanup(string materialType)
    {
        bool hasInactiveController = materialType switch
        {
            SettingsService.WidgetMaterialTypeMica or SettingsService.WidgetMaterialTypeMicaAlt =>
                AcrylicController is not null,
            SettingsService.WidgetMaterialTypeAcrylic or SettingsService.WidgetMaterialTypeAcrylicBase =>
                MicaController is not null,
            _ => AcrylicController is not null || MicaController is not null
        };

        if (!hasInactiveController)
        {
            _inactiveBackdropCleanupTimer?.Stop();
            return;
        }

        if (_inactiveBackdropCleanupTimer is null)
        {
            _inactiveBackdropCleanupTimer = DispatcherQueue.CreateTimer();
            _inactiveBackdropCleanupTimer.IsRepeating = false;
            _inactiveBackdropCleanupTimer.Tick += InactiveBackdropCleanupTimer_Tick;
            PerformanceLogger.RecordTransientUiTimerCreated();
        }

        _inactiveBackdropCleanupTimer.Stop();
        _inactiveBackdropCleanupTimer.Interval = InactiveBackdropControllerRetention;
        _inactiveBackdropCleanupTimer.Start();
    }

    protected void StopInactiveBackdropCleanupTimer()
    {
        DispatcherQueueTimer? timer = _inactiveBackdropCleanupTimer;
        if (timer is null)
        {
            return;
        }

        _inactiveBackdropCleanupTimer = null;
        timer.Stop();
        timer.Tick -= InactiveBackdropCleanupTimer_Tick;
        PerformanceLogger.RecordTransientUiTimerReleased();
    }

    private void InactiveBackdropCleanupTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        // Controllers of the inactive material family stay alive but detached.
        // Disposing them here used to force a full controller recreation on the
        // next material switch, which leaks native composition memory and DWM
        // handles per switch; CleanupBase releases both on window teardown.
        sender.Stop();
    }

    protected bool ApplyMicaController(
        bool isDark,
        Windows.UI.Color tintColor,
        bool useAlt)
    {
        if (!MicaController.IsSupported())
        {
            DisposeMicaController();
            return false;
        }

        BackdropTarget ??= this.As<ICompositionSupportsSystemBackdrop>();
        BackdropConfiguration ??= new SystemBackdropConfiguration();
        BackdropConfiguration.IsInputActive = true;
        BackdropConfiguration.Theme = isDark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light;

        // Kind is a mutable property: switching Mica/BaseAlt must reuse the
        // controller. Recreating system backdrop controllers on every material
        // change leaks native composition memory and DWM handles that no GC or
        // working-set trim reclaims; detached controllers stay idle instead.
        if (MicaController is null)
        {
            DetachAcrylicControllerTarget();
            MicaController = new MicaController
            {
                Kind = useAlt ? MicaKind.BaseAlt : MicaKind.Base
            };
        }

        DetachAcrylicControllerTarget();
        if (!MicaControllerAttached)
        {
            if (!MicaController.AddSystemBackdropTarget(BackdropTarget))
            {
                DisposeMicaController();
                return false;
            }

            MicaControllerAttached = true;
            MicaController.SetSystemBackdropConfiguration(BackdropConfiguration);
        }

        MicaController.Kind = useAlt ? MicaKind.BaseAlt : MicaKind.Base;
        MicaController.TintColor = tintColor;
        MicaController.FallbackColor = WidgetMaterialVisualCalculator.BuildMicaFallbackColor(
            isDark,
            useAlt);

        WidgetMaterialOpacityProfile profile = WidgetMaterialVisualCalculator.CalculateMica(
            isDark,
            useAlt,
            SettingsService.Settings.WidgetMaterialIntensity);
        MicaController.TintOpacity = (float)profile.TintOpacity;
        MicaController.LuminosityOpacity = (float)profile.LuminosityOpacity;
        return true;
    }

    protected void DisposeMicaController()
    {
        _lastAppliedBackdropSignature = null;
        if (MicaController is null)
        {
            return;
        }

        try
        {
            MicaController.RemoveAllSystemBackdropTargets();
            MicaController.Dispose();
        }
        catch
        {
        }
        finally
        {
            MicaController = null;
            MicaControllerAttached = false;
        }
    }

    protected void DetachMicaControllerTarget()
    {
        if (MicaController is null || !MicaControllerAttached)
        {
            return;
        }

        try
        {
            MicaController.RemoveAllSystemBackdropTargets();
        }
        catch
        {
        }
        finally
        {
            MicaControllerAttached = false;
        }
    }

    protected bool ApplyAcrylicController(
        bool isDark,
        Windows.UI.Color tintColor,
        double surfaceOpacity,
        bool useBase)
    {
        if (!WindowsCompatibilityService.SupportsDesktopAcrylic)
        {
            DisposeAcrylicController();
            return false;
        }

        BackdropTarget ??= this.As<ICompositionSupportsSystemBackdrop>();
        BackdropConfiguration ??= new SystemBackdropConfiguration();
        BackdropConfiguration.IsInputActive = true;
        BackdropConfiguration.Theme = isDark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light;
        BackdropConfiguration.HighContrastBackgroundColor = isDark
            ? ColorHelper.FromArgb(0xFF, 0x20, 0x20, 0x20)
            : ColorHelper.FromArgb(0xFF, 0xF3, 0xF3, 0xF3);

        if (AcrylicController is null || AcrylicController.IsClosed)
        {
            DetachMicaControllerTarget();
            AcrylicController = new DesktopAcrylicController
            {
                Kind = useBase ? DesktopAcrylicKind.Base : DesktopAcrylicKind.Thin
            };
        }

        DetachMicaControllerTarget();
        if (!AcrylicControllerAttached)
        {
            if (!AcrylicController.AddSystemBackdropTarget(BackdropTarget))
            {
                DisposeAcrylicController();
                return false;
            }

            AcrylicControllerAttached = true;
            AcrylicController.SetSystemBackdropConfiguration(BackdropConfiguration);
        }

        AcrylicController.Kind = useBase ? DesktopAcrylicKind.Base : DesktopAcrylicKind.Thin;
        AcrylicController.TintColor = tintColor;
        AcrylicController.FallbackColor = tintColor;

        WidgetMaterialOpacityProfile profile = WidgetMaterialVisualCalculator.CalculateAcrylic(
            isDark,
            useBase,
            surfaceOpacity,
            SettingsService.Settings.WidgetMaterialIntensity);
        AcrylicController.TintOpacity = (float)profile.TintOpacity;
        AcrylicController.LuminosityOpacity = (float)profile.LuminosityOpacity;
        return true;
    }

    protected void DisposeAcrylicController()
    {
        _lastAppliedBackdropSignature = null;
        if (AcrylicController is null)
        {
            return;
        }

        try
        {
            AcrylicController.RemoveAllSystemBackdropTargets();
            AcrylicController.Dispose();
        }
        catch
        {
        }
        finally
        {
            AcrylicController = null;
            AcrylicControllerAttached = false;
        }
    }

    protected void DetachAcrylicControllerTarget()
    {
        if (AcrylicController is null || !AcrylicControllerAttached)
        {
            return;
        }

        try
        {
            AcrylicController.RemoveAllSystemBackdropTargets();
        }
        catch
        {
        }
        finally
        {
            AcrylicControllerAttached = false;
        }
    }

    // ── Backdrop refresh timer ─────────────────────────────────

    protected void QueueBackdropRefresh()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(QueueBackdropRefresh);
            return;
        }

        ++BackdropRefreshGeneration;
        _backdropRefreshStage = 0;

        if (_backdropRefreshTimer is null)
        {
            _backdropRefreshTimer = DispatcherQueue.CreateTimer();
            _backdropRefreshTimer.Tick += BackdropRefreshTimer_Tick;
            PerformanceLogger.RecordTransientUiTimerCreated();
        }
        else
        {
            _backdropRefreshTimer.Stop();
        }

        _backdropRefreshTimer.Interval = TimeSpan.FromMilliseconds(BackdropRefreshDelays[0]);
        _backdropRefreshTimer.Start();
    }

    private void BackdropRefreshTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        OnBackdropRefreshTick(BackdropRefreshGeneration);
    }

    private void OnBackdropRefreshTick(long generation)
    {
        if (generation != BackdropRefreshGeneration)
        {
            _backdropRefreshTimer?.Stop();
            return;
        }

        RefreshBackdropIfCurrent(generation);

        int nextStage = _backdropRefreshStage + 1;
        _backdropRefreshStage = nextStage;

        if (nextStage < BackdropRefreshDelays.Length)
        {
            _backdropRefreshTimer!.Interval = TimeSpan.FromMilliseconds(BackdropRefreshDelays[nextStage]);
        }
        else
        {
            _backdropRefreshTimer!.Stop();
        }
    }

    private void RefreshBackdropIfCurrent(long generation)
    {
        if (generation != BackdropRefreshGeneration)
        {
            return;
        }

        if (!Visible || IsHideAnimationRunning)
        {
            return;
        }

        // Skip backdrop refresh during drag/resize — the window is moving
        // and the backdrop will be refreshed once when the operation ends.
        if (IsDragging || IsResizing)
        {
            return;
        }

        ApplyBackdropPreference();
    }

    protected void StopBackdropRefreshTimer()
    {
        DispatcherQueueTimer? timer = _backdropRefreshTimer;
        if (timer is null)
        {
            return;
        }

        _backdropRefreshTimer = null;
        timer.Stop();
        timer.Tick -= BackdropRefreshTimer_Tick;
        PerformanceLogger.RecordTransientUiTimerReleased();
    }

    // ── Layer / Z-order management ─────────────────────────────
}
