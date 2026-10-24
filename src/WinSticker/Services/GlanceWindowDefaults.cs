namespace WinSticker.Services;

/// <summary>
/// Logical window size of the Glance clock patch at SizeScale = 1. The scale
/// slider and manual resizing both anchor to these numbers; the minimums are
/// the slider's lower scale expressed in pixels so both sizing paths agree.
/// </summary>
internal static class GlanceWindowDefaults
{
    public const double BaseWidth = 280;
    public const double BaseHeight = 110;

    public const double MinimumWidth = BaseWidth * GlanceWorldClockPolicy.MinimumSizeScale;
    public const double MinimumHeight = BaseHeight * GlanceWorldClockPolicy.MinimumSizeScale;
}
