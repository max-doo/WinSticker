using WinSticker.Models;
using WinSticker.ViewModels;
using WinSticker.Views;

namespace WinSticker.Tests;

public sealed class CompactPresentationRefreshTests
{
    [Theory]
    [InlineData(nameof(WeatherWidgetViewModel.CurrentTemperatureText))]
    [InlineData(nameof(WeatherWidgetViewModel.CurrentDescription))]
    [InlineData(nameof(WeatherWidgetViewModel.PrecipitationText))]
    [InlineData(nameof(WeatherWidgetViewModel.CurrentWeatherCode))]
    [InlineData(nameof(WeatherWidgetViewModel.UsesRichSkin))]
    [InlineData(nameof(WeatherWidgetViewModel.RichSkinUsesLightText))]
    [InlineData(nameof(WeatherWidgetViewModel.RichBackdropTopColor))]
    [InlineData(nameof(WeatherWidgetViewModel.RichBackdropBottomColor))]
    public void WeatherCompactPresentation_RefreshesForDisplayedWeatherProperties(
        string propertyName)
    {
        Assert.True(ContentWidgetWindow.IsCompactPresentationPropertyRelevant(
            WidgetKind.Weather,
            propertyName));
    }

    [Theory]
    [InlineData(nameof(WeatherWidgetViewModel.LayoutMode))]
    [InlineData(nameof(WeatherWidgetViewModel.ExpandedSunriseVisibility))]
    [InlineData(nameof(WeatherWidgetViewModel.ExpandedHourlyPrecipVisibility))]
    [InlineData(nameof(WeatherWidgetViewModel.ExpandedHourlyCardHeight))]
    [InlineData(nameof(WeatherWidgetViewModel.ExpandedHourlyDividerVisibility))]
    public void WeatherCompactPresentation_IgnoresExpandedLayoutProperties(
        string propertyName)
    {
        Assert.False(ContentWidgetWindow.IsCompactPresentationPropertyRelevant(
            WidgetKind.Weather,
            propertyName));
    }

    [Theory]
    [InlineData(nameof(MusicWidgetViewModel.Title))]
    [InlineData(nameof(MusicWidgetViewModel.IsPlaying))]
    [InlineData(nameof(MusicWidgetViewModel.SeekValue))]
    public void MusicCompactPresentation_RefreshesForDisplayedMusicProperties(
        string propertyName)
    {
        Assert.True(ContentWidgetWindow.IsCompactPresentationPropertyRelevant(
            WidgetKind.Music,
            propertyName));
    }

    [Fact]
    public void MusicCompactPresentation_IgnoresExpandedOnlyPositionText()
    {
        Assert.False(ContentWidgetWindow.IsCompactPresentationPropertyRelevant(
            WidgetKind.Music,
            nameof(MusicWidgetViewModel.PositionText)));
    }

    [Theory]
    [InlineData(nameof(GlanceWidgetViewModel.TimeText))]
    [InlineData(nameof(GlanceWidgetViewModel.DateText))]
    [InlineData(nameof(GlanceWidgetViewModel.WeekdayText))]
    [InlineData(nameof(GlanceWidgetViewModel.ShowTime))]
    [InlineData(nameof(GlanceWidgetViewModel.ShowDate))]
    [InlineData(nameof(GlanceWidgetViewModel.ShowWeekday))]
    public void GlanceCompactPresentation_RefreshesForVisibleClockProperties(
        string propertyName)
    {
        Assert.True(ContentWidgetWindow.IsCompactPresentationPropertyRelevant(
            WidgetKind.Glance,
            propertyName));
    }

    [Fact]
    public void GlanceCompactPresentation_IgnoresExpandedOnlyWeatherBlock()
    {
        Assert.False(ContentWidgetWindow.IsCompactPresentationPropertyRelevant(
            WidgetKind.Glance,
            nameof(GlanceWidgetViewModel.ShowWeatherBlock)));
    }

    [Fact]
    public void UnknownPropertyNotification_AlwaysRefreshes()
    {
        Assert.True(ContentWidgetWindow.IsCompactPresentationPropertyRelevant(
            WidgetKind.Weather,
            propertyName: null));
    }
}
