using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace WinSticker.Services;

public static class AppRelaunchService
{
    // Store builds do not ship WinSticker.Updater (excluded from Store packaging),
    // so the relaunch is delegated to the WinAppSDK restart agent from the
    // graceful-shutdown tail instead of a detached helper process.
    public static bool PendingOsRestartOnShutdown { get; set; }

    public static AppRelaunchScheduleResult ScheduleAfterCurrentProcessExit()
    {
        if (AppDistributionService.Current.IsMicrosoftStore)
        {
            PendingOsRestartOnShutdown = true;
            return AppRelaunchScheduleResult.StartedSuccessfully;
        }

        string appPath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "WinSticker.exe");
        if (!File.Exists(appPath))
        {
            return AppRelaunchScheduleResult.Failed("WinSticker.exe was not found.");
        }

        try
        {
            string helperPath = AppUpdateService.PrepareDetachedUpdaterHelper(
                AppContext.BaseDirectory,
                WinStickerDataPathService.Current.UpdatesDirectory);
            if (!File.Exists(helperPath))
            {
                return AppRelaunchScheduleResult.Failed("WinSticker.Updater.exe was not found.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = helperPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(helperPath) ?? AppContext.BaseDirectory
            };
            startInfo.ArgumentList.Add("--restart-only");
            startInfo.ArgumentList.Add("--pid");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--app");
            startInfo.ArgumentList.Add(appPath);
            Process.Start(startInfo);
            return AppRelaunchScheduleResult.StartedSuccessfully;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                       InvalidOperationException or Win32Exception)
        {
            App.Log($"[Relaunch] Failed to schedule app restart: {ex}");
            return AppRelaunchScheduleResult.Failed(ex.Message);
        }
    }
}

public sealed record AppRelaunchScheduleResult(bool Started, string? ErrorMessage)
{
    public static AppRelaunchScheduleResult StartedSuccessfully { get; } = new(true, null);

    public static AppRelaunchScheduleResult Failed(string errorMessage) => new(false, errorMessage);
}
