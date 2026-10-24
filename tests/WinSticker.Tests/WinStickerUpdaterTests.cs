namespace WinSticker.Tests;

public sealed class WinStickerUpdaterTests
{
    [Fact]
    public void InstallerArguments_ShowProgressAndLockTheExistingDirectory()
    {
        const string InstallDirectory = @"D:\Apps\WinSticker";

        IReadOnlyList<string> arguments = WinSticker.Updater.Program.BuildInstallerArguments(
            InstallDirectory,
            silent: true,
            WinSticker.Updater.DirectInstallScope.CurrentUser);

        Assert.Contains($"/DIR={InstallDirectory}", arguments);
        Assert.Contains("/CURRENTUSER", arguments);
        Assert.DoesNotContain("/ALLUSERS", arguments);
        Assert.Contains("/SILENT", arguments);
        Assert.Contains("/SP-", arguments);
        Assert.Contains("/NORESTART", arguments);
        Assert.Contains("/FORCECLOSEAPPLICATIONS", arguments);
        Assert.DoesNotContain("/VERYSILENT", arguments);
        Assert.DoesNotContain("/SUPPRESSMSGBOXES", arguments);
        Assert.DoesNotContain(arguments, argument =>
            argument.StartsWith("/LANG=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MachineWideInstallerArguments_PreserveAllUsersScope()
    {
        IReadOnlyList<string> arguments = WinSticker.Updater.Program.BuildInstallerArguments(
            @"C:\Program Files\WinSticker",
            silent: true,
            WinSticker.Updater.DirectInstallScope.AllUsers);

        Assert.Contains("/ALLUSERS", arguments);
        Assert.DoesNotContain("/CURRENTUSER", arguments);
    }

    [Fact]
    public void SilentInstallerArguments_PinTheAppLanguageWhenProvided()
    {
        IReadOnlyList<string> arguments = WinSticker.Updater.Program.BuildInstallerArguments(
            @"D:\Apps\WinSticker",
            silent: true,
            WinSticker.Updater.DirectInstallScope.CurrentUser,
            installerLanguage: "english");

        Assert.Contains("/LANG=english", arguments);
    }

    [Fact]
    public void SilentInstallerArguments_OmitLanguageWhenNoneResolved()
    {
        IReadOnlyList<string> arguments = WinSticker.Updater.Program.BuildInstallerArguments(
            @"D:\Apps\WinSticker",
            silent: true,
            WinSticker.Updater.DirectInstallScope.CurrentUser,
            installerLanguage: "");

        Assert.DoesNotContain(arguments, argument =>
            argument.StartsWith("/LANG=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InteractiveInstallerArguments_NeverPinLanguage()
    {
        // A matching /LANG suppresses Inno's Select Language dialog even in
        // interactive runs, which would take the language choice away from a
        // user running Setup manually. The pin must stay silent-only.
        IReadOnlyList<string> arguments = WinSticker.Updater.Program.BuildInstallerArguments(
            @"D:\Apps\WinSticker",
            silent: false,
            WinSticker.Updater.DirectInstallScope.CurrentUser,
            installerLanguage: "english");

        Assert.DoesNotContain("/SILENT", arguments);
        Assert.DoesNotContain(arguments, argument =>
            argument.StartsWith("/LANG=", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("english", "english")]
    [InlineData("English", "english")]
    [InlineData("chinesesimplified", "chinesesimplified")]
    [InlineData("chinesetraditional", "chinesetraditional")]
    [InlineData("japanese", "japanese")]
    [InlineData("german", "german")]
    [InlineData("brazilianportuguese", "brazilianportuguese")]
    [InlineData("hindi", "hindi")]
    [InlineData("spanish", "spanish")]
    [InlineData("french", "french")]
    [InlineData("arabic", "arabic")]
    [InlineData("bengali", "bengali")]
    [InlineData("russian", "russian")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("zh-CN", "")]
    [InlineData("not-a-language", "")]
    [InlineData("english /SILENT", "")]
    public void InstallerLanguageResolver_WhitelistsKnownInstallerNamesOnly(
        string? requested,
        string expected)
    {
        Assert.Equal(expected, WinSticker.Updater.Program.ResolveInstallerLanguage(requested));
    }

    [Fact]
    public void ProgramFilesInstallWithoutRegistration_FallsBackToAllUsersScope()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        Assert.False(string.IsNullOrWhiteSpace(programFiles));

        string unregisteredPath = Path.Combine(
            programFiles,
            $"WinSticker-Scope-Probe-{Guid.NewGuid():N}");

        Assert.Equal(
            WinSticker.Updater.DirectInstallScope.AllUsers,
            WinSticker.Updater.Program.ResolveInstallScope(unregisteredPath));
    }

    [Fact]
    public void PerUserInstallWithoutRegistration_DefaultsToCurrentUserScope()
    {
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        Assert.False(string.IsNullOrWhiteSpace(localAppData));

        string unregisteredPath = Path.Combine(
            localAppData,
            $"WinSticker-Scope-Probe-{Guid.NewGuid():N}");

        Assert.Equal(
            WinSticker.Updater.DirectInstallScope.CurrentUser,
            WinSticker.Updater.Program.ResolveInstallScope(unregisteredPath));
    }

    [Theory]
    [InlineData(2, "cancelled")]
    [InlineData(5, "cancelled")]
    [InlineData(20, "path-mismatch")]
    [InlineData(1, "failed")]
    [InlineData(99, "failed")]
    public void IncompleteInstallerExitCode_MapsToRecoveryOutcome(int exitCode, string expected)
    {
        Assert.Equal(expected, WinSticker.Updater.Program.GetIncompleteUpdateOutcome(exitCode));
    }
}
