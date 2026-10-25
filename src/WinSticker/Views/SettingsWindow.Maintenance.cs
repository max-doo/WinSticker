using WinSticker.Helpers;
using WinSticker.Platform;
using WinSticker.Services;
using WinSticker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace WinSticker.Views;

public sealed partial class SettingsWindow
{
    private void RefreshDragDropPermissionButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RefreshDragDropPermissionDiagnostic();
    }

    private async void ResyncRuntimeStateButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ResyncRuntimeStateAsync();
    }

    private async void ExportDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsRoot.XamlRoot is null)
        {
            return;
        }

        string? folderPath = await FolderPickerService.PickFolderAsync(_hWnd);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        ExportDiagnosticsButton.IsEnabled = false;
        try
        {
            WinStickerDiagnosticSnapshot snapshot = App.Current.CreateDiagnosticSnapshot();
            string archivePath = await App.Current.DiagnosticsBundleService.ExportAsync(
                folderPath,
                snapshot,
                WinStickerDataPathService.Current.LogFilePath);
            await ShowInfoDialogAsync(
                _localizationService.T("Settings.Diagnostics.SuccessTitle"),
                _localizationService.Format("Settings.Diagnostics.SuccessBody", archivePath));
            Win32Helper.ShowInExplorer(archivePath);
        }
        catch (Exception ex)
        {
            App.Log($"[DiagnosticsBundle] Export failed: {ex}");
            await ShowInfoDialogAsync(
                _localizationService.T("Settings.Diagnostics.FailedTitle"),
                _localizationService.Format("Settings.Diagnostics.FailedBody", ex.Message));
        }
        finally
        {
            ExportDiagnosticsButton.IsEnabled = true;
        }
    }

    private async void RepairDragDropPermissionButton_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsRoot.XamlRoot is null)
        {
            return;
        }

        var result = ViewModel.RepairDragDropPermission();
        if (result.RequiresStartupSettings)
        {
            bool openSystemSettings = await ConfirmDialog.ShowAsync(
                SettingsRoot.XamlRoot,
                _localizationService,
                _localizationService.T("Settings.AutoStart.Title"),
                _localizationService.T("Settings.AutoStart.WindowsDisabled"),
                _localizationService.T("Settings.AutoStart.OpenSystemSettings"),
                ContentDialogButton.Primary);

            if (openSystemSettings)
            {
                await OpenStartupAppsSettingsAsync();            }

            return;
        }

        if (result.NeedsRelaunch)
        {
            bool relaunch = await ConfirmDialog.ShowAsync(
                SettingsRoot.XamlRoot,
                _localizationService,
                _localizationService.T("Settings.DragDropPermission.RelaunchTitle"),
                _localizationService.T("Settings.DragDropPermission.RelaunchBody"),
                _localizationService.T("Settings.DragDropPermission.RelaunchButton"),
                ContentDialogButton.Primary);

            if (relaunch)
            {
                if (DragDropPermissionService.TryRelaunchAsExplorerUser(
                        $"--await-parent-exit {Environment.ProcessId}"))
                {
                    // Same trap as the elevation notice: a bare inline
                    // Application.Exit() is deferred through the XAML message
                    // loop and never consumed. The full shutdown path carries
                    // the deadline and Environment.Exit fallbacks.
                    await App.Current.ShutdownForRestartAsync();
                }
                else
                {
                    await ShowInfoDialogAsync(
                        _localizationService.T("Settings.DragDropPermission.RelaunchFailedTitle"),
                        _localizationService.T("Settings.DragDropPermission.RelaunchFailedBody"));
                }
            }

            return;
        }

        await ShowInfoDialogAsync(
            _localizationService.T(result.Success
                ? "Settings.DragDropPermission.RepairCompleteTitle"
                : "Settings.DragDropPermission.RepairFailedTitle"),
            result.Success
                ? _localizationService.Format("Settings.DragDropPermission.RepairCompleteBody", result.RepairedCount)
                : result.FailureMessage);
    }

    private async void OpenUacSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => Win32Helper.OpenFile("UserAccountControlSettings.exe"));
    }

    private async void ExportDataBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsRoot.XamlRoot is null || _backupCommands.IsStopping)
        {
            return;
        }

        string? folderPath = await FolderPickerService.PickFolderAsync(_hWnd);
        if (string.IsNullOrWhiteSpace(folderPath) || _isClosed || _backupCommands.IsStopping)
        {
            return;
        }

        ExportDataBackupButton.IsEnabled = false;
        try
        {
            string backupPath = await _backupCommands.ExportAsync(folderPath);
            if (_isClosed || _backupCommands.IsStopping) return;
            await ShowInfoDialogAsync(
                _localizationService.T("Settings.DataBackup.SuccessTitle"),
                _localizationService.Format("Settings.DataBackup.SuccessBody", backupPath));
            Win32Helper.ShowInExplorer(backupPath);
        }
        catch (OperationCanceledException) when (_backupCommands.IsStopping) { }
        catch (Exception ex)
        {
            App.Log($"[DataBackup] Manual export failed: {ex}");
            if (_isClosed || _backupCommands.IsStopping) return;
            await ShowInfoDialogAsync(
                _localizationService.T("Settings.DataBackup.FailedTitle"),
                _localizationService.Format("Settings.DataBackup.FailedBody", ex.Message));
        }
        finally
        {
            if (!_isClosed && !_backupCommands.IsStopping) ExportDataBackupButton.IsEnabled = true;
        }
    }

    private async void RestoreDataBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsRoot.XamlRoot is null)
        {
            return;
        }

        string? backupPath;
        try
        {
            backupPath = await FileOpenPickerService.PickSingleFileAsync(
                _hWnd,
                [".zip"],
                PickerLocationId.DocumentsLibrary);
        }
        catch (Exception ex)
        {
            App.Log($"[DataBackup] Restore picker failed: {ex}");
            await ShowInfoDialogAsync(
                _localizationService.T("Settings.DataBackup.RestoreFailedTitle"),
                _localizationService.Format("Settings.DataBackup.RestoreFailedBody", FormatBackupError(ex)));
            return;
        }

        if (string.IsNullOrWhiteSpace(backupPath))
        {
            return;
        }

        await RestoreDataBackupFromPathAsync(backupPath);
    }

    private async Task RestoreDataBackupFromPathAsync(string archivePath)
    {
        if (SettingsRoot.XamlRoot is null)
        {
            return;
        }

        RestoreDataBackupButton.IsEnabled = false;
        ExportDataBackupButton.IsEnabled = false;
        bool restartScheduled = false;
        try
        {
            WinStickerRestorePreparation preparation = await App.Current.DataBackupService.PrepareRestoreAsync(
                archivePath);
            string integrityWarning = preparation.HasIntegrityManifest
                ? string.Empty
                : $"\n\n{_localizationService.T("Settings.DataBackup.LegacyIntegrityWarning")}";
            string newerVersionWarning = preparation.IsFromNewerAppVersion
                ? $"\n\n{_localizationService.Format(
                    "Settings.DataBackup.RestoreNewerVersionWarning",
                    preparation.AppVersion)}"
                : string.Empty;
            bool restoreConfirmed = await ConfirmDialog.ShowAsync(
                SettingsRoot.XamlRoot,
                _localizationService,
                _localizationService.T("Settings.DataBackup.RestoreConfirmTitle"),
                _localizationService.Format(
                    "Settings.DataBackup.RestoreConfirmBody",
                    preparation.BackupCreatedAtUtc.ToLocalTime().ToString("g"),
                    preparation.AppVersion,
                    preparation.FileCount,
                    ViewModel.FormatBytes(preparation.TotalUncompressedBytes),
                    integrityWarning + newerVersionWarning),
                _localizationService.T("Settings.DataBackup.RestoreConfirmButton"));

            if (!restoreConfirmed)
            {
                await App.Current.DataBackupService.CancelPendingRestoreAsync();
                return;
            }

            // The pending marker was written before this dialog opened;
            // persist the confirmation now so a crash, hidden window, or
            // exit between here and the relaunch discards the restore at
            // the next launch instead of applying it unconfirmed.
            if (!await App.Current.DataBackupService.ConfirmPendingFullRestoreAsync())
            {
                await App.Current.DataBackupService.CancelPendingRestoreAsync();
                await ShowInfoDialogAsync(
                    _localizationService.T("Settings.DataBackup.RestoreFailedTitle"),
                    _localizationService.Format("Settings.DataBackup.RestoreFailedBody", string.Empty));
                return;
            }

            AppRelaunchScheduleResult relaunch = AppRelaunchService.ScheduleAfterCurrentProcessExit();
            if (!relaunch.Started)
            {
                await App.Current.DataBackupService.CancelPendingRestoreAsync();
                await ShowInfoDialogAsync(
                    _localizationService.T("Settings.DataBackup.RestartFailedTitle"),
                    _localizationService.Format(
                        "Settings.DataBackup.RestartFailedBody",
                        relaunch.ErrorMessage ?? string.Empty));
                return;
            }

            restartScheduled = true;
            await App.Current.ShutdownForRestartAsync();
        }
        catch (Exception ex)
        {
            App.Log($"[DataBackup] Restore preparation failed: {ex}");
            if (!restartScheduled)
            {
                await App.Current.DataBackupService.CancelPendingRestoreAsync();
            }
            await ShowInfoDialogAsync(
                _localizationService.T("Settings.DataBackup.RestoreFailedTitle"),
                _localizationService.Format("Settings.DataBackup.RestoreFailedBody", FormatBackupError(ex)));
        }
        finally
        {
            if (!restartScheduled && !_isClosed)
            {
                RestoreDataBackupButton.IsEnabled = true;
                ExportDataBackupButton.IsEnabled = true;
            }
        }
    }

    private async void CreateBackupSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        if (_backupCommands.IsStopping) return;
        CreateBackupSnapshotButton.IsEnabled = false;
        RefreshBackupSnapshotsButton.IsEnabled = false;
        try
        {
            var result = await _backupCommands.CreateSnapshotNowAsync();
            if (_isClosed || _backupCommands.IsStopping) return;
            string? snapshotPath = result.ArchivePath;
            if (snapshotPath is null)
            {
                await ShowInfoDialogAsync(
                    _localizationService.T("Settings.DataBackup.FailedTitle"),
                    _localizationService.Format(
                        "Settings.DataBackup.FailedBody",
                        result.Error ?? _localizationService.T("Settings.DataBackup.Snapshots.Empty")));
                return;
            }

            await RefreshBackupSnapshotInventoryAsync();
            Win32Helper.ShowInExplorer(snapshotPath);
        }
        catch (OperationCanceledException) when (_backupCommands.IsStopping) { }
        catch (Exception ex)
        {
            App.Log($"[DataBackup] Immediate snapshot failed: {ex}");
            if (_isClosed || _backupCommands.IsStopping) return;
            await ShowInfoDialogAsync(
                _localizationService.T("Settings.DataBackup.FailedTitle"),
                _localizationService.Format("Settings.DataBackup.FailedBody", ex.Message));
        }
        finally
        {
            if (!_isClosed && !_backupCommands.IsStopping)
            {
                CreateBackupSnapshotButton.IsEnabled = true;
                RefreshBackupSnapshotsButton.IsEnabled = true;
            }
        }
    }

    private async void ChangeAutomaticBackupDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsRoot.XamlRoot is null)
        {
            return;
        }

        string? folderPath = await FolderPickerService.PickFolderAsync(_hWnd);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        if (!_backupSettingsViewModel.IsValidLocalDirectory(
                folderPath,
                out string? rejectionReasonKey))
        {
            await ShowInfoDialogAsync(
                _localizationService.T("Settings.DataBackup.AutomaticBackupDirectory.InvalidTitle"),
                _localizationService.T(rejectionReasonKey ?? "Settings.DataBackup.AutomaticBackupDirectory.InvalidPath"));
            return;
        }

        _backupSettingsViewModel.UpdateLocalDirectory(folderPath);
        await RefreshBackupSnapshotInventoryAsync();
    }

    private async void ResetAutomaticBackupDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        _backupSettingsViewModel.UpdateLocalDirectory(string.Empty);
        await RefreshBackupSnapshotInventoryAsync();
    }

    private void OpenBackupFolderButton_Click(object sender, RoutedEventArgs e)
    {
        // With a custom folder configured, open it directly; otherwise keep the
        // pre-existing behavior of opening the recovery root that contains the
        // default "automatic" snapshot folder.
        string directory = _backupSettingsViewModel.State.LocalOpenDirectory;
        Directory.CreateDirectory(directory);
        Win32Helper.ShowInExplorer(directory);
    }

    private async void RefreshBackupSnapshotsButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshBackupSnapshotInventoryAsync();
    }

    private async Task RefreshBackupSnapshotInventoryAsync()
    {
        if (_isRefreshingBackupSnapshots || BackupSnapshotsList is null)
        {
            return;
        }

        _isRefreshingBackupSnapshots = true;
        RefreshBackupSnapshotsButton.IsEnabled = false;
        try
        {
            IReadOnlyList<WinStickerBackupSnapshotInfo> snapshots =
                await App.Current.DataBackupService.GetSnapshotInventoryAsync();
            var rows = snapshots.Select(snapshot =>
            {
                string kind = snapshot.Kind == "pre-restore"
                    ? _localizationService.T("Settings.DataBackup.Snapshots.PreRestore")
                    : _localizationService.T("Settings.DataBackup.Snapshots.Automatic");
                string status = snapshot.IsReadable
                    ? _localizationService.T("Settings.DataBackup.Snapshots.Readable")
                    : _localizationService.T("Settings.DataBackup.Snapshots.Unreadable");
                string title = $"{kind} · {snapshot.CreatedAtUtc.ToLocalTime():g}";
                string details = $"{ViewModel.FormatBytes(snapshot.SizeBytes)} · {status}\n{Path.GetFileName(snapshot.Path)}";
                return new BackupSnapshotListItem(snapshot.Path, title, details, snapshot.IsReadable);
            }).ToArray();

            BackupSnapshotsList.ItemsSource = rows.Cast<object>().ToArray();
            BackupSnapshotSummaryText.Text = rows.Length == 0
                ? _localizationService.T("Settings.DataBackup.Snapshots.Empty")
                : _localizationService.Format(
                    "Settings.DataBackup.Snapshots.Summary",
                    rows.Length,
                    ViewModel.FormatBytes(snapshots.Sum(snapshot => snapshot.SizeBytes)),
                    snapshots[0].CreatedAtUtc.ToLocalTime().ToString("g"));
        }
        catch (Exception ex)
        {
            App.Log($"[DataBackup] Snapshot inventory failed: {ex}");
            BackupSnapshotsList.ItemsSource = null;
            BackupSnapshotSummaryText.Text = _localizationService.Format(
                "Settings.DataBackup.FailedBody",
                ex.Message);
        }
        finally
        {
            RefreshBackupSnapshotsButton.IsEnabled = true;
            _isRefreshingBackupSnapshots = false;
        }
    }

    private async void RestoreSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: BackupSnapshotListItem item } && item.CanRestore)
        {
            await RestoreDataBackupFromPathAsync(item.Path);
        }
    }

    private async void DeleteSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: BackupSnapshotListItem item } ||
            !await ConfirmDialog.ShowAsync(
                SettingsRoot.XamlRoot,
                _localizationService,
                _localizationService.T("Settings.DataBackup.Snapshots.DeleteConfirmTitle"),
                _localizationService.Format(
                    "Settings.DataBackup.Snapshots.DeleteConfirmBody",
                    item.Title),
                _localizationService.T("Settings.DataBackup.Snapshots.Delete")))
        {
            return;
        }

        try
        {
            await App.Current.DataBackupService.DeleteSnapshotAsync(item.Path);
            await RefreshBackupSnapshotInventoryAsync();
        }
        catch (Exception ex)
        {
            App.Log($"[DataBackup] Snapshot deletion failed: {ex}");
            await ShowInfoDialogAsync(
                _localizationService.T("Settings.DataBackup.FailedTitle"),
                _localizationService.Format("Settings.DataBackup.FailedBody", ex.Message));
        }
    }

    private async void CheckAttachmentHealthButton_Click(object sender, RoutedEventArgs e)
    {
        CheckAttachmentHealthButton.IsEnabled = false;
        AttachmentHealthSummaryText.Text = _localizationService.T("Settings.AttachmentHealth.Checking");
        try
        {
            WinStickerAttachmentHealthReport report = await App.Current.AttachmentHealthService.ScanAsync();
            string key = report.UnreadableStoreCount > 0
                ? "Settings.AttachmentHealth.Partial"
                : report.IsHealthy
                    ? "Settings.AttachmentHealth.Healthy"
                    : "Settings.AttachmentHealth.Issues";
            AttachmentHealthSummaryText.Text = _localizationService.Format(
                key,
                report.ReferencedFileCount,
                report.MissingLinkedFiles.Count,
                report.MissingManagedFiles.Count,
                report.OrphanManagedFiles.Count,
                report.UnreadableStoreCount);
        }
        catch (Exception ex)
        {
            App.Log($"[AttachmentHealth] Scan failed: {ex}");
            AttachmentHealthSummaryText.Text = _localizationService.Format(
                "Settings.AttachmentHealth.Failed",
                ex.Message);
        }
        finally
        {
            CheckAttachmentHealthButton.IsEnabled = true;
        }
    }

    private async void RestoreDefaultSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsRoot.XamlRoot is null)
        {
            return;
        }

        bool restore = await ConfirmDialog.ShowAsync(
            SettingsRoot.XamlRoot,
            _localizationService,
            _localizationService.T("Settings.Dialog.RestoreTitle"),
            _localizationService.T("Settings.Dialog.RestoreBody"),
            _localizationService.T("Common.Restore"));

        if (!restore)
        {
            return;
        }

        await ViewModel.RestoreDefaultPreferencesAsync();
    }

}
