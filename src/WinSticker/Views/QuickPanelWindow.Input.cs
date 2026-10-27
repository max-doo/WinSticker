using System.Runtime.InteropServices;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using WinSticker.Platform;
using WinSticker.Services;
using Windows.System;

namespace WinSticker.Views;

public sealed partial class QuickPanelWindow
{
    private bool _preserveTargetFocus;
    private IntPtr _mouseHook;
    private IntPtr _keyboardHook;
    private Win32Helper.LowLevelMouseProc? _mouseHookProc;
    private Win32Helper.LowLevelKeyboardProc? _keyboardHookProc;
    private DispatcherQueueTimer? _foregroundTimer;
    private readonly HashSet<uint> _consumedKeys = [];
    private uint _openingHotkeyKey;

    private void SetPreserveTargetFocus(bool preserve)
    {
        _preserveTargetFocus = preserve;
        RootGrid.AllowFocusOnInteraction = !preserve;
        int style = Win32Helper.GetWindowLong(_hwnd, Win32Helper.GWL_EXSTYLE);
        style = preserve ? style | Win32Helper.WS_EX_NOACTIVATE : style & ~Win32Helper.WS_EX_NOACTIVATE;
        Win32Helper.SetWindowLongPtr(_hwnd, Win32Helper.GWL_EXSTYLE, (IntPtr)style);
    }

    private void ActivateForEditing()
    {
        SetPreserveTargetFocus(false);
        Activate();
        Win32Helper.SetForegroundWindow(_hwnd);
    }

