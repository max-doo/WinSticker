using WinSticker.Platform;

namespace WinSticker.Services;

public interface IQuickPanelPasteDesktop
{
    bool IsValidTarget(IntPtr target);
    IntPtr ForegroundWindow { get; }
    bool IsPanelWindow(IntPtr window);
    bool ActivateTarget(IntPtr target);
    bool ArePasteKeysReleased { get; }
    bool SendPaste();
}

/// <summary>Preserves an already focused editor and aborts when the user changes targets.</summary>
public sealed class QuickPanelPasteService(
    IQuickPanelPasteDesktop desktop,
    Func<TimeSpan, Task>? wait = null)
{
    private readonly Func<TimeSpan, Task> _wait = wait ?? Task.Delay;

    public async Task<bool> PrepareTargetAsync(IntPtr target)
    {
        if (!desktop.IsValidTarget(target)) return false;
        if (desktop.ForegroundWindow == target) return true;
        if (!desktop.IsPanelWindow(desktop.ForegroundWindow) || !desktop.ActivateTarget(target)) return false;

        for (int attempt = 0; attempt < 50; attempt++)
        {
            if (!desktop.IsValidTarget(target)) return false;
            IntPtr foreground = desktop.ForegroundWindow;
            if (foreground == target) return true;
            if (foreground != IntPtr.Zero && !desktop.IsPanelWindow(foreground)) return false;
            await _wait(TimeSpan.FromMilliseconds(10));
        }
        return false;
    }

    public async Task<bool> SendPasteAsync(IntPtr target, Func<bool> isCurrent)
    {
        // A held Alt/Shift/Win would turn Ctrl+V into a different command.
        // Wait for physical release rather than injecting modifier key-ups.
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (!isCurrent() || !desktop.IsValidTarget(target) || desktop.ForegroundWindow != target) return false;
            if (desktop.ArePasteKeysReleased) return desktop.SendPaste();
            await _wait(TimeSpan.FromMilliseconds(10));
        }
        return false;
    }
}

internal sealed class QuickPanelPasteDesktop(IntPtr panel) : IQuickPanelPasteDesktop
{
    public IntPtr ForegroundWindow => Win32Helper.GetForegroundWindow();
    public bool IsValidTarget(IntPtr target) => target != IntPtr.Zero &&
        !IsPanelWindow(target) && Win32Helper.IsWindow(target) && !Win32Helper.IsIconic(target);

    public bool IsPanelWindow(IntPtr window)
    {
        if (window == IntPtr.Zero) return false;
        IntPtr root = Win32Helper.GetAncestor(window, 2 /* GA_ROOT */);
        for (int depth = 0; root != IntPtr.Zero && depth < 8; depth++)
        {
            if (root == panel) return true;
            root = Win32Helper.GetWindow(root, Win32Helper.GW_OWNER);
        }
        return false;
    }

    public bool ActivateTarget(IntPtr target) => Win32Helper.SetForegroundWindow(target);
    public bool ArePasteKeysReleased =>
        !Win32Helper.IsKeyDown(0x10) && !Win32Helper.IsKeyDown(0x11) &&
        !Win32Helper.IsKeyDown(0x12) && !Win32Helper.IsKeyDown(0x5B) &&
        !Win32Helper.IsKeyDown(0x5C) && !Win32Helper.IsKeyDown(0x56);
    public bool SendPaste() => Win32Helper.TrySendKeyChord(Win32Helper.VK_CONTROL, 0x56);
}
