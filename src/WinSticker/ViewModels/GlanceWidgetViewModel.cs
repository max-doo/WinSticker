using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using WinSticker.Models;
using WinSticker.Platform;
using WinSticker.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace WinSticker.ViewModels;

/// <summary>
/// One world-clock row under the main clock: city name plus its current time.
/// Refreshed wholesale on each clock tick; the rows are plain display state.
/// </summary>
public sealed partial class GlanceWorldClockRow : ObservableObject
{
    public GlanceWorldClockRow(string timeZoneId, string cityName)
    {
        TimeZoneId = timeZoneId;
        CityName = cityName;
    }

    public string TimeZoneId { get; }
    public string CityName { get; }

    /// <summary>Settings-only row: the city plus its zone id, for list disambiguation.</summary>
    public string? SettingsDisplayName
    {
        get => _settingsDisplayName;
        set => SetProperty(ref _settingsDisplayName, value);
    }

    private string? _settingsDisplayName;

    [ObservableProperty]
    public partial string TimeText { get; set; }

    /// <summary>Relative day against the local date: "明天" / "昨天", empty when equal.</summary>
    [ObservableProperty]
    public partial string RelativeDayText { get; set; }

    /// <summary>Whether the city is in its local daytime (6:00–18:00); night rows dim.</summary>
    [ObservableProperty]
    public partial bool IsDaytime { get; set; }
}

public sealed partial class GlanceWidgetViewModel : ObservableObject, IDisposable
{
    private readonly GlanceWidgetStore _store;
    private readonly GlanceWeatherSummaryService _weatherSummaryService;
    private readonly LocalizationService _localizationService;
    private readonly SettingsService? _settingsService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _clockTimer;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private GlanceWidgetData _settings = new();
    private GlanceTextContrastDecision _contrastDecision = GlanceTextContrastDecision.Unknown;
    private string _appliedSurfaceSignature = string.Empty;
    private bool _isContrastRefreshRunning;
    private GlanceWeatherSummary? _weatherSummary;
    private DateTimeOffset _weatherFetchedUtc = DateTimeOffset.MinValue;
    private bool _isWeatherRefreshRunning;
    private bool _isDisposedWeather;
    private string _timeText = string.Empty;
    private string _dateText = string.Empty;
    private string _weekdayText = string.Empty;
    private bool _isWindowVisible = true;
    private bool _isDisposed;
    private double _availableWidth = 360;
    private double _availableHeight = 200;

    public GlanceWidgetViewModel(
        WidgetConfig config,
        LocalizationService localizationService,
        GlanceWidgetStore? store = null,
        DispatcherQueue? dispatcherQueue = null,
        SettingsService? settingsService = null)
    {
        if (config.WidgetKind != WidgetKind.Glance)
        {
            throw new ArgumentException("Glance content requires a Glance widget config.", nameof(config));
        }

        Config = config;
        _localizationService = localizationService;
        _store = store ?? GlanceWidgetStore.ForWidget(config.Id);
        _settingsService = settingsService;
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("Glance content must be created on a UI thread.");
        _weatherSummaryService = new GlanceWeatherSummaryService(localizationService, settingsService);

        _clockTimer = _dispatcherQueue.CreateTimer();
        _clockTimer.IsRepeating = false;
        _clockTimer.Tick += ClockTimer_Tick;

        _store.Changed += Store_Changed;
        _localizationService.LanguageChanged += LocalizationService_LanguageChanged;
        if (_settingsService is not null)
        {
            _settingsService.SettingsChanged += SettingsService_SettingsChanged;
        }
    }

    public WidgetConfig Config { get; }

    public GlanceWidgetData Settings => _settings;
    public ObservableCollection<GlanceWorldClockRow> WorldClockRows { get; } = [];

    public string TimeText { get => _timeText; private set => SetProperty(ref _timeText, value); }
    public string DateText { get => _dateText; private set => SetProperty(ref _dateText, value); }
    public string WeekdayText { get => _weekdayText; private set => SetProperty(ref _weekdayText, value); }

