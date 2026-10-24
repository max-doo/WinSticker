namespace WinSticker.Tests;

public sealed class Windows10CornerCompatibilityTests
{
    [Fact]
    public void EffectiveCornerPolicy_IsUsedByWindowCapsuleAndPopupSurfaces()
    {
        string widgetBounds = Read("src/WinSticker/Views/WidgetWindowBase.Bounds.cs");
        string widgetCollapse = Read("src/WinSticker/Views/WidgetWindowBase.Collapse.cs");
        string searchPopup = Read("src/WinSticker/Views/SearchPopupWindow.xaml.cs");
        string stackPopover = Read(
            "src/WinSticker/Controls/WidgetContents/FileSurfaceContent.StackPopover.cs");

        Assert.Contains(
            "ResolveEffectiveWidgetCornerPreference",
            widgetBounds,
            StringComparison.Ordinal);
        Assert.Contains(
            "ResolveEffectiveWidgetCornerPreference",
            widgetCollapse,
            StringComparison.Ordinal);
        Assert.Contains(
            "ResolveEffectiveWidgetCompactMediaCornerMode",
            widgetCollapse,
            StringComparison.Ordinal);
        Assert.Contains(
            "ResolveEffectiveWidgetCornerPreference",
            searchPopup,
            StringComparison.Ordinal);
        Assert.Contains(
            "ResolveEffectiveWidgetCornerPreference",
            stackPopover,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EffectiveCornerPolicy_IsUsedByMusicWeatherAndGlanceInnerSurfaces()
    {
        string musicViewModel = Read("src/WinSticker/ViewModels/MusicWidgetViewModel.cs");
        string musicContent = Read(
            "src/WinSticker/Controls/WidgetContents/MusicWidgetContent.xaml.cs");
        string weatherViewModel = Read("src/WinSticker/ViewModels/WeatherWidgetViewModel.cs");
        string weatherContent = Read(
            "src/WinSticker/Controls/WidgetContents/WeatherWidgetContent.xaml");
        string weatherCodeBehind = Read(
            "src/WinSticker/Controls/WidgetContents/WeatherWidgetContent.xaml.cs");
        string weatherAot = Read(
            "src/WinSticker/ViewModels/WeatherViewModels.AotBindableProperties.cs");
        string glanceCodeBehind = Read(
            "src/WinSticker/Controls/WidgetContents/GlanceWidgetContent.xaml.cs");

        Assert.Contains(
            "ResolveEffectiveWidgetCornerPreference",
            musicViewModel,
            StringComparison.Ordinal);
        Assert.Contains("ApplyAlbumArtCornerRadius", musicContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Max(8, size * 0.12)", musicContent, StringComparison.Ordinal);
        Assert.Contains(
            "ResolveEffectiveWidgetCornerPreference",
            weatherViewModel,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CornerRadius=\"{Binding WidgetCornerRadius}\"",
            weatherContent,
            StringComparison.Ordinal);
        Assert.Contains(
            "ApplyRichSkinCornerRadius",
            weatherCodeBehind,
            StringComparison.Ordinal);
        Assert.Contains("RichBackdrop.CornerRadius", weatherCodeBehind, StringComparison.Ordinal);
        Assert.Contains("LoadingOverlay.CornerRadius", weatherCodeBehind, StringComparison.Ordinal);
        Assert.Contains("nameof(WidgetCornerRadius)", weatherAot, StringComparison.Ordinal);
        // The v12 clock patch carries its own contrast plate and matching
        // corner policy in the content code-behind.
        Assert.Contains(
            "ResolveEffectiveWidgetCornerPreference",
            glanceCodeBehind,
            StringComparison.Ordinal);
        Assert.Contains("ContrastPlate.CornerRadius", glanceCodeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void Win10Compatibility_PreservesPersistedRequestedCornerPreference()
    {
        string compatibility = Read(
            "src/WinSticker/Services/WindowsCompatibilityService.cs");
        string settings = Read("src/WinSticker/Services/SettingsService.cs");

        Assert.Contains(
            "ResolveEffectiveWidgetCornerPreferenceForBuild",
            compatibility,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ResolveEffectiveWidgetCornerPreference",
            settings,
            StringComparison.Ordinal);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(TestPaths.FromRepository(relativePath));
}
