using System.Runtime.InteropServices;

namespace WinSticker.Tests;

public sealed class DesktopOwnerZOrderGuardTests
{
    private const uint OrderOnly = Win32Helper.SWP_NOMOVE |
        Win32Helper.SWP_NOSIZE | Win32Helper.SWP_NOACTIVATE;

    [Theory]
    [InlineData(OrderOnly, false, true)]
    [InlineData(OrderOnly, true, false)]
    [InlineData(OrderOnly | Win32Helper.SWP_NOZORDER, false, false)]
    [InlineData(OrderOnly | Win32Helper.SWP_SHOWWINDOW, false, false)]
    [InlineData(OrderOnly | 0x0080, false, false)]
    [InlineData(Win32Helper.SWP_NOMOVE | Win32Helper.SWP_NOSIZE, false, false)]
    [InlineData(Win32Helper.SWP_NOSIZE | Win32Helper.SWP_NOACTIVATE, false, false)]
    [InlineData(Win32Helper.SWP_NOMOVE | Win32Helper.SWP_NOACTIVATE, false, false)]
    public void Policy_OnlyPreservesUnrequestedOrderOnlyChanges(
        uint flags, bool applicationPositionChange, bool expected)
    {
        Assert.Equal(expected,
            DesktopOwnerZOrderGuard.ShouldPreserveOrder(flags, applicationPositionChange));
    }

    [Fact]
    public void NativeGuard_BlocksUnrequestedRaiseButAllowsExplicitSingleAndBatchOrdering()
    {
        // Hidden native windows exercise the real WINDOWPOS callback without
        // changing the user's foreground window or touching Explorer's HWNDs.
        IntPtr owner = CreateWindow(IntPtr.Zero);
        IntPtr widget = CreateWindow(owner);
        IntPtr coveringWindow = CreateWindow(IntPtr.Zero);
        var observedChanges = new List<(uint Before, uint After, bool ApplicationChange)>();
        UIntPtr observerId = new(0x54455354);
        Win32Helper.SubclassProc observer = (window, message, wParam, lParam, id, data) =>
        {
            const uint windowPositionChanging = 0x0046;
            int flagsOffset = IntPtr.Size * 2 + sizeof(int) * 4;
            uint before = message == windowPositionChanging
                ? unchecked((uint)Marshal.ReadInt32(lParam, flagsOffset)) : 0;
            bool applicationChange = Win32Helper.IsApplicationPositionChange;
            IntPtr result = Win32Helper.DefSubclassProc(window, message, wParam, lParam);
            if (message == windowPositionChanging)
            {
                observedChanges.Add((before,
                    unchecked((uint)Marshal.ReadInt32(lParam, flagsOffset)), applicationChange));
            }

            return result;
        };
        try
        {
            Assert.True(Win32Helper.SetWindowPos(widget, Win32Helper.HWND_BOTTOM,
                0, 0, 0, 0, OrderOnly));
            Assert.True(Win32Helper.SetWindowPos(coveringWindow, Win32Helper.HWND_TOP,
                0, 0, 0, 0, OrderOnly));
            Assert.True(DesktopOwnerZOrderGuard.Install(widget));
            Assert.True(Win32Helper.SetWindowSubclass(widget, observer, observerId, UIntPtr.Zero));

            Assert.True(SetWindowPos(widget, Win32Helper.HWND_TOP, 0, 0, 0, 0, OrderOnly));
            Assert.Contains(observedChanges, change => !change.ApplicationChange &&
                (change.Before & Win32Helper.SWP_NOZORDER) == 0 &&
                (change.After & Win32Helper.SWP_NOZORDER) != 0);

            observedChanges.Clear();
            Assert.True(Win32Helper.SetWindowPos(widget, Win32Helper.HWND_TOP,
                0, 0, 0, 0, OrderOnly));
            Assert.Contains(observedChanges, change => change.ApplicationChange &&
                (change.After & Win32Helper.SWP_NOZORDER) == 0);
            Assert.False(Win32Helper.IsApplicationPositionChange);

            Assert.True(Win32Helper.SetWindowPos(coveringWindow, Win32Helper.HWND_TOP,
                0, 0, 0, 0, OrderOnly));
            observedChanges.Clear();
            IntPtr batch = Win32Helper.BeginDeferWindowPos(1);
            Assert.NotEqual(IntPtr.Zero, batch);
            batch = Win32Helper.DeferWindowPos(batch, widget, Win32Helper.HWND_TOP,
                0, 0, 0, 0, OrderOnly);
            Assert.NotEqual(IntPtr.Zero, batch);
            Assert.True(Win32Helper.EndDeferWindowPos(batch));
            Assert.Contains(observedChanges, change => change.ApplicationChange &&
                (change.After & Win32Helper.SWP_NOZORDER) == 0);
            Assert.False(Win32Helper.IsApplicationPositionChange);

            DesktopOwnerZOrderGuard.Remove(widget);
            observedChanges.Clear();
            Assert.True(SetWindowPos(widget, coveringWindow, 0, 0, 0, 0, OrderOnly));
            Assert.Contains(observedChanges, change => !change.ApplicationChange &&
                (change.After & Win32Helper.SWP_NOZORDER) == 0);
        }
        finally
        {
            _ = Win32Helper.RemoveWindowSubclass(widget, observer, observerId);
            DesktopOwnerZOrderGuard.Remove(widget);
            DestroyWindow(widget);
            DestroyWindow(coveringWindow);
            DestroyWindow(owner);
        }
    }

    private static IntPtr CreateWindow(IntPtr owner)
    {
        IntPtr window = CreateWindowExW(0, "STATIC", null, unchecked((int)0x80000000),
            0, 0, 40, 40, owner, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, window);
        return window;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        int extendedStyle, string className, string? windowName, int style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu,
        IntPtr instance, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