    public bool ShowTime => _settings.ShowTime;
    public bool ShowDate => _settings.ShowDate;
    public bool ShowYear => _settings.ShowYear;
    public bool ShowWeekday => _settings.ShowWeekday;
    public bool ShowSeconds => _settings.ShowSeconds;
    public double SizeScale => _settings.SizeScale;
    public bool HasWorldClocks => WorldClockRows.Count > 0;

    // ─── Weather block ────────────────────────────────────────────
    public bool ShowWeather => _settings.ShowWeather;
    public bool HasWeatherSummary => _weatherSummary is not null;
    public bool ShowWeatherBlock => ShowWeather && HasWeatherSummary;
    public ImageSource? WeatherIcon => _weatherSummary?.Icon;
    public string WeatherTemperatureText => _weatherSummary?.TemperatureText ?? string.Empty;
    public string WeatherDescription => _weatherSummary?.Description ?? string.Empty;
    public string WeatherRangeText => _weatherSummary?.RangeText ?? string.Empty;
    public bool HasWeatherRange => _weatherSummary?.HasRange == true;
    public string WeatherLocationName
    {
        get
        {
            string raw = _weatherSummary?.LocationName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            int commaIndex = raw.IndexOfAny([',', '，']);
            return commaIndex > 0 ? raw[..commaIndex].Trim() : raw.Trim();
        }
    }

    // ─── Surface material (per-widget transparency) ────────────────
    public event EventHandler? SurfaceAppearanceChanged;

    public GlanceSurfaceMaterialMode SurfaceMaterial => _settings.SurfaceMaterial;
    public bool IsTransparentSurface =>
        _settings.SurfaceMaterial == GlanceSurfaceMaterialMode.Transparent;
    public bool IsGlassSurface => _settings.SurfaceMaterial == GlanceSurfaceMaterialMode.Glass;
    public GlanceTextContrastMode TextContrast => _settings.TextContrast;
    public bool IsTextPlateEnabled => _settings.ShowTextPlate;

    /// <summary>Material the host window must use; null follows the global setting.</summary>
    public string? SurfaceMaterialTypeOverride => _settings.SurfaceMaterial switch
    {
        GlanceSurfaceMaterialMode.Transparent => SettingsService.WidgetMaterialTypeNone,
        GlanceSurfaceMaterialMode.Glass => SettingsService.WidgetMaterialTypeAcrylic,
        _ => null
    };

    /// <summary>Surface opacity the host window must use; null follows the global setting.</summary>
    public double? SurfaceOpacityOverride => _settings.SurfaceMaterial switch
    {
        GlanceSurfaceMaterialMode.Transparent => 0.0,
        GlanceSurfaceMaterialMode.Glass => Math.Clamp(_settings.SurfaceMaterialOpacity, 0.0, 1.0),
        _ => null
    };

    /// <summary>True when the window must draw no border and no divider.</summary>
    public bool SuppressSurfaceChrome => IsTransparentSurface;

    /// <summary>
    /// Theme the text scope adopts. A transparent surface resolves against the
    /// sampled wallpaper; every other surface follows the window theme.
    /// </summary>
    public ElementTheme ForegroundTheme
    {
        get
        {
            if (!IsTransparentSurface)
            {
                return ElementTheme.Default;
            }

            return _settings.TextContrast switch
            {
                GlanceTextContrastMode.White => ElementTheme.Dark,
                GlanceTextContrastMode.Black => ElementTheme.Light,
                GlanceTextContrastMode.Off => ElementTheme.Default,
                _ => _contrastDecision.Foreground
            };
        }
    }

    /// <summary>
    /// Whether the faint plate behind the text is needed right now. Only the
    /// transparent surface without a photo uses it.
    /// </summary>
    public bool ShowContrastPlate =>
        IsTransparentSurface &&
        _settings.TextContrast != GlanceTextContrastMode.Off &&
        _settings.ShowTextPlate &&
        // High contrast owns the palette: the system colours already guarantee
        // the contrast, and a themed plate would fight them.
        !WindowsCompatibilityService.IsHighContrast &&
        _contrastDecision.ShowPlate;

