using WinSticker.Contracts;
using WinSticker.Controls.WidgetContents;
using WinSticker.Models;

namespace WinSticker.Services;

internal sealed class WeatherWidgetContentProvider : IWidgetContentProvider
{
    public WidgetKind WidgetKind => WidgetKind.Weather;

    public bool CanCreateDetachedContent => true;

    public IWidgetContent CreateDetachedContent(WidgetConfig config, WidgetContentProviderContext context)
    {
        if (config.WidgetKind != WidgetKind)
        {
            throw new ArgumentException("Weather content requires a Weather widget config.", nameof(config));
        }

        WeatherService? weatherService = null;
#if WINSTICKER_NATIVE_AOT && WINSTICKER_AOT_SMOKE_HARNESS
        weatherService = AotWeatherSurfaceFixture.TryCreateService(config);
#endif
        return new WeatherWidgetContentAdapter(
            config,
            context.LocalizationService,
            context.SettingsService,
            weatherService);
    }
}
