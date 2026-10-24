#if WINSTICKER_NATIVE_AOT
namespace WinSticker.ViewModels;

// GlanceWidgetContent intentionally keeps its runtime Binding surface. Expose
// only the properties used by that XAML surface in NativeAOT builds.
[WinRT.GeneratedBindableCustomProperty([
    nameof(CalendarCompactTimeFontSize),
    nameof(CalendarCornerRadius),
    nameof(CalendarPanelHeight),
    nameof(CalendarPanelMaxWidth),
    nameof(CalendarPanelWidth),
    nameof(CompactCalendarDateText),
    nameof(CompactTimeFontSize),
    nameof(DateText),
    nameof(HasPhotoInfo),
    nameof(HasWeatherSummary),
    nameof(IsCalendarLayout),
    nameof(IsCenteredLayout),
    nameof(IsCompactCalendarPresentation),
    nameof(IsEditorialLayout),
    nameof(IsExpandedCalendarPresentation),
    nameof(IsForegroundVisible),
    nameof(IsImmersiveLayout),
    nameof(IsPaused),
    nameof(NextToolTip),
    nameof(PauseToolTip),
    nameof(PhotoInfoText),
    nameof(ReadabilityOpacity),
    nameof(ShowCalendarImageReadability),
    nameof(ShowContrastPlate),
    nameof(ShowDate),
    nameof(ShowExpandedCalendarImageReadability),
    nameof(ShowNonCalendarImageReadability),
    nameof(ShowPhotoControls),
    nameof(ShowTime),
    nameof(ShowWeatherRow),
    nameof(ShowWeekday),
    nameof(TimeFontFamily),
    nameof(TimeFontSize),
    nameof(TimeText),
    nameof(TraditionalCalendarTitle),
    nameof(WeatherIcon),
    nameof(WeatherText),
    nameof(WeekdayText)
], [])]
public sealed partial class GlanceWidgetViewModel
{
}
#endif