    private void ClipboardView_KeyboardInputRequested(object? sender, EventArgs e) => ActivateForEditing();

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_preserveTargetFocus && e.OriginalSource is DependencyObject source &&
            TryFindAncestor<Microsoft.UI.Xaml.Controls.TextBox>(source, out _)) ActivateForEditing();
    }

    private void StartInputMonitoring()
    {
        StopInputMonitoring();
        // Scoped to the visible panel. Hooks only inspect input and enqueue work;
        // window operations must run after the native callback returns.
        _mouseHookProc ??= PanelMouseHook;
        _keyboardHookProc ??= PanelKeyboardHook;
        _mouseHook = Win32Helper.SetWindowsMouseHookEx(Win32Helper.WH_MOUSE_LL,
            _mouseHookProc, Win32Helper.GetModuleHandle(null), 0);
        _keyboardHook = Win32Helper.SetWindowsHookEx(Win32Helper.WH_KEYBOARD_LL,
            _keyboardHookProc, Win32Helper.GetModuleHandle(null), 0);
        int openingKey = _settingsService.Settings.Core.QuickPanelHotkeyKey;
        _openingHotkeyKey = Win32Helper.IsKeyDown(openingKey) ? (uint)openingKey : 0;
        if (_mouseHook == IntPtr.Zero || _keyboardHook == IntPtr.Zero)
            App.Log($"[QuickPanel] Input hook installation failed: {Marshal.GetLastWin32Error()}");
        _foregroundTimer ??= DispatcherQueue.CreateTimer();
        _foregroundTimer.Interval = TimeSpan.FromMilliseconds(100);
        _foregroundTimer.Tick -= ForegroundTimer_Tick;
        _foregroundTimer.Tick += ForegroundTimer_Tick;
        _foregroundTimer.Start();
    }

    private void StopInputMonitoring()
    {
        _foregroundTimer?.Stop();
        if (_mouseHook != IntPtr.Zero) Win32Helper.UnhookWindowsHookEx(_mouseHook);
        if (_keyboardHook != IntPtr.Zero) Win32Helper.UnhookWindowsHookEx(_keyboardHook);
        _mouseHook = _keyboardHook = IntPtr.Zero;
        _consumedKeys.Clear();
        _openingHotkeyKey = 0;
    }

    private void QueuePanelInput(Action action)
    {
        int version = _visibilityVersion;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isClosed && IsPanelVisible && version == _visibilityVersion) action();
        });
    }

    private IntPtr PanelMouseHook(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && IsPanelVisible && !_pasteInProgress &&
            (message == Win32Helper.WM_LBUTTONDOWN || message == Win32Helper.WM_RBUTTONDOWN ||
             message == Win32Helper.WM_MBUTTONDOWN || message == Win32Helper.WM_XBUTTONDOWN))
        {
            var sample = Marshal.PtrToStructure<Win32Helper.MSLLHOOKSTRUCT>(data);
            var desktop = new QuickPanelPasteDesktop(_hwnd);
            if (!desktop.IsPanelWindow(Win32Helper.WindowFromPoint(sample.pt)))
                QueuePanelInput(() => { if (!_viewModel.IsPinned) HidePanel(); });
        }
        return Win32Helper.CallNextHookEx(_mouseHook, code, message, data);
    }

    private IntPtr PanelKeyboardHook(int code, IntPtr message, IntPtr data)
    {
        if (code < 0) return Win32Helper.CallNextHookEx(_keyboardHook, code, message, data);
        var sample = Marshal.PtrToStructure<Win32Helper.KBDLLHOOKSTRUCT>(data);
        if ((sample.flags & Win32Helper.LLKHF_INJECTED) != 0)
            return Win32Helper.CallNextHookEx(_keyboardHook, code, message, data);
        bool down = message == Win32Helper.WM_KEYDOWN || message == Win32Helper.WM_SYSKEYDOWN;
        bool up = message == Win32Helper.WM_KEYUP || message == Win32Helper.WM_SYSKEYUP;
        // The key which opened the panel belongs to that invocation until
        // key-up, even if Alt is released before Space. Repeats must neither
        // dismiss the panel nor type spaces into the original editor.
        if (_openingHotkeyKey != 0 && sample.vkCode == _openingHotkeyKey)
        {
            if (up) _openingHotkeyKey = 0;
            if (down) return (IntPtr)1;
        }
        if (up && _consumedKeys.Remove(sample.vkCode)) return (IntPtr)1;
        if (!down || !_preserveTargetFocus || !IsPanelVisible || _viewModel.IsPinned || _pasteInProgress ||
            Win32Helper.GetForegroundWindow() != _lastActiveHwnd)
            return Win32Helper.CallNextHookEx(_keyboardHook, code, message, data);

        bool control = Win32Helper.IsKeyDown(0x11);
        bool shift = Win32Helper.IsKeyDown(0x10);
        bool otherModifier = Win32Helper.IsKeyDown(0x12) || Win32Helper.IsKeyDown(0x5B) || Win32Helper.IsKeyDown(0x5C);
        var key = (VirtualKey)sample.vkCode;
        var core = _settingsService.Settings.Core;
        int modifiers = (Win32Helper.IsKeyDown(0x12) ? 1 : 0) | (control ? 2 : 0) | (shift ? 4 : 0);
        if (core.QuickPanelHotkeyEnabled && core.QuickPanelHotkeyKey == sample.vkCode &&
            core.QuickPanelHotkeyModifiers == modifiers)
            return Win32Helper.CallNextHookEx(_keyboardHook, code, message, data);
        bool handled = !otherModifier && (key == VirtualKey.Escape || key == VirtualKey.Tab ||
            (control ? key == VirtualKey.F : key is VirtualKey.Up or VirtualKey.Down or VirtualKey.Home or VirtualKey.End or VirtualKey.Enter));
        if (handled)
        {
            _consumedKeys.Add(sample.vkCode);
            QueuePanelInput(() => HandleClipboardKey(key, control, shift));
            return (IntPtr)1;
        }
        // Other typing continues in the browser, dismissing a transient panel.
        bool modifierKey = sample.vkCode is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5;
        if (!otherModifier && !control && !modifierKey)
            QueuePanelInput(() => { if (!_viewModel.IsPinned) HidePanel(); });
        return Win32Helper.CallNextHookEx(_keyboardHook, code, message, data);
    }

    private void HandleClipboardKey(VirtualKey key, bool control, bool shift)
    {
        if (key == VirtualKey.Escape) HidePanel();
        else if (key == VirtualKey.Tab && control) _viewModel.CycleTab(shift);
        else if (key == VirtualKey.F && control) ClipboardView.OpenSearch();
        else if (key == VirtualKey.Tab)
        {
            ActivateForEditing();
            FocusManager.TryMoveFocus(shift ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next,
                new FindNextElementOptions { SearchRoot = RootGrid });
        }
        else ClipboardView.HandleNavigationKey(key);
    }

    private void ForegroundTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (!IsPanelVisible || _pasteInProgress) return;
        var desktop = new QuickPanelPasteDesktop(_hwnd);
        IntPtr foreground = desktop.ForegroundWindow;
        if (foreground == IntPtr.Zero || desktop.IsPanelWindow(foreground)) return;
        if (_viewModel.IsPinned) _lastActiveHwnd = foreground;
        else if (!_preserveTargetFocus || foreground != _lastActiveHwnd) HidePanel();
    }
}
