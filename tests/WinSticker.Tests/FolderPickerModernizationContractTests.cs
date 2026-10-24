using System.Text.RegularExpressions;
using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class FolderPickerModernizationContractTests
{
    [Fact]
    public void FolderPickerService_UsesModernWindowIdPickerWithoutLegacyCom()
    {
        string source = ReadSource("src/WinSticker/Services/FolderPickerService.cs");

        Assert.Contains("using Microsoft.Windows.Storage.Pickers;", source, StringComparison.Ordinal);
        Assert.Contains(
            "public static async Task<string?> PickFolderAsync(IntPtr ownerHwnd)",
            source,
            StringComparison.Ordinal);
        Assert.Contains("Win32Interop.GetWindowIdFromWindow(ownerHwnd)", source, StringComparison.Ordinal);
        Assert.Contains("new FolderPicker(ownerWindowId)", source, StringComparison.Ordinal);
        Assert.Contains("await picker.PickSingleFolderAsync()", source, StringComparison.Ordinal);
        Assert.Contains("result?.Path", source, StringComparison.Ordinal);

        Assert.DoesNotContain("ComImport", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IFileOpenDialog", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FileOpenDialog", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StartDialogTopMostMonitor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("dialog.Show(ownerHwnd)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FolderPickerService_RejectsZeroOwnerBeforeWinRtActivation()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => FolderPickerService.ValidateOwnerWindowHandle(IntPtr.Zero));

        Assert.Equal("ownerHwnd", exception.ParamName);
    }

    [Fact]
    public void FileOpenPickerService_OffersOwnerAwareSingleAndMultipleFilePicks()
    {
        string service = ReadSource("src/WinSticker/Services/FileOpenPickerService.cs");

        Assert.Contains(
            "public static async Task<string?> PickSingleFileAsync(",
            service,
            StringComparison.Ordinal);
        Assert.Contains(
            "public static async Task<IReadOnlyList<string>> PickMultipleFilesAsync(",
            service,
            StringComparison.Ordinal);
        Assert.Contains("await picker.PickSingleFileAsync()", service, StringComparison.Ordinal);
        Assert.Contains("await picker.PickMultipleFilesAsync()", service, StringComparison.Ordinal);
    }

    [Fact]
    public void FileOpenPickerService_RejectsZeroOwnerBeforeWinRtActivation()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => FileOpenPickerService.ValidateOwnerWindowHandle(IntPtr.Zero));

        Assert.Equal("ownerHwnd", exception.ParamName);
    }

    [Fact]
    public void ProductCode_RetiresTheLegacyWinRtPickerStack()
    {
        // The legacy Windows.Storage.Pickers stack needs InitializeWithWindow
        // bridging and shows a differently-behaved dialog; every product
        // entrance now goes through the owner-aware WinAppSDK pickers.
        string[] legacyLiterals =
        [
            "using Windows.Storage.Pickers;",
            "Windows.Storage.Pickers.FileOpenPicker",
            "Windows.Storage.Pickers.FileSavePicker",
            "Windows.Storage.Pickers.FolderPicker",
        ];
        string sourceRoot = TestPaths.FromRepository("src/WinSticker");
        foreach (string file in Directory.EnumerateFiles(
                     sourceRoot,
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            string source = File.ReadAllText(file);
            foreach (string literal in legacyLiterals)
            {
                Assert.False(
                    source.Contains(literal, StringComparison.Ordinal),
                    $"{file} still uses the legacy picker stack: {literal}");
            }
        }
    }

    [Fact]
    public void AllProductEntrances_AwaitTheOwnerAwarePicker()
    {
        var expectedCalls = new Dictionary<string, int>
        {
            ["src/WinSticker/App.Tray.cs"] = 1,
            ["src/WinSticker/Services/JumpListService.cs"] = 1,
            ["src/WinSticker/Views/SettingsWindow.Maintenance.cs"] = 3,
            ["src/WinSticker/Views/SettingsWindow.StorageAndUpdates.cs"] = 1
        };

        int totalCalls = 0;
        foreach ((string path, int expectedCount) in expectedCalls)
        {
            string source = ReadSource(path);
            int callCount = Regex.Matches(
                source,
                @"await\s+FolderPickerService\.PickFolderAsync\s*\(").Count;

            Assert.Equal(expectedCount, callCount);
            Assert.DoesNotContain("FolderPickerService.PickFolder(", source, StringComparison.Ordinal);
            Assert.False(
                Regex.IsMatch(
                    source,
                    @"FolderPickerService\.PickFolderAsync\s*\(\s*IntPtr\.Zero"),
                $"{path} still opens FolderPicker without an owner window.");
            totalCalls += callCount;
        }

        // The v12 clock patch dropped its local-folder wallpaper entrance, so
        // the product now has six owner-aware picker call sites.
        Assert.Equal(6, totalCalls);
    }

    [Fact]
    public void TrayAndJumpListEntrances_UseTheStableTrayOwnerWindow()
    {
        string tray = ReadSource("src/WinSticker/App.Tray.cs");
        string jumpList = ReadSource("src/WinSticker/Services/JumpListService.cs");

        Assert.Contains("internal IntPtr GetFolderPickerOwnerWindowHandle()", tray, StringComparison.Ordinal);
        Assert.Contains("WindowNative.GetWindowHandle(_trayWindow)", tray, StringComparison.Ordinal);
        Assert.Contains("Win32Helper.IsWindow(ownerHwnd)", tray, StringComparison.Ordinal);
        Assert.Contains("GetFolderPickerOwnerWindowHandle());", tray, StringComparison.Ordinal);
        Assert.Contains("app.GetFolderPickerOwnerWindowHandle());", jumpList, StringComparison.Ordinal);
    }

    [Fact]
    public void TrayOwnerResolution_RejectsCloakedOwnersAndFallsBackToAFreshHelper()
    {
        string tray = ReadSource("src/WinSticker/App.Tray.cs");

        // A cloaked owner (its virtual desktop is not the active one) makes
        // the owned picker dialog invisible, so the tray entrance must gate
        // every candidate on DWMWA_CLOAKED and keep a self-healing helper.
        Assert.Contains("PickerOwnerResolutionPolicy.Resolve(", tray, StringComparison.Ordinal);
        Assert.Contains("Win32Helper.IsWindowCloaked(", tray, StringComparison.Ordinal);
        Assert.Contains("RecreateFolderPickerHelperWindow()", tray, StringComparison.Ordinal);
        Assert.Contains("_folderPickerHelperWindow", tray, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath) =>
        File.ReadAllText(TestPaths.FromRepository(relativePath));
}
