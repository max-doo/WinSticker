namespace WinSticker.Services;

internal enum RelativeLayerRestoreDisposition
{
    DesktopBottom,
    PreservePeerOrder,
    BehindForeground,
}

internal static class RelativeLayerRestorePolicy
{
    public static bool ShouldAttachToDesktop(
        bool usesDesktopPinnedMode,
        bool keepVisibleOnShowDesktop)
    {
        return usesDesktopPinnedMode || keepVisibleOnShowDesktop;
    }

    public static RelativeLayerRestoreDisposition Decide(
        bool hasForeground,
        bool foregroundIsDesktopShell,
        bool foregroundIsSelf,
        bool foregroundIsWinSticker)
    {
        if (!hasForeground || foregroundIsDesktopShell)
        {
            return RelativeLayerRestoreDisposition.DesktopBottom;
        }

        if (foregroundIsSelf || foregroundIsWinSticker)
        {
            return RelativeLayerRestoreDisposition.PreservePeerOrder;
        }

        return RelativeLayerRestoreDisposition.BehindForeground;
    }
}
