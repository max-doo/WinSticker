using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class WinStickerDataPathIsolationTests
{
    [Fact]
    public void ProductionRoot_PreservesLegacyInstanceNamesAndRecoveryLocation()
    {
        var service = new WinStickerDataPathService();

        Assert.False(service.IsDevelopmentRoot);
        Assert.Equal("WinSticker_Activate_Event_7F3A9B2E", service.ActivationEventName);
        Assert.Equal("WinSticker_SingleInstance_Mutex_7F3A9B2E", service.SingleInstanceMutexName);
        Assert.Equal(
            Path.Combine(Path.GetDirectoryName(service.RootPath)!, "WinSticker-Recovery"),
            service.RecoveryDirectory);
    }

    [Fact]
    public void DevelopmentRoot_IsolatesDataRecoveryAndInstanceNames()
    {
        string firstRoot = Path.Combine(Path.GetTempPath(), "WinSticker-Dev-QuickCapture139");
        string secondRoot = Path.Combine(Path.GetTempPath(), "WinSticker-Dev-Todo139");
        var first = new WinStickerDataPathService(firstRoot);
        var same = new WinStickerDataPathService(firstRoot.ToLowerInvariant());
        var second = new WinStickerDataPathService(secondRoot);

        Assert.True(first.IsDevelopmentRoot);
        Assert.Equal($"{first.RootPath}-Recovery", first.RecoveryDirectory);
        Assert.StartsWith(first.RootPath, first.DataDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(first.SingleInstanceMutexName, same.SingleInstanceMutexName);
        Assert.NotEqual(first.SingleInstanceMutexName, second.SingleInstanceMutexName);
        Assert.NotEqual("WinSticker_SingleInstance_Mutex_7F3A9B2E", first.SingleInstanceMutexName);
    }

    [Fact]
    public void DebugEnvironmentVariable_IsPartOfTheStartupContract()
    {
        string source = File.ReadAllText(TestPaths.FromRepository(
            "src/WinSticker/Services/WinStickerDataPathService.cs"));

        Assert.Contains("WINSTICKER_DEV_DATA_ROOT", source, StringComparison.Ordinal);
        Assert.Contains("#if DEBUG", source, StringComparison.Ordinal);
        Assert.Contains("ResolveConfiguredRoot", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/WinSticker/Services/SettingsService.cs")]
    [InlineData("src/WinSticker/Services/QuickCaptureStore.cs")]
    [InlineData("src/WinSticker/Services/TodoWidgetStore.cs")]
    [InlineData("src/WinSticker/Services/SearchHistoryService.cs")]
    [InlineData("src/WinSticker/Services/LegacySearchIndexCleanupService.cs")]
    [InlineData("src/WinSticker/Services/DesktopOrganizationRecoveryStore.cs")]
    public void AppOwnedStorage_UsesSharedDataRoot(string relativePath)
    {
        string source = File.ReadAllText(TestPaths.FromRepository(relativePath));

        Assert.Contains("WinStickerDataPathService.Current", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.SpecialFolder.LocalApplicationData", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DebugLauncher_DefaultsToAWorktreeScopedDataRoot()
    {
        string source = File.ReadAllText(TestPaths.FromRepository("scripts/start-debug.ps1"));

        Assert.Contains("WinSticker-Dev", source, StringComparison.Ordinal);
        Assert.Contains("WINSTICKER_DEV_DATA_ROOT", source, StringComparison.Ordinal);
        Assert.Contains("UseProductionData", source, StringComparison.Ordinal);
        Assert.Contains("ExecutablePath.StartsWith($repoRootPath", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TestHosts_NeverConstructTheProductionPathOrganizerService()
    {
        // The public OrganizerService ctor hardcodes the production
        // suppression ledger and recovery journal; a test using it silently
        // reads and rewrites the real data directory and races the running
        // app. History: unisolated tests once grew the ledger to 8.4 MB and
        // left 27 quarantined corrupt backups. Every test must go through
        // TestOrganizerServices (or the internal ctor with injected paths).
        string testsRoot = TestPaths.FromRepository("tests/WinSticker.Tests");
        // Assembled from fragments so this test's own source does not carry
        // the literal it bans.
        string bannedConstruction = "new " + "OrganizerService(";
        foreach (string file in Directory.EnumerateFiles(
                     testsRoot,
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                Path.GetFileName(file) is "TestOrganizerServices.cs")
            {
                continue;
            }

            string source = File.ReadAllText(file);
            Assert.False(
                source.Contains(bannedConstruction, StringComparison.Ordinal),
                $"{file} constructs OrganizerService directly; use " +
                "TestOrganizerServices.Create so the suppression ledger and " +
                "recovery journal stay off the real data directory.");
        }
    }
}