    /// <summary>
    /// Identity of everything that feeds the native backdrop. Reloading
    /// settings compares it so the host is only asked to re-apply DWM state
    /// when the surface actually changed.
    /// </summary>
    internal string SurfaceSignature => string.Create(
        CultureInfo.InvariantCulture,
        $"{_settings.SurfaceMaterial}|{_settings.SurfaceMaterialOpacity:F3}");

    public bool IsForegroundVisible =>
        ShowTime || ShowDate || ShowWeekday || ShowWeather || HasWorldClocks;
    public FontFamily TimeFontFamily => new(string.IsNullOrWhiteSpace(_settings.TimeFontFamily)
        ? "XamlAutoFontFamily"
        : _settings.TimeFontFamily);

    private double BaseUnitScale => Math.Clamp(
        Math.Min(_availableWidth > 0 ? _availableWidth / 280.0 : 1.0,
                 _availableHeight > 0 ? _availableHeight / 110.0 : 1.0),
        0.6, 3.0);

    public double TimeFontSize => RoundFontSize(
        Math.Clamp(48 * BaseUnitScale, 24, 120) * _settings.TimeScale);

    public double DateFontSize => RoundFontSize(Math.Clamp(13 * BaseUnitScale, 10, 26));

    public double WeatherTempFontSize => RoundFontSize(Math.Clamp(24 * BaseUnitScale, 16, 52));

    public double WeatherSubFontSize => RoundFontSize(Math.Clamp(12 * BaseUnitScale, 9, 22));

    public double WeatherIconSize => RoundFontSize(Math.Clamp(30 * BaseUnitScale, 20, 68));

    public async Task InitializeAsync()
    {
        _settings = await _store.LoadAsync();
        ApplySettingsProperties();
        UpdateDateAndTime();
        UpdateTimers();
        _appliedSurfaceSignature = SurfaceSignature;
        await RefreshContrastAsync();
        // The weather block must not delay widget construction — it reads the
        // shared weather cache and only reaches the network when that is cold.
        _ = RefreshWeatherAsync();
    }

    public void ApplyAppearance()
    {
        OnPropertyChanged(nameof(ForegroundTheme));
        OnPropertyChanged(nameof(ShowContrastPlate));
    }

    public void OnActivated()
    {
        UpdateDateAndTime();
        if (IsTransparentSurface)
        {
            // The wallpaper may have changed while the widget was inactive.
            _ = RefreshContrastAsync();
        }
    }

    public void OnDeactivated()
    {
    }

    public void OnWindowVisibilityChanged(bool visible)
    {
        _isWindowVisible = visible;
        UpdateTimers();
        if (visible)
        {
            UpdateDateAndTime();
        }
    }

    public void OnCompactStateChanged(bool collapsed)
    {
        UpdateTimers();
    }

    public void OnWindowRevealCompleted()
    {
        if (_isWindowVisible && _weatherSummary is null)
        {
            _ = RefreshWeatherAsync();
        }
    }

    public void UpdateAvailableSize(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (Math.Abs(_availableWidth - width) < 0.01 &&
            Math.Abs(_availableHeight - height) < 0.01)
        {
            return;
        }

        _availableWidth = width;
        _availableHeight = height;
        OnPropertyChanged(nameof(TimeFontSize));
        OnPropertyChanged(nameof(DateFontSize));
        OnPropertyChanged(nameof(WeatherTempFontSize));
        OnPropertyChanged(nameof(WeatherSubFontSize));
        OnPropertyChanged(nameof(WeatherIconSize));
    }

    private static double RoundFontSize(double value) => Math.Round(value * 2) / 2;

    public Task SetDisplayElementAsync(
        GlanceDisplayElement element,
        bool isVisible)
    {
        return _store.UpdateAsync(settings =>
            GlanceWidgetSettingsPolicy.SetDisplayElement(
                settings,
                element,
                isVisible));
    }

