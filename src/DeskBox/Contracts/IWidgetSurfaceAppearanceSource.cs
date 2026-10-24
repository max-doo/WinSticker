namespace DeskBox.Contracts;

/// <summary>
/// Optional contract for content that owns its host window's surface material
/// instead of following the global appearance setting.
///
/// The window chrome (backdrop controller, accent policy, border, divider) is
/// global by design: one appearance setting governs every widget kind. A
/// content surface that must look different — the Glance clock's transparent
/// style above all — overrides it per instance through this contract, so the
/// global setting keeps its meaning for every other widget.
/// </summary>
public interface IWidgetSurfaceAppearanceSource
{
    /// <summary>
    /// Material for the hosting window. <see langword="null"/> follows the
    /// global <c>WidgetMaterialType</c>.
    /// </summary>
    string? SurfaceMaterialTypeOverride { get; }

    /// <summary>
    /// Surface opacity for the hosting window. <see langword="null"/> follows
    /// the global <c>WidgetOpacity</c>. Only meaningful for materials that
    /// support opacity and for the acrylic path.
    /// </summary>
    double? SurfaceOpacityOverride { get; }

    /// <summary>
    /// True when the window must draw no border and no divider — the surface
    /// is fully transparent and chrome would betray it.
    /// </summary>
    bool SuppressSurfaceChrome { get; }

    /// <summary>
    /// Raised when any of the three overrides above changed and the host must
    /// re-apply its backdrop and surface style. Raised on the UI thread.
    /// </summary>
    event EventHandler? SurfaceAppearanceChanged;
}
