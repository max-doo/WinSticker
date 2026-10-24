using System.Globalization;
using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class GlanceWorldClockPolicyTests
{
    [Fact]
    public void FormatCityTime_Hour24UsesTwoDigitHour()
    {
        DateTimeOffset utcNow = new(2026, 10, 24, 16, 5, 0, TimeSpan.Zero);

        string text = GlanceWorldClockPolicy.FormatCityTime(
            utcNow,
            TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"),
            GlanceTimeFormatMode.Hour24,
            CultureInfo.InvariantCulture);

        Assert.Equal("00:05", text);
    }

    [Fact]
    public void FormatCityTime_Hour12KeepsDesignator()
    {
        DateTimeOffset utcNow = new(2026, 10, 24, 4, 5, 0, TimeSpan.Zero);

        string text = GlanceWorldClockPolicy.FormatCityTime(
            utcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"),
            GlanceTimeFormatMode.Hour12,
            CultureInfo.InvariantCulture);

        Assert.Equal("9:05 PM", text);
    }

    [Fact]
    public void FormatCityTime_FollowSystemUsesSystemPattern()
    {
        DateTimeOffset utcNow = new(2026, 10, 24, 4, 5, 0, TimeSpan.Zero);
        CultureInfo systemCulture = CultureInfo.GetCultureInfo("en-US");

        string text = GlanceWorldClockPolicy.FormatCityTime(
            utcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"),
            GlanceTimeFormatMode.FollowSystem,
            systemCulture);

        Assert.Equal("9:05 PM", text);
    }

    [Fact]
    public void ResolveCityName_FallsBackToZoneIdWhenNameMissing()
    {
        var clock = new GlanceWorldClockData("China Standard Time", "  ");

        string name = GlanceWorldClockPolicy.ResolveCityName(clock);

        Assert.Equal("China Standard Time", name);
    }

    [Fact]
    public void ResolveCityName_TrimsProvidedName()
    {
        var clock = new GlanceWorldClockData("China Standard Time", " 上海 ");

        string name = GlanceWorldClockPolicy.ResolveCityName(clock);

        Assert.Equal("上海", name);
    }

    [Fact]
    public void AddWorldClock_AcceptsValidZoneAndTrimsName()
    {
        var settings = new GlanceWidgetData();

        GlanceWorldClockPolicy.AddWorldClock(settings, " China Standard Time ", "上海");

        GlanceWorldClockData added = Assert.Single(settings.WorldClocks);
        Assert.Equal("China Standard Time", added.TimeZoneId);
        Assert.Equal("上海", added.CityName);
    }

    [Fact]
    public void AddWorldClock_RejectsUnknownZone()
    {
        var settings = new GlanceWidgetData();

        GlanceWorldClockPolicy.AddWorldClock(settings, "Mars/Standard_Time", "火星");

        Assert.Empty(settings.WorldClocks);
    }

    [Fact]
    public void AddWorldClock_IgnoresDuplicateZone()
    {
        var settings = new GlanceWidgetData();
        GlanceWorldClockPolicy.AddWorldClock(settings, "China Standard Time", "上海");

        GlanceWorldClockPolicy.AddWorldClock(settings, "china standard time", "北京");

        GlanceWorldClockData added = Assert.Single(settings.WorldClocks);
        Assert.Equal("上海", added.CityName);
    }

    [Fact]
    public void AddWorldClock_CapsEntryCount()
    {
        var settings = new GlanceWidgetData();
        string[] zoneIds =
        [
            "China Standard Time",
            "Pacific Standard Time",
            "UTC",
            "GMT Standard Time",
            "W. Europe Standard Time",
            "India Standard Time"
        ];
        foreach (string zoneId in zoneIds)
        {
            GlanceWorldClockPolicy.AddWorldClock(settings, zoneId, zoneId);
        }

        Assert.Equal(GlanceWorldClockPolicy.MaximumWorldClockCount, settings.WorldClocks.Count);
        Assert.Equal("China Standard Time", settings.WorldClocks[0].TimeZoneId);
        Assert.Equal("W. Europe Standard Time", settings.WorldClocks[^1].TimeZoneId);
    }

    [Fact]
    public void RemoveWorldClock_RemovesMatchingZoneIgnoringCase()
    {
        var settings = new GlanceWidgetData();
        GlanceWorldClockPolicy.AddWorldClock(settings, "China Standard Time", "上海");
        GlanceWorldClockPolicy.AddWorldClock(settings, "UTC", "伦敦");

        GlanceWorldClockPolicy.RemoveWorldClock(settings, "china standard time");

        GlanceWorldClockData remaining = Assert.Single(settings.WorldClocks);
        Assert.Equal("UTC", remaining.TimeZoneId);
    }

    [Fact]
    public void SetSizeScale_ClampsToSupportedRange()
    {
        var settings = new GlanceWidgetData();

        GlanceWorldClockPolicy.SetSizeScale(settings, 9);
        Assert.Equal(1.75, settings.SizeScale);

        GlanceWorldClockPolicy.SetSizeScale(settings, 0.1);
        Assert.Equal(0.75, settings.SizeScale);

        GlanceWorldClockPolicy.SetSizeScale(settings, 1.25);
        Assert.Equal(1.25, settings.SizeScale);

        GlanceWorldClockPolicy.SetSizeScale(settings, double.NaN);
        Assert.Equal(1, settings.SizeScale);
    }
}