    public Task SetShowSecondsAsync(bool showSeconds)
    {
        return _store.UpdateAsync(settings => settings.ShowSeconds = showSeconds);
    }

    /// <summary>
    /// Overall patch scale. A change resizes the window unless the user has
    /// manually resized it to a size the slider would override.
    /// </summary>
    public Task SetSizeScaleAsync(double scale)
    {
        return _store.UpdateAsync(settings =>
            GlanceWorldClockPolicy.SetSizeScale(settings, scale));
    }

    public Task AddWorldClockAsync(string timeZoneId, string? cityName)
    {
        return _store.UpdateAsync(settings =>
            GlanceWorldClockPolicy.AddWorldClock(settings, timeZoneId, cityName));
    }

    public Task RemoveWorldClockAsync(string timeZoneId)
    {
        return _store.UpdateAsync(settings =>
            GlanceWorldClockPolicy.RemoveWorldClock(settings, timeZoneId));
    }

    public Task SetSurfaceMaterialAsync(GlanceSurfaceMaterialMode mode)
    {
        return _store.UpdateAsync(settings =>
            GlanceWidgetSettingsPolicy.SetSurfaceMaterial(settings, mode));
    }

    public Task SetSurfaceMaterialOpacityAsync(double opacity)
    {
        return _store.UpdateAsync(settings =>
            GlanceWidgetSettingsPolicy.SetSurfaceMaterialOpacity(settings, opacity));
    }

    public Task SetTextContrastAsync(GlanceTextContrastMode mode)
    {
        return _store.UpdateAsync(settings =>
            GlanceWidgetSettingsPolicy.SetTextContrast(settings, mode));
    }

    public Task SetTextPlateEnabledAsync(bool enabled)
    {
        return _store.UpdateAsync(settings => settings.ShowTextPlate = enabled);
    }

