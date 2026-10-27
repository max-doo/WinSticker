#if WINSTICKER_NATIVE_AOT
namespace WinSticker.ViewModels;

[WinRT.GeneratedBindableCustomProperty([
    nameof(AppVersion),
    nameof(AutoCheckForUpdates),
    nameof(AutoDownloadUpdates),
    nameof(AutoStart),
    nameof(AutoStartStatusText),
    nameof(AutoStartStatusVisibility),
    nameof(AutoStartSystemSettingsVisibility),
    nameof(AutoStartModeVisibility),
    nameof(SelectedAutoStartMode),
    nameof(AvailableAutoStartModeOptions),
    nameof(SilentStartup),
    nameof(ShowReleaseNotesAfterUpdate),
    nameof(AvailableLanguageOptions),
    nameof(CanOpenUpdateFallback),
    nameof(IsOneClickActionEnabled),
    nameof(ManualUpdateDownloadUrl),
    nameof(OneClickActionButtonText),

    nameof(SelectedLanguage),
    nameof(ShowUpdateToastNotification),
    nameof(UpdateActivityCardVisibility),
    nameof(UpdateAutoCheckVisibility),
    nameof(UpdateAutoDownloadVisibility),
    nameof(UpdateBadgeStateName),
    nameof(UpdateBadgeVisibility),
    nameof(UpdateToastNotificationVisibility),
    nameof(UpdateDetailText),
    nameof(UpdateFallbackActionText),
    nameof(UpdateFallbackVisibility),
    nameof(UpdateProgressIsIndeterminate),
    nameof(UpdateProgressText),
    nameof(UpdateProgressTextVisibility),
    nameof(UpdateProgressValue),
    nameof(UpdateProgressVisibility),
    nameof(UpdateStatusText),
    nameof(ViewReleaseNotesButtonText),
], [])]
public partial class SettingsViewModel
{
}
#endif
