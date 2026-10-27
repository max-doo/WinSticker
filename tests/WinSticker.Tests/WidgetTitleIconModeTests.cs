using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class WidgetTitleIconModeTests
{
    [Fact]
    public void SearchWidget_UsesDedicatedSearchIconFamily()
    {
        Assert.Equal(WidgetTitleIconKindNames.Search, WidgetTitleIconKindNames.FromWidgetKind(WidgetKind.Search));
        Assert.Equal(WidgetTitleIconKindNames.Search, WidgetTitleIconKindNames.FromLegacyGlyph("\uE721"));
        Assert.Equal("search", WidgetTitleIconKindNames.GetColorAssetName(WidgetTitleIconKind.Search));
        Assert.Equal("WidgetTitleIcon.Label.Search", WidgetTitleIconKindNames.GetLocalizationKey(WidgetTitleIconKind.Search));
    }

    [Fact]
    public void TodoAndQuickCaptureColorIcons_UseDedicatedPalettesAndCanvas()
    {
        string todo = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Assets/WidgetTitleIcons/todo.svg"));
        string quickCapture = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Assets/WidgetTitleIcons/quick-capture.svg"));

        foreach (string icon in new[] { todo, quickCapture })
        {
            Assert.Contains("width=\"20\" height=\"20\" viewBox=\"0 0 20 20\"", icon, StringComparison.Ordinal);
        }

        // Todo uses Microsoft To Do inspired green ribbon checkmark palette
        Assert.Contains("stop-color=\"#059669\"", todo, StringComparison.Ordinal);
        Assert.Contains("stop-color=\"#34D399\"", todo, StringComparison.Ordinal);
        Assert.Contains("stop-color=\"#064E3B\"", todo, StringComparison.Ordinal);
        Assert.DoesNotContain("#38BDF8", todo, StringComparison.Ordinal);
        Assert.DoesNotContain("#2563EB", todo, StringComparison.Ordinal);

        // QuickCapture uses warm yellow Post-it sticky note palette
        Assert.Contains("stop-color=\"#FFEB60\"", quickCapture, StringComparison.Ordinal);
        Assert.Contains("stop-color=\"#FACC15\"", quickCapture, StringComparison.Ordinal);
        Assert.Contains("stop-color=\"#F59E0B\"", quickCapture, StringComparison.Ordinal);
        Assert.Contains("stop-color=\"#D97706\"", quickCapture, StringComparison.Ordinal);
        Assert.Contains("fill=\"#78350F\"", quickCapture, StringComparison.Ordinal);
        Assert.DoesNotContain("#38BDF8", quickCapture, StringComparison.Ordinal);
        Assert.DoesNotContain("#2563EB", quickCapture, StringComparison.Ordinal);
    }

    [Fact]
    public void GlanceColorIcon_UsesFluentCalendarColorAsset()
    {
        string glance = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Assets/WidgetTitleIcons/glance.svg"));

        Assert.Equal(WidgetTitleIconKindNames.Glance, WidgetTitleIconKindNames.FromWidgetKind(WidgetKind.Glance));
        Assert.Equal("glance", WidgetTitleIconKindNames.GetColorAssetName(WidgetTitleIconKind.Glance));
        Assert.Contains("width=\"20\" height=\"20\" viewBox=\"0 0 20 20\"", glance, StringComparison.Ordinal);
        Assert.Contains("ic_fluent_calendar_20_color__a", glance, StringComparison.Ordinal);
        Assert.Contains("#0094F0", glance, StringComparison.Ordinal);
        Assert.Contains("#2764E7", glance, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"sky\"", glance, StringComparison.Ordinal);
    }
}