    private async Task ReloadSettingsAsync()
    {
        _settings = await _store.LoadAsync();
        ApplySettingsProperties();
        UpdateDateAndTime();
        UpdateTimers();
        await RefreshContrastAsync();
        if (_settings.ShowWeather && _weatherSummary is null)
        {
            _ = RefreshWeatherAsync();
        }
        else
        {
            RaiseWeatherProperties();
        }

        string signature = SurfaceSignature;
        if (!string.Equals(signature, _appliedSurfaceSignature, StringComparison.Ordinal))
        {
            _appliedSurfaceSignature = signature;
            // The host owns the backdrop; it re-applies it only when this fires.
            SurfaceAppearanceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Re-resolves the transparent surface's text colour against the current
    /// desktop wallpaper. Cheap to call: the probe caches per wallpaper file
    /// path and last-write time, so repeated calls only cost a file stat.
    /// </summary>
    public async Task RefreshContrastAsync()
    {
        if (_isDisposed || _isContrastRefreshRunning)
        {
            return;
        }

        _isContrastRefreshRunning = true;
        try
        {
            GlanceTextContrastDecision decision = await DesktopWallpaperContrastProbe
                .ResolveAsync(_settings.TextContrast, _settings.ShowTextPlate);
            if (_isDisposed)
            {
                return;
            }

            if (decision == _contrastDecision)
            {
                return;
            }

            _contrastDecision = decision;
            // The probe decodes off-thread; property changes must land on the
            // dispatcher so binding updates stay on the UI thread.
            if (_dispatcherQueue.HasThreadAccess)
            {
                RaiseContrastProperties();
            }
            else
            {
                _dispatcherQueue.TryEnqueue(RaiseContrastProperties);
            }
        }
        finally
        {
            _isContrastRefreshRunning = false;
        }
    }

    private void RaiseContrastProperties()
    {
        OnPropertyChanged(nameof(ForegroundTheme));
        OnPropertyChanged(nameof(ShowContrastPlate));
    }

    private void ApplySettingsProperties()
    {
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(ShowTime));
        OnPropertyChanged(nameof(ShowDate));
        OnPropertyChanged(nameof(ShowYear));
        OnPropertyChanged(nameof(ShowWeekday));
        OnPropertyChanged(nameof(ShowSeconds));
        OnPropertyChanged(nameof(SizeScale));
        OnPropertyChanged(nameof(ShowWeather));
        OnPropertyChanged(nameof(HasWeatherSummary));
        OnPropertyChanged(nameof(ShowWeatherBlock));
        OnPropertyChanged(nameof(IsForegroundVisible));
        OnPropertyChanged(nameof(HasWorldClocks));
        OnPropertyChanged(nameof(TimeFontFamily));
        OnPropertyChanged(nameof(TimeFontSize));
        OnPropertyChanged(nameof(DateFontSize));
        OnPropertyChanged(nameof(WeatherTempFontSize));
        OnPropertyChanged(nameof(WeatherSubFontSize));
        OnPropertyChanged(nameof(WeatherIconSize));
        OnPropertyChanged(nameof(SurfaceMaterial));
        OnPropertyChanged(nameof(IsTransparentSurface));
        OnPropertyChanged(nameof(IsGlassSurface));
        OnPropertyChanged(nameof(TextContrast));
        OnPropertyChanged(nameof(IsTextPlateEnabled));
        OnPropertyChanged(nameof(SuppressSurfaceChrome));
        OnPropertyChanged(nameof(ForegroundTheme));
        OnPropertyChanged(nameof(ShowContrastPlate));
        SyncWorldClockRows();
    }

    /// <summary>
    /// Rebuilds the row collection from settings. Rows are keyed by zone id so
    /// an unchanged list keeps its row objects (and their tick-updated text).
    /// </summary>
    private void SyncWorldClockRows()
    {
        var desired = GlanceWorldClockPolicy.NormalizeWorldClocks(_settings.WorldClocks);
        for (int index = WorldClockRows.Count - 1; index >= 0; index--)
        {
            if (desired.All(clock => !string.Equals(
                    clock.TimeZoneId,
                    WorldClockRows[index].TimeZoneId,
                    StringComparison.OrdinalIgnoreCase)))
            {
                WorldClockRows.RemoveAt(index);
            }
        }

        for (int index = 0; index < desired.Count; index++)
        {
            GlanceWorldClockData clock = desired[index];
            int existingIndex = WorldClockRows.ToList().FindIndex(row => string.Equals(
                row.TimeZoneId,
                clock.TimeZoneId,
                StringComparison.OrdinalIgnoreCase));
            if (existingIndex < 0)
            {
                WorldClockRows.Insert(
                    index,
                    new GlanceWorldClockRow(clock.TimeZoneId, GlanceWorldClockPolicy.ResolveCityName(clock)));
            }
            else if (existingIndex != index)
            {
                GlanceWorldClockRow row = WorldClockRows[existingIndex];
                WorldClockRows.RemoveAt(existingIndex);
                WorldClockRows.Insert(index, row);
            }
        }

        UpdateWorldClockRows();
    }

    private void UpdateWorldClockRows()
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        DateOnly localDate = DateOnly.FromDateTime(DateTime.Now);
        CultureInfo culture = GetCulture();
        foreach (GlanceWorldClockRow row in WorldClockRows)
        {
            if (!GlanceWorldClockPolicy.TryResolveZone(row.TimeZoneId, out TimeZoneInfo zone))
            {
                continue;
            }

            DateTimeOffset zoneNow = TimeZoneInfo.ConvertTime(utcNow, zone);
            row.TimeText = GlanceWorldClockPolicy.FormatCityTime(
                utcNow,
                zone,
                _settings.TimeFormat,
                culture);
            int dayDiff = DateOnly.FromDateTime(zoneNow.DateTime).DayNumber - localDate.DayNumber;
            row.RelativeDayText = dayDiff switch
            {
                1 => _localizationService.T("Glance.WorldClock.Tomorrow"),
                -1 => _localizationService.T("Glance.WorldClock.Yesterday"),
                int diff when diff > 1 => string.Format(
                    culture,
                    _localizationService.T("Glance.WorldClock.DaysAhead"),
                    diff),
                int diff when diff < -1 => string.Format(
                    culture,
                    _localizationService.T("Glance.WorldClock.DaysBehind"),
                    -diff),
                _ => string.Empty
            };
            row.IsDaytime = zoneNow.Hour is >= 6 and < 18;
        }
    }

