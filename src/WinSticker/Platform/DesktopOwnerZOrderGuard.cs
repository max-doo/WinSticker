using System.Runtime.InteropServices;

namespace WinSticker.Platform;

/// <summary>
/// Keeps a desktop-owned window in its existing band when Explorer activates
/// the desktop. Explicit application positioning still owns widget ordering.
/// </summary>
internal static class DesktopOwnerZOrderGuard
{
    private const uint WindowPositionChanging = 0x0046;
    private const uint HideWindow = 0x0080;
    private static readonly UIntPtr SubclassId = new(0x57445A47);
    private static readonly Win32Helper.SubclassProc SubclassProcedure =
        CallbackDepth.WrapSubclassProc(WindowProcedure);

    internal static bool Install(IntPtr windowHandle) =>
        Win32Helper.SetWindowSubclass(
            windowHandle, SubclassProcedure, SubclassId, UIntPtr.Zero);

    internal static void Remove(IntPtr windowHandle) =>
        _ = Win32Helper.RemoveWindowSubclass(
            windowHandle, SubclassProcedure, SubclassId);

    internal static bool ShouldPreserveOrder(uint flags, bool applicationPositionChange)
    {
        const uint ownerReorder = Win32Helper.SWP_NOMOVE |
            Win32Helper.SWP_NOSIZE | Win32Helper.SWP_NOACTIVATE;
        const uint relevantFlags = ownerReorder | Win32Helper.SWP_NOZORDER |
            Win32Helper.SWP_SHOWWINDOW | HideWindow;
        return !applicationPositionChange && (flags & relevantFlags) == ownerReorder;
    }

    private static IntPtr WindowProcedure(
        IntPtr windowHandle, uint message, UIntPtr wParam, IntPtr lParam,
        UIntPtr subclassId, UIntPtr referenceData)
    {
        if (message == WindowPositionChanging && lParam != IntPtr.Zero)
        {
            WindowPosition position = Marshal.PtrToStructure<WindowPosition>(lParam);
            if (ShouldPreserveOrder(position.Flags, Win32Helper.IsApplicationPositionChange))
            {
                // Amend this transaction before Windows commits it. Do not
                // issue another SetWindowPos from inside the native callback.
                position.Flags |= Win32Helper.SWP_NOZORDER;
                Marshal.StructureToPtr(position, lParam, fDeleteOld: false);
            }
        }
        else if (message == Win32Helper.WM_NCDESTROY)
        {
            Remove(windowHandle);
        }

        return Win32Helper.DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPosition
    {
        public IntPtr WindowHandle;
        public IntPtr InsertAfter;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public uint Flags;
    }
}
