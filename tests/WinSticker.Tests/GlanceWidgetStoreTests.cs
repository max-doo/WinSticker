using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class GlanceWidgetStoreTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "WinSticker.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_UsesPhaseOneDefaults()
    {
        var store = new GlanceWidgetStore(_tempRoot);

        GlanceWidgetData data = await store.LoadAsync();

        Assert.Equal(GlanceWidgetData.CurrentVersion, data.Version);
        Assert.True(data.ShowTime);
        Assert.True(data.ShowDate);
        Assert.False(data.ShowYear);
        Assert.True(data.ShowWeekday);
        Assert.True(data.ShowWeather);
        Assert.False(data.ShowSeconds);
        Assert.Equal(GlanceTimeFormatMode.FollowSystem, data.TimeFormat);
        Assert.Equal(1, data.SizeScale);
        Assert.Empty(data.WorldClocks);
        Assert.Null(data.TimeFontFamily);
        Assert.Equal(GlanceSurfaceMaterialMode.FollowSystem, data.SurfaceMaterial);
        Assert.Equal(GlanceTextContrastMode.Auto, data.TextContrast);
    }

    [Fact]
    public async Task SaveAsync_PreservesYearOnlyWhenDateIsVisible()
    {
        var store = new GlanceWidgetStore(_tempRoot);
        await store.SaveAsync(new GlanceWidgetData
        {
            ShowDate = true,
            ShowYear = true
        });

        GlanceWidgetData withDate = await store.LoadAsync();
        Assert.True(withDate.ShowYear);

        withDate.ShowDate = false;
        await store.SaveAsync(withDate);

        GlanceWidgetData withoutDate = await store.LoadAsync();
        Assert.False(withoutDate.ShowDate);
        Assert.False(withoutDate.ShowYear);
    }

    [Fact]
    public async Task SaveAsync_NormalizesUnknownTimeFormat()
    {
        var store = new GlanceWidgetStore(_tempRoot);
        await store.SaveAsync(new GlanceWidgetData
        {
            TimeFormat = (GlanceTimeFormatMode)999
        });

        GlanceWidgetData reloaded = await store.LoadAsync();

        Assert.Equal(GlanceTimeFormatMode.FollowSystem, reloaded.TimeFormat);
    }

    [Fact]
    public async Task SaveAsync_ClampsSizeScaleAndDropsInvalidWorldClocks()
    {
        var store = new GlanceWidgetStore(_tempRoot);
        await store.SaveAsync(new GlanceWidgetData
        {
            SizeScale = 9,
            WorldClocks =
            [
                new GlanceWorldClockData("China Standard Time", "上海"),
                new GlanceWorldClockData("Mars/Standard_Time", "火星"),
                new GlanceWorldClockData(" china standard time ", "北京")
            ]
        });

        GlanceWidgetData reloaded = await new GlanceWidgetStore(_tempRoot).LoadAsync();

        Assert.Equal(1.75, reloaded.SizeScale);
        GlanceWorldClockData clock = Assert.Single(reloaded.WorldClocks);
        Assert.Equal("China Standard Time", clock.TimeZoneId);
        Assert.Equal("上海", clock.CityName);
    }

    [Fact]
    public async Task SaveAsync_TrimsWorldClockNames()
    {
        var store = new GlanceWidgetStore(_tempRoot);
        await store.SaveAsync(new GlanceWidgetData
        {
            WorldClocks = [new GlanceWorldClockData("UTC", " 伦敦 ")]
        });

        GlanceWidgetData reloaded = await new GlanceWidgetStore(_tempRoot).LoadAsync();

        Assert.Equal("伦敦", reloaded.WorldClocks[0].CityName);
    }

    [Fact]
    public async Task LoadAsync_ReturnsCopiesThatCannotMutateCachedState()
    {
        var store = new GlanceWidgetStore(_tempRoot);
        GlanceWidgetData first = await store.LoadAsync();
        first.ShowTime = false;

        GlanceWidgetData second = await store.LoadAsync();

        Assert.True(second.ShowTime);
    }

    [Fact]
    public async Task PerWidgetStores_KeepPreferencesIndependent()
    {
        var first = new GlanceWidgetStore(_tempRoot, "first-widget");
        var second = new GlanceWidgetStore(_tempRoot, "second-widget");

        await first.SaveAsync(new GlanceWidgetData
        {
            ShowTime = false,
            WorldClocks = [new GlanceWorldClockData("UTC", "伦敦")]
        });
        await second.SaveAsync(new GlanceWidgetData
        {
            ShowTime = true
        });

        GlanceWidgetData firstReloaded = await new GlanceWidgetStore(
            _tempRoot,
            "first-widget").LoadAsync();
        GlanceWidgetData secondReloaded = await new GlanceWidgetStore(
            _tempRoot,
            "second-widget").LoadAsync();

        Assert.NotEqual(first.StorePath, second.StorePath);
        Assert.False(firstReloaded.ShowTime);
        Assert.Single(firstReloaded.WorldClocks);
        Assert.True(secondReloaded.ShowTime);
        Assert.Empty(secondReloaded.WorldClocks);
    }

    [Fact]
    public async Task LoadAsync_MigratesV11PhotoDocumentWithoutLosingKeptSettings()
    {
        string legacyJson = """
        {
          "version": 11,
          "showTime": true,
          "showDate": false,
          "showWeekday": true,
          "showWeather": true,
          "timeFormat": "Hour24",
          "layout": "Calendar",
          "backgroundSource": "Bing",
          "onlineImageCategory": "Astronomy",
          "localImagePaths": ["C:\\Pictures\\one.jpg"],
          "rotationIntervalMinutes": 30,
          "transition": "SlideFade",
          "showCalendar": true,
          "traditionalCalendarMode": "ChineseLunar",
          "showChineseFestivals": true,
          "surfaceMaterial": "Transparent",
          "textContrast": "White"
        }
        """;
        Directory.CreateDirectory(_tempRoot);
        var store = new GlanceWidgetStore(_tempRoot);
        await File.WriteAllTextAsync(store.StorePath, legacyJson);

        GlanceWidgetData migrated = await new GlanceWidgetStore(_tempRoot).LoadAsync();

        Assert.Equal(GlanceWidgetData.CurrentVersion, migrated.Version);
        Assert.True(migrated.ShowTime);
        Assert.False(migrated.ShowDate);
        Assert.True(migrated.ShowWeekday);
        Assert.True(migrated.ShowWeather);
        Assert.Equal(GlanceTimeFormatMode.Hour24, migrated.TimeFormat);
        Assert.Equal(GlanceSurfaceMaterialMode.Transparent, migrated.SurfaceMaterial);
        Assert.Equal(GlanceTextContrastMode.White, migrated.TextContrast);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
        }
    }
}