    private void UpdateDateAndTime()
    {
        CultureInfo culture = GetCulture();
        DateTime now = DateTime.Now;
        TimeText = FormatTimeText(now, _settings.TimeFormat, _settings.ShowSeconds, culture);
        DateText = FormatDateText(now, culture, _settings.ShowYear);
        WeekdayText = now.ToString("dddd", culture);
        UpdateWorldClockRows();
    }

    internal static string FormatTimeText(
        DateTime date,
        GlanceTimeFormatMode mode,
        bool includeSeconds,
        CultureInfo displayCulture,
        CultureInfo? systemCulture = null)
    {
        ArgumentNullException.ThrowIfNull(displayCulture);
        CultureInfo timeCulture = mode == GlanceTimeFormatMode.FollowSystem
            ? systemCulture ?? CultureInfo.CurrentCulture
            : displayCulture;
        string pattern = mode switch
        {
            GlanceTimeFormatMode.FollowSystem => RemoveAmPmDesignator(
                timeCulture.DateTimeFormat.ShortTimePattern),
            GlanceTimeFormatMode.Hour24 => "HH:mm",
            GlanceTimeFormatMode.Hour12 => "h:mm",
            _ => RemoveAmPmDesignator(
                CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern)
        };
        if (includeSeconds)
        {
            pattern = WithSeconds(pattern);
        }

        return date.ToString(pattern, timeCulture);
    }

    /// <summary>Inserts a seconds token after the minute token of a time pattern.</summary>
    internal static string WithSeconds(string pattern)
    {
        int minuteStart = pattern.IndexOf('m');
        if (minuteStart < 0)
        {
            return pattern;
        }

        int minuteEnd = minuteStart;
        while (minuteEnd + 1 < pattern.Length && pattern[minuteEnd + 1] == 'm')
        {
            minuteEnd++;
        }

        string minuteToken = pattern[minuteStart..(minuteEnd + 1)];
        return pattern.Replace(
            minuteToken,
            $"{minuteToken}:ss",
            StringComparison.Ordinal);
    }

    private static string RemoveAmPmDesignator(string pattern)
    {
        StringBuilder result = new(pattern.Length);
        for (int index = 0; index < pattern.Length; index++)
        {
            char current = pattern[index];
            if (current is '\'' or '"')
            {
                char quote = current;
                result.Append(current);
                while (++index < pattern.Length)
                {
                    result.Append(pattern[index]);
                    if (pattern[index] == quote)
                    {
                        break;
                    }
                }
                continue;
            }

            if (current == '\\' && index + 1 < pattern.Length)
            {
                result.Append(current);
                result.Append(pattern[++index]);
                continue;
            }

            if (current == 't')
            {
                while (index + 1 < pattern.Length && pattern[index + 1] == 't')
                {
                    index++;
                }
                continue;
            }

            result.Append(current);
        }

        return result.ToString().Trim();
    }

    internal static string FormatDateText(
        DateTime date,
        CultureInfo culture,
        bool includeYear)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (!includeYear)
        {
            return date.ToString("M", culture);
        }

