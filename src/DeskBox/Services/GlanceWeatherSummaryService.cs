using DeskBox.Helpers;
using DeskBox.Models;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DeskBox.Services;

/// <summary>
/// The compact weather line under the clock: icon + temperature + today's range.
/// </summary>
/// <param name="Description">Localized condition text ("晴").</param>
/// <param name="TemperatureText">Current temperature with a degree sign, already unit-converted.</param>
/// <param name="RangeText">Today's low–high, empty when the forecast has none.</param>
/// <param name="Icon">Vector icon source resolved from the user's icon style.</param>
/// <param name="IsStale">True when the value came from cache past its TTL.</param>
internal sealed record GlanceWeatherSummary(
    string Description,
    string TemperatureText,
    string RangeText,
    ImageSource Icon,
    bool IsStale)
{
    public bool HasRange => !string.IsNullOrEmpty(RangeText);
}

/// <summary>
/// Weather for the Glance clock.
///
/// This deliberately wraps <see cref="WeatherService"/> instead of the weather
/// widget's view model: the clock only needs one line, and sharing the service
/// means the clock and the weather widget read the same cache entry, so a
/// desktop with both never doubles the request rate. Location resolution
/// mirrors the weather widget's rules (manual city wins, then the cached
/// resolved location, then the Windows location API) without duplicating its
/// full state machine.
/// </summary>
internal sealed class GlanceWeatherSummaryService : IDisposable
{
    /// <summary>Matches the weather widget's default refresh cadence.</summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly WeatherService _weatherService = new();
    private readonly SettingsService? _settingsService;
    private readonly LocalizationService _localizationService;
    private bool _isDisposed;
    private bool _loggedMissingLocation;

    public GlanceWeatherSummaryService(
        LocalizationService localizationService,
        SettingsService? settingsService)
    {
        _localizationService = localizationService;
        _settingsService = settingsService;
    }

    public async Task<GlanceWeatherSummary?> GetAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (_isDisposed)
        {
            return null;
        }

        try
        {
            (double Latitude, double Longitude, string Name)? location =
                await ResolveLocationAsync(cancellationToken);
            if (_isDisposed || location is not { } resolved)
            {
                // Logged once per widget: without a location the row is simply
                // absent, which is indistinguishable from a broken layout.
                if (!_loggedMissingLocation)
                {
                    _loggedMissingLocation = true;
                    App.Log("[GlanceWeather] No location available; weather row hidden");
                }

                return null;
            }

            WeatherData? data = await _weatherService.GetWeatherAsync(
                resolved.Latitude,
                resolved.Longitude,
                resolved.Name,
                forceRefresh,
                CacheDuration,
                cancellationToken: cancellationToken);
            if (_isDisposed || data?.Current is not { } current)
            {
                return null;
            }

            bool isDay = current.IsDay != 0;
            string iconStyle = _settingsService?.Settings.Weather.WeatherIconStyle ??
                Contracts.WeatherOptionKinds.IconStyleFluent;
            string temperature = FormatTemperature(current.Temperature);
            string range = FormatRange(data);
            string description = WeatherCodeMapper.GetDescription(
                current.WeatherCode,
                _localizationService);

            // One line per fetch: the row's visibility depends on three inputs
            // (setting, summary, width), so a silent failure here looks exactly
            // like a layout bug in the field report.
            App.Log(
                $"[GlanceWeather] Summary '{temperature} {description}' range='{range}' " +
                $"city='{resolved.Name}' stale={data.IsStale}");

            return new GlanceWeatherSummary(
                description,
                temperature,
                range,
                new SvgImageSource(WeatherCodeMapper.GetIconUri(
                    current.WeatherCode,
                    isDay,
                    iconStyle)),
                data.IsStale);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            App.LogVerbose($"[GlanceWeather] Summary unavailable: {ex.Message}");
            return null;
        }
    }

    private async Task<(double Latitude, double Longitude, string Name)?> ResolveLocationAsync(
        CancellationToken cancellationToken)
    {
        if (_settingsService?.Settings.Weather is { } weather &&
            !weather.WeatherAutoLocation &&
            (weather.WeatherLatitude != 0 || weather.WeatherLongitude != 0))
        {
            return (weather.WeatherLatitude, weather.WeatherLongitude, weather.WeatherCityName);
        }

        WeatherCacheState state = await _weatherService.LoadCacheStateAsync();
        if (_isDisposed)
        {
            return null;
        }

        if (state.LastLocation?.IsValid == true)
        {
            return (
                state.LastLocation.Latitude,
                state.LastLocation.Longitude,
                state.LastLocation.Name);
        }

        cancellationToken.ThrowIfCancellationRequested();
        (double Lat, double Lon, string Name)? detected =
            await WindowsLocationHelper.GetLocationAsync(_localizationService);
        if (detected is not { } location)
        {
            // No location permission and no cached one: no weather line. Showing
            // a guessed city would be worse than showing nothing.
            return null;
        }

        await _weatherService.SaveResolvedLocationAsync(
            location.Lat,
            location.Lon,
            location.Name,
            DateTimeOffset.UtcNow);
        return (location.Lat, location.Lon, location.Name);
    }

    /// <summary>
    /// Compact form: the clock line keeps a bare degree sign ("21°") like the
    /// phone widgets it mirrors; the unit itself is already implied by the
    /// weather widget's own display.
    /// </summary>
    private string FormatTemperature(double celsius)
    {
        bool fahrenheit = string.Equals(
            _settingsService?.Settings.Weather.WeatherTemperatureUnit,
            SettingsService.WeatherTemperatureUnitFahrenheit,
            StringComparison.Ordinal);
        double value = fahrenheit ? celsius * 9 / 5 + 32 : celsius;
        return $"{Math.Round(value)}\u00B0";
    }

    private string FormatRange(WeatherData data)
    {
        if (data.Daily is not { } daily ||
            daily.TemperatureMin.Count == 0 ||
            daily.TemperatureMax.Count == 0)
        {
            return string.Empty;
        }

        string low = FormatTemperature(daily.TemperatureMin[0]);
        string high = FormatTemperature(daily.TemperatureMax[0]);
        return $"{low}~{high}";
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _weatherService.Dispose();
    }
}
