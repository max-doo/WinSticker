using WinSticker.Models;
using WinSticker.Platform;
using Microsoft.UI.Xaml;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace WinSticker.Services;

/// <summary>
/// What a transparent surface should do with its text, resolved against the
/// desktop wallpaper behind it.
/// </summary>
/// <param name="Foreground">Theme the text block scope must adopt.</param>
/// <param name="ShowPlate">
/// Whether the faint plate behind the text is required to reach a readable
/// contrast ratio.
/// </param>
/// <param name="IsKnown">
/// False when the wallpaper could not be sampled; the caller keeps the plate
/// on as the safe default.
/// </param>
internal readonly record struct GlanceTextContrastDecision(
    ElementTheme Foreground,
    bool ShowPlate,
    bool IsKnown)
{
    /// <summary>
    /// The fallback used before the first sample completes: light text plus a
    /// plate, which is the survivable pair on any wallpaper.
    /// </summary>
    public static GlanceTextContrastDecision Unknown { get; } =
        new(ElementTheme.Dark, true, false);
}

/// <summary>
/// Samples the desktop wallpaper's average relative luminance so a fully
/// transparent widget can pick a readable text colour instead of guessing.
///
/// The sample is a 16×16 decode of the wallpaper file, cached per file path and
/// last-write time: a wallpaper change is the only event that invalidates it,
/// so the widget pays for one decode per wallpaper, not per tick.
/// </summary>
internal static class DesktopWallpaperContrastProbe
{
    private const int SampleEdge = 16;

    /// <summary>
    /// WCAG AA for large text. The clock's primary time is well above the
    /// "large" threshold, so 3.0 would be defensible; 4.5 keeps the secondary
    /// date/timezone rows readable at 11–12 px as well.
    /// </summary>
    private const double MinimumContrastRatio = 4.5;

    private static readonly object Gate = new();
    private static string? _cachedPath;
    private static DateTime _cachedWriteUtc;
    private static double? _cachedLuminance;
    private static bool _hasCache;

    /// <summary>
    /// Resolves the contrast decision for a transparent surface.
    /// </summary>
    public static async Task<GlanceTextContrastDecision> ResolveAsync(
        GlanceTextContrastMode mode,
        bool showPlate)
    {
        if (mode == GlanceTextContrastMode.Off)
        {
            return new GlanceTextContrastDecision(ElementTheme.Default, false, false);
        }

        if (mode == GlanceTextContrastMode.White)
        {
            return new GlanceTextContrastDecision(ElementTheme.Dark, showPlate, true);
        }

        if (mode == GlanceTextContrastMode.Black)
        {
            return new GlanceTextContrastDecision(ElementTheme.Light, showPlate, true);
        }

        double? luminance = await TryGetWallpaperLuminanceAsync().ConfigureAwait(false);
        if (luminance is not { } wallpaperLuminance)
        {
            // Sampling failed (solid-colour desktop, locked-down shell): light
            // text plus plate is the pair that survives the widest range.
            return new GlanceTextContrastDecision(ElementTheme.Dark, showPlate, false);
        }

        double whiteContrast = ContrastRatio(1.0, wallpaperLuminance);
        double blackContrast = ContrastRatio(wallpaperLuminance, 0.0);
        bool useLightText = whiteContrast >= blackContrast;
        double bestContrast = useLightText ? whiteContrast : blackContrast;
        bool needsPlate = showPlate && bestContrast < MinimumContrastRatio;
        return new GlanceTextContrastDecision(
            useLightText ? ElementTheme.Dark : ElementTheme.Light,
            needsPlate,
            true);
    }

    private static double ContrastRatio(double lighter, double darker)
    {
        double high = Math.Max(lighter, darker);
        double low = Math.Min(lighter, darker);
        return (high + 0.05) / (low + 0.05);
    }

    private static async Task<double?> TryGetWallpaperLuminanceAsync()
    {
        string? path = Win32Helper.TryGetDesktopWallpaperPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        DateTime writeUtc;
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            writeUtc = File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return null;
        }

        lock (Gate)
        {
            if (_hasCache &&
                string.Equals(_cachedPath, path, StringComparison.OrdinalIgnoreCase) &&
                _cachedWriteUtc == writeUtc)
            {
                return _cachedLuminance;
            }
        }

        double? luminance = await DecodeAverageLuminanceAsync(path).ConfigureAwait(false);
        lock (Gate)
        {
            _cachedPath = path;
            _cachedWriteUtc = writeUtc;
            _cachedLuminance = luminance;
            _hasCache = true;
        }

        return luminance;
    }

    private static async Task<double?> DecodeAverageLuminanceAsync(string path)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(path);
            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            var transform = new BitmapTransform
            {
                ScaledWidth = SampleEdge,
                ScaledHeight = SampleEdge,
                InterpolationMode = BitmapInterpolationMode.Fant
            };
            PixelDataProvider pixels = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);
            byte[] data = pixels.DetachPixelData();
            if (data.Length < 4)
            {
                return null;
            }

            double total = 0;
            int samples = 0;
            for (int index = 0; index + 3 < data.Length; index += 4)
            {
                // Bgra8: B, G, R, A. Relative luminance uses the linear-light
                // channels, so undo the sRGB transfer first — averaging gamma
                // bytes directly over-reports bright wallpapers.
                total += 0.2126 * ToLinear(data[index + 2]) +
                         0.7152 * ToLinear(data[index + 1]) +
                         0.0722 * ToLinear(data[index]);
                samples++;
            }

            return samples == 0 ? null : Math.Clamp(total / samples, 0.0, 1.0);
        }
        catch (Exception ex)
        {
            App.LogVerbose(
                $"[ContrastProbe] Wallpaper luminance unavailable ('{path}'): {ex.Message}");
            return null;
        }
    }

    private static double ToLinear(byte channel)
    {
        double value = channel / 255.0;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