        // LongDatePattern already carries the locale's natural year/month/day
        // order. Remove its weekday token because weekday is an independent
        // Glance display option.
        string pattern = Regex.Replace(
            culture.DateTimeFormat.LongDatePattern,
            @"(?<!d)d{3,4}(?!d)",
            string.Empty,
            RegexOptions.CultureInvariant);
        pattern = pattern.Trim().Trim(',', '，', '،').Trim();
        return date.ToString(pattern, culture);
    }

    private CultureInfo GetCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo(_localizationService.CurrentCultureName);
        }
        catch
        {
            return CultureInfo.CurrentCulture;
        }
    }

    private void UpdateTimers()
    {
        UpdateClockTimer();
    }

    private void UpdateClockTimer()
    {
        _clockTimer.Stop();
        bool needsClockForRows = HasWorldClocks || ShowWeather;
        if (!_isWindowVisible || (!_settings.ShowTime && !ShowDate && !ShowWeekday && !needsClockForRows) || _isDisposed)
        {
            return;
        }

        DateTime now = DateTime.Now;
        _clockTimer.Interval = _settings.ShowTime && _settings.ShowSeconds
            ? TimeSpan.FromSeconds(1)
            : TimeSpan.FromSeconds(Math.Max(1, 60 - now.Second)) - TimeSpan.FromMilliseconds(now.Millisecond);
        _clockTimer.Start();
    }

    private void ClockTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        UpdateDateAndTime();
        UpdateClockTimer();
        EnsureWeatherFresh();
    }

    /// <summary>
    /// Piggybacks on the clock tick instead of owning a second timer: the
    /// summary only needs refreshing every few minutes, so a cheap age check on
    /// a tick that already runs is the whole schedule.
    /// </summary>
    private void EnsureWeatherFresh()
    {
        if (!_settings.ShowWeather || _isWeatherRefreshRunning)
        {
            return;
        }

        if (DateTimeOffset.UtcNow - _weatherFetchedUtc < TimeSpan.FromMinutes(15))
        {
            return;
        }

        _ = RefreshWeatherAsync();
    }

    /// <summary>
    /// Loads or refreshes the weather summary. Failures are silent by design:
    /// the block simply disappears rather than showing a placeholder that
    /// claims a temperature nobody measured.
    /// </summary>
    public async Task RefreshWeatherAsync(bool forceRefresh = false)
    {
        if (_isDisposedWeather || _isDisposed || _isWeatherRefreshRunning || !_settings.ShowWeather)
        {
            return;
        }

        _isWeatherRefreshRunning = true;
        try
        {
            GlanceWeatherSummary? summary = await _weatherSummaryService.GetAsync(
                forceRefresh,
                _lifetimeCts.Token);
            if (_isDisposedWeather)
            {
                return;
            }

            _weatherSummary = summary;
            _weatherFetchedUtc = DateTimeOffset.UtcNow;
            RaiseWeatherProperties();
        }
        finally
        {
            _isWeatherRefreshRunning = false;
        }
    }

    private void RaiseWeatherProperties()
    {
        if (!_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(RaiseWeatherProperties);
            return;
        }

        OnPropertyChanged(nameof(HasWeatherSummary));
        OnPropertyChanged(nameof(ShowWeatherBlock));
        OnPropertyChanged(nameof(WeatherIcon));
        OnPropertyChanged(nameof(WeatherTemperatureText));
        OnPropertyChanged(nameof(WeatherDescription));
        OnPropertyChanged(nameof(WeatherRangeText));
        OnPropertyChanged(nameof(HasWeatherRange));
        OnPropertyChanged(nameof(WeatherLocationName));
        OnPropertyChanged(nameof(IsForegroundVisible));
    }

    private void Store_Changed(object? sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            _ = ReloadSettingsAsync();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => _ = ReloadSettingsAsync());
        }
    }

    private void LocalizationService_LanguageChanged()
    {
        if (_isDisposed)
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(UpdateDateAndTime);
    }

    private void SettingsService_SettingsChanged()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            _ = RefreshWeatherAsync(forceRefresh: true);
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => _ = RefreshWeatherAsync(forceRefresh: true));
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _store.Changed -= Store_Changed;
        _localizationService.LanguageChanged -= LocalizationService_LanguageChanged;
        if (_settingsService is not null)
        {
            _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
        }
        _clockTimer.Stop();
        _clockTimer.Tick -= ClockTimer_Tick;
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        _isDisposedWeather = true;
        _weatherSummaryService.Dispose();
    }
}
