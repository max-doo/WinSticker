using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinSticker.Helpers;
using WinSticker.Models;
using WinSticker.Platform;
using Windows.System;

namespace WinSticker.Services;

/// <summary>
/// Manages the global hotkey for invoking the Quick Panel (快捷弹窗).
/// Uses the standard Win32 RegisterHotKey + WM_HOTKEY mechanism.
/// </summary>
public sealed class QuickPanelHotkeyService : IDisposable
{
    private const int QuickPanelHotkeyId = 0x4446;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private static readonly UIntPtr SubclassId = new(0x4446);

    private readonly SettingsService _settingsService;
    private readonly Func<Task> _invokeAsync;
    private readonly Win32Helper.SubclassProc _subclassProc;
    private IntPtr _windowHandle;
    private bool _isSubclassInstalled;
    private bool _isRegistered;
    private bool _isInvoking;

    public QuickPanelHotkeyService(
        SettingsService settingsService,
        Func<Task> invokeAsync)
    {
        _settingsService = settingsService;
        _invokeAsync = invokeAsync;
        _subclassProc = CallbackDepth.WrapSubclassProc(WindowSubclassProc);
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    public bool IsRegistered => _isRegistered;

    public GlobalHotkeyGesture CurrentGesture => GlobalHotkeyService.NormalizeGesture(
        _settingsService.Settings.Core.QuickPanelHotkeyModifiers,
        _settingsService.Settings.Core.QuickPanelHotkeyKey);

    public void Attach(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) return;

        Detach();
        _windowHandle = windowHandle;
        RefreshRegistration();
    }

    public void Detach()
    {
        Unregister();
        RemoveSubclass();
        _windowHandle = IntPtr.Zero;
    }

    public void RefreshRegistration()
    {
        Unregister();

        if (_windowHandle == IntPtr.Zero || !_settingsService.Settings.Core.QuickPanelHotkeyEnabled)
        {
            RemoveSubclass();
            return;
        }

        if (!_isSubclassInstalled)
        {
            _isSubclassInstalled = Win32Helper.SetWindowSubclass(
                _windowHandle,
                _subclassProc,
                SubclassId,
                UIntPtr.Zero);
            if (!_isSubclassInstalled)
            {
                App.Log("[QuickPanelHotkey] Failed to install window subclass");
                return;
            }
        }

        var gesture = CurrentGesture;
        if (!GlobalHotkeyService.IsValidGesture(gesture))
        {
            return;
        }

        if (Register(_windowHandle, gesture))
        {
            _isRegistered = true;
            App.Log($"[QuickPanelHotkey] Registered gesture={FormatGesture(gesture)}");
            return;
        }

        App.Log($"[QuickPanelHotkey] Failed to register gesture={FormatGesture(gesture)} (may be in use)");
    }

    private void Unregister()
    {
        if (_isRegistered && _windowHandle != IntPtr.Zero)
        {
            Win32Helper.UnregisterHotKey(_windowHandle, QuickPanelHotkeyId);
        }

        _isRegistered = false;
    }

    private void RemoveSubclass()
    {
        if (_isSubclassInstalled && _windowHandle != IntPtr.Zero)
        {
            Win32Helper.RemoveWindowSubclass(_windowHandle, _subclassProc, SubclassId);
        }

        _isSubclassInstalled = false;
    }

    private static bool Register(IntPtr windowHandle, GlobalHotkeyGesture gesture)
    {
        return Win32Helper.RegisterHotKey(
            windowHandle,
            QuickPanelHotkeyId,
            ToWin32Modifiers(gesture.Modifiers) | ModNoRepeat,
            (uint)gesture.VirtualKey);
    }

    private static uint ToWin32Modifiers(HotkeyModifierKeys modifiers)
    {
        uint value = 0;
        if (modifiers.HasFlag(HotkeyModifierKeys.Alt)) value |= ModAlt;
        if (modifiers.HasFlag(HotkeyModifierKeys.Control)) value |= ModControl;
        if (modifiers.HasFlag(HotkeyModifierKeys.Shift)) value |= ModShift;
        return value;
    }

    private static string FormatGesture(GlobalHotkeyGesture gesture)
    {
        var parts = new List<string>();
        if (gesture.Modifiers.HasFlag(HotkeyModifierKeys.Control)) parts.Add("Ctrl");
        if (gesture.Modifiers.HasFlag(HotkeyModifierKeys.Alt)) parts.Add("Alt");
        if (gesture.Modifiers.HasFlag(HotkeyModifierKeys.Shift)) parts.Add("Shift");
        parts.Add($"VK:{gesture.VirtualKey:X2}");
        return string.Join("+", parts);
    }

    private IntPtr WindowSubclassProc(IntPtr hWnd, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (message == GlobalHotkeyService.WmHotkey &&
            wParam.ToUInt32() == QuickPanelHotkeyId)
        {
            // Keep the physical chord intact until the user releases it. A
            // synthetic Alt-up makes held Space repeats look like typing to
            // the non-activating panel and can immediately dismiss it.
            WinSticker.Platform.UiDispatch.PostAfterCallback(() => _ = InvokeAsync());
            return IntPtr.Zero;
        }

        return Win32Helper.DefSubclassProc(hWnd, message, wParam, lParam);
    }

    private async Task InvokeAsync()
    {
        if (_isInvoking) return;
        _isInvoking = true;
        try
        {
            await _invokeAsync();
        }
        catch (Exception ex)
        {
            App.Log($"[QuickPanelHotkey] Invocation failed: {ex.Message}");
        }
        finally
        {
            _isInvoking = false;
        }
    }

    public void Dispose()
    {
        _settingsService.SettingsChanged -= OnSettingsChanged;
        Detach();
    }

    private void OnSettingsChanged()
    {
        RefreshRegistration();
    }
}
