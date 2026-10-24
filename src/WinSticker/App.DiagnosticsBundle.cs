using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WinSticker.Core.Persistence;
using WinSticker.Helpers;
using WinSticker.Models;
using WinSticker.Platform;
using WinSticker.Services;

namespace WinSticker;

public partial class App
{
    public WinStickerDiagnosticsBundleService DiagnosticsBundleService { get; private set; } = null!;

    public WinStickerDiagnosticSnapshot CreateDiagnosticSnapshot()
    {
        AppRuntimeHealthSnapshot? runtimeHealth = DiagnosticsService?.GetRuntimeHealthSnapshot(
            EverythingSearchService,
            WidgetManager?.GetFolderWatcherHealthSnapshots());
        WinStickerWidgetManagerDiagnostic widgetManager =
            WidgetManager?.CreateDiagnosticsSnapshot() ?? WinStickerWidgetManagerDiagnostic.Empty;
        IReadOnlyList<WinStickerDisplayDiagnostic> displays = GetDisplayDiagnostics();
        SettingsPersistenceFailure? saveFailure = SettingsService.LastPersistenceFailure;
        GlobalHotkeyGesture toggleGesture = GlobalHotkeyService?.CurrentGesture ??
            GlobalHotkeyService.NormalizeGesture(
                SettingsService.Settings.GlobalHotkeyModifiers,
                SettingsService.Settings.GlobalHotkeyKey);
        GlobalHotkeyGesture searchGesture = SearchHotkeyService?.CurrentGesture ??
            GlobalHotkeyService.NormalizeGesture(
                SettingsService.Settings.SearchHotkeyModifiers,
                SettingsService.Settings.SearchHotkeyKey);
        ShortcutNativeDiagnosticState shortcutNative =
            ShortcutNativeBackend.CaptureDiagnosticState();
        var startupLedger = new WinStickerStartupLedgerDiagnostic(
            StormGuardActive: _startupStormAssessment?.SuppressFileMutatingStartup == true,
            AssessmentReason: _startupStormAssessment?.Reason,
            PreviousLaunchStartedAtUtc: _startupStormAssessment?.PreviousLaunchStartedAtUtc);
        var callbackRadarCounts = Platform.CallbackDepth.SnapshotWindowOperationCounts();
        var callbackRadar = new WinStickerCallbackRadarDiagnostic(
            callbackRadarCounts.Values.Sum(),
            callbackRadarCounts,
            Platform.UiDispatch.PostAfterCallbackDroppedCount);
        RecoveryOfferMarker? recoveryOfferMarker = RecoveryOfferStore.TryRead();
        var recoveryOffer = new WinStickerRecoveryOfferDiagnostic(
            recoveryOfferMarker is not null,
            recoveryOfferMarker?.DetectedAtUtc.ToUniversalTime(),
            recoveryOfferMarker?.SnapshotCreatedAtUtc.ToUniversalTime(),
            recoveryOfferMarker?.SnapshotAppVersion,
            recoveryOfferMarker?.SnapshotWidgetCount ?? 0,
            _lastRecoveryOfferOutcome.ToString());

        return new WinStickerDiagnosticSnapshot(
            SchemaVersion: 9,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            AppVersion: GetDiagnosticVersion(),
            DistributionChannel: DistributionService.ChannelName,
            IsPackaged: DistributionService.IsPackaged,
            OperatingSystem: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            UiCulture: CultureInfo.CurrentUICulture.Name,
            Hotkeys: new WinStickerHotkeyDiagnostic(
                SettingsService.Settings.GlobalHotkeyEnabled,
                GlobalHotkeyService?.IsRegistered == true,
                (int)toggleGesture.Modifiers,
                toggleGesture.VirtualKey,
                GlobalHotkeyService?.ReceivedCount ?? 0,
                GlobalHotkeyService?.InvocationCount ?? 0,
                GlobalHotkeyService?.DispatchFailureCount ?? 0,
                GlobalHotkeyService?.UsesReservedHook == true,
                GlobalHotkeyService?.ReservedHookThreadId ?? 0,
                GlobalHotkeyService?.ReservedHookLastErrorCode ?? 0,
                GlobalHotkeyService?.ReservedHookTriggerCount ?? 0,
                GlobalHotkeyService?.ReservedHookPostFailureCount ?? 0,
                GlobalHotkeyService?.ReservedHookInputFailureCount ?? 0,
                !string.IsNullOrWhiteSpace(GlobalHotkeyService?.LastError),
                SettingsService.Settings.SearchHotkeyEnabled,
                SearchHotkeyService?.IsRegistered == true,
                (int)searchGesture.Modifiers,
                searchGesture.VirtualKey),
            Settings: new WinStickerSettingsDiagnostic(
                SettingsService.LastLoadRecoveryState,
                SettingsService.HasPendingSave,
                saveFailure?.Operation,
                saveFailure?.OccurredAt.ToUniversalTime()),
            RecoveryOffer: recoveryOffer,
            ShortcutNative: new WinStickerShortcutNativeDiagnostic(
                shortcutNative.SelectedBackend,
                shortcutNative.ModuleName,
                shortcutNative.ModuleExists,
                shortcutNative.ModuleArchitecture,
                shortcutNative.ModuleSha256,
                shortcutNative.LoadAttempted,
                shortcutNative.LoadState,
                shortcutNative.AbiVersion,
                shortcutNative.Capabilities),
            RuntimeHealth: runtimeHealth,
            WidgetManager: widgetManager,
            Displays: displays,
            StorageMigration: WidgetManager?.StorageMigrationDiagnostic,
            StartupLedger: startupLedger,
            CallbackRadar: callbackRadar);
    }

    private static IReadOnlyList<WinStickerDisplayDiagnostic> GetDisplayDiagnostics()
    {
        try
        {
            return Win32Helper.GetMonitorWorkAreaInfos()
                .Select((display, index) => new WinStickerDisplayDiagnostic(
                    index + 1,
                    display.IsPrimary,
                    display.DpiScale,
                    ToDiagnosticRect(display.Monitor),
                    ToDiagnosticRect(display.WorkArea)))
                .ToArray();
        }
        catch (Exception ex)
        {
            Log($"[DiagnosticsBundle] Display enumeration failed: {ex.Message}");
            return [];
        }
    }

    private static WinStickerDiagnosticRect ToDiagnosticRect(Win32Helper.RECT bounds)
    {
        return new WinStickerDiagnosticRect(
            bounds.Left,
            bounds.Top,
            bounds.Right - bounds.Left,
            bounds.Bottom - bounds.Top);
    }

    private static string GetDiagnosticVersion()
    {
        Assembly assembly = typeof(App).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }
}
