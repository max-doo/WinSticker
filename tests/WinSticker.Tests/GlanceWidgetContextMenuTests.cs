using System.Text.Json;
using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class GlanceWidgetContextMenuTests
{
    [Fact]
    public void DisplayPolicy_KeepsYearCoupledToDate()
    {
        var settings = new GlanceWidgetData();

        GlanceWidgetSettingsPolicy.SetDisplayElement(settings, GlanceDisplayElement.Time, false);
        GlanceWidgetSettingsPolicy.SetDisplayElement(settings, GlanceDisplayElement.Year, true);
        Assert.True(settings.ShowDate);
        Assert.True(settings.ShowYear);
        GlanceWidgetSettingsPolicy.SetDisplayElement(settings, GlanceDisplayElement.Date, false);
        GlanceWidgetSettingsPolicy.SetDisplayElement(settings, GlanceDisplayElement.Weekday, false);
        GlanceWidgetSettingsPolicy.SetDisplayElement(settings, GlanceDisplayElement.Weather, false);

        Assert.False(settings.ShowTime);
        Assert.False(settings.ShowDate);
        Assert.False(settings.ShowYear);
        Assert.False(settings.ShowWeekday);
        Assert.False(settings.ShowWeather);
    }

    [Fact]
    public void ContextMenu_KeepsOnlyDisplayAndSurfaceBeforeSharedWidgetCommands()
    {
        string builder = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Services/GlanceWidgetContextMenuBuilder.cs"));
        string host = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Views/ContentWidgetWindow.Commands.cs"));

        Assert.Contains("new ToggleMenuFlyoutItem", builder, StringComparison.Ordinal);
        Assert.Contains("new RadioMenuFlyoutItem", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("new MenuFlyoutItem", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("Glance.Actions.Next", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("Glance.Actions.Pause", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("Glance.Actions.OpenSource", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("Glance.Actions.ShowInExplorer", builder, StringComparison.Ordinal);
        Assert.Contains("Glance.Display.Title", builder, StringComparison.Ordinal);
        Assert.Contains("Glance.Display.Seconds", builder, StringComparison.Ordinal);
        Assert.Contains("Glance.Surface.Title", builder, StringComparison.Ordinal);
        Assert.Contains("CurrentContent is GlanceWidgetContentAdapter", host, StringComparison.Ordinal);
        Assert.True(
            host.IndexOf("GlanceWidgetContextMenuBuilder.Append", StringComparison.Ordinal) <
            host.IndexOf("var rename = new MenuFlyoutItem", StringComparison.Ordinal));
    }

    [Fact]
    public void SettingsEntry_IsPresentInEveryLocale()
    {
        string stringsDirectory = TestPaths.FromRepository("src/WinSticker/Strings");
        foreach (string path in Directory.EnumerateFiles(stringsDirectory, "*.json"))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.True(document.RootElement.TryGetProperty("Widget.Settings.Glance", out _), path);
            Assert.True(document.RootElement.TryGetProperty("Glance.Display.Year", out _), path);
            Assert.True(document.RootElement.TryGetProperty("Glance.Display.Seconds", out _), path);
            Assert.True(document.RootElement.TryGetProperty("Glance.Size.Title", out _), path);
            Assert.True(document.RootElement.TryGetProperty("Glance.WorldClock.Title", out _), path);
            Assert.True(document.RootElement.TryGetProperty("Glance.WorldClock.Tomorrow", out _), path);
        }
    }

    [Fact]
    public void ChineseMenuLabels_DescribeSettingsAndVisualLayoutPrecisely()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
            TestPaths.FromRepository("src/WinSticker/Strings/zh-CN.json")));

        Assert.Equal(
            "时光设置",
            document.RootElement.GetProperty("Widget.Settings.Glance").GetString());
        Assert.Equal(
            "字体大小",
            document.RootElement.GetProperty("Glance.Typography.Title").GetString());
        Assert.Equal(
            "世界时钟",
            document.RootElement.GetProperty("Glance.WorldClock.Title").GetString());
        Assert.Equal(
            "贴片大小",
            document.RootElement.GetProperty("Glance.Size.Title").GetString());
    }

    [Fact]
    public void GlanceMoreFlyout_ExcludesIrrelevantCommandsForGlanceWidget()
    {
        string host = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Views/ContentWidgetWindow.Commands.cs"));

        int glanceIndex = host.IndexOf("if (CurrentContent is GlanceWidgetContentAdapter glance)", StringComparison.Ordinal);
        Assert.True(glanceIndex >= 0);

        int returnIndex = host.IndexOf("return flyout;", glanceIndex, StringComparison.Ordinal);
        Assert.True(returnIndex > glanceIndex);

        string glanceBlock = host[glanceIndex..returnIndex];
        Assert.Contains("GlanceWidgetContextMenuBuilder.Append", glanceBlock, StringComparison.Ordinal);
        Assert.Contains("WidgetLockMenuBuilder.Create", glanceBlock, StringComparison.Ordinal);
        Assert.Contains("WidgetSettingsMenuHelper.CreateMenuItem", glanceBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Common.Rename", glanceBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Widget.StyleMenu.Label", glanceBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("WidgetCollapseMenuBuilder.Create", glanceBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("WidgetGroupMenuBuilder.Append", glanceBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void GlanceWidgetContent_UsesFluidTypographyAndHitTestableBackground()
    {
        string xaml = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Controls/WidgetContents/GlanceWidgetContent.xaml"));
        string vm = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/ViewModels/GlanceWidgetViewModel.cs"));

        Assert.Contains("Background=\"Transparent\"", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding DateFontSize}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding WeatherTempFontSize}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding WeatherSubFontSize}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding WeatherIconSize}", xaml, StringComparison.Ordinal);

        Assert.Contains("public double DateFontSize", vm, StringComparison.Ordinal);
        Assert.Contains("public double WeatherTempFontSize", vm, StringComparison.Ordinal);
        Assert.Contains("public double WeatherSubFontSize", vm, StringComparison.Ordinal);
        Assert.Contains("public double WeatherIconSize", vm, StringComparison.Ordinal);
        Assert.Contains("IndexOfAny", vm, StringComparison.Ordinal);
    }
}
