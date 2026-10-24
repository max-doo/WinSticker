#if WINSTICKER_NATIVE_AOT
namespace WinSticker.ViewModels;

// GlanceWidgetContent intentionally keeps its runtime Binding surface. Expose
// only the properties used by that XAML surface in NativeAOT builds.
[WinRT.GeneratedBindableCustomProperty([
    nameof(DateFontSize),
    nameof(DateText),
    nameof(HasWeatherRange),
    nameof(HasWeatherSummary),
    nameof(HasWorldClocks),
    nameof(IsForegroundVisible),
    nameof(ShowContrastPlate),
    nameof(ShowDate),
    nameof(ShowSeconds),
    nameof(ShowTime),
    nameof(ShowWeatherBlock),
    nameof(ShowWeekday),
    nameof(TimeFontFamily),
    nameof(TimeFontSize),
    nameof(TimeText),
    nameof(WeatherDescription),
    nameof(WeatherIcon),
    nameof(WeatherIconSize),
    nameof(WeatherLocationName),
    nameof(WeatherRangeText),
    nameof(WeatherSubFontSize),
    nameof(WeatherTemperatureText),
    nameof(WeatherTempFontSize),
    nameof(WeekdayText)
], [])]
public sealed partial class GlanceWidgetViewModel
{
}

[WinRT.GeneratedBindableCustomProperty([
    nameof(CityName),
    nameof(IsDaytime),
    nameof(RelativeDayText),
    nameof(SettingsDisplayName),
    nameof(TimeText)
], [])]
public sealed partial class GlanceWorldClockRow
{
}
#endif
