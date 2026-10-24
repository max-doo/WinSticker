namespace WinSticker.Tests;

public sealed class InstallerUninstallContractTests
{
    private static readonly string[] InstallerLanguages =
    [
        "english",
        "chinesesimplified",
        "chinesetraditional",
        "japanese",
        "german",
        "brazilianportuguese",
        "hindi",
        "spanish",
        "french",
        "arabic",
        "bengali",
        "russian",
        "korean"
    ];

    private static readonly string[] AppDataChoiceMessages =
    [
        "AppDataChoiceTitle",
        "ConfirmRemoveAppData",
        "KeepAppDataButton",
        "RemoveAppDataButton",
        "AppDataCleanupFailed"
    ];

    private static readonly string[] ManagedStorageShortcutMessages =
    [
        "ManagedStorageShortcutPrompt",
        "ManagedStorageShortcutCreateFailed"
    ];

    private static readonly string[] StoreEditionMessages =
    [
        "StoreEditionDataPreserved",
        "StoreEditionStateUnknown"
    ];

    private static readonly string[] DependencyMessages =
    [
        "DependencyDownloadCancelled",
        "DependencyDownloadFailed",
        "DependencyDownloadFailedSummary",
        "DependencyInstallStartFailed",
        "DependencyInstallFailed",
        "DependencyInstallFailedSummary",
        "RuntimeDependencyComment"
    ];

    [Fact]
    public void Uninstall_OffersSafeKeepOrPurgeChoice()
    {
        string code = ReadRepositoryFile("installer/WinSticker.Uninstall.iss");

        Assert.Contains("ChooseAppDataRemoval", code, StringComparison.Ordinal);
        Assert.Contains("SuppressibleTaskDialogMsgBox", code, StringComparison.Ordinal);
        Assert.Contains("IDYES", code, StringComparison.Ordinal);
        Assert.Contains("WinStickerPurgeUserDataParameter = '/PURGEUSERDATA'", code, StringComparison.Ordinal);
        Assert.Contains("WinStickerAppDataRootPath = '{localappdata}\\WinSticker'", code, StringComparison.Ordinal);
        Assert.Contains("WinStickerRecoveryRootPath = '{localappdata}\\WinSticker-Recovery'", code, StringComparison.Ordinal);
        Assert.Contains("WinStickerTemporaryRootPath = '{%TEMP}\\WinSticker'", code, StringComparison.Ordinal);
        Assert.Contains("MB_YESNO or MB_DEFBUTTON2", code, StringComparison.Ordinal);
        Assert.Contains("RemoveNotificationRegistration", code, StringComparison.Ordinal);
        Assert.Contains("if ActivatorId <> '' then", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Format(ExpandConstant('{cm:", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DelTree(GetManagedStorageRootPath", code, StringComparison.Ordinal);
        Assert.Contains("OfferManagedStorageShortcut", code, StringComparison.Ordinal);
        Assert.Contains("managedStorageDesktopShortcutPath", code, StringComparison.Ordinal);
        Assert.Contains("GetManagedStorageShortcutPath", code, StringComparison.Ordinal);
        Assert.DoesNotContain("managedStorageDesktopShortcutEnabled", code, StringComparison.Ordinal);
        Assert.DoesNotContain("if not ShortcutEnabled", code, StringComparison.Ordinal);
        Assert.Contains("if FileExists(ExpandConstant(WinStickerDataSettingsPath)) then", code, StringComparison.Ordinal);
        int adminShortcutGuard = code.IndexOf(
            "if FileExists(ExpandConstant(WinStickerDataSettingsPath)) then",
            code.IndexOf("if IsAdminInstallMode then", StringComparison.Ordinal),
            StringComparison.Ordinal);
        int adminShortcutOffer = code.IndexOf("OfferManagedStorageShortcut", adminShortcutGuard, StringComparison.Ordinal);
        int adminBranchResult = code.IndexOf("Result := True;", adminShortcutGuard, StringComparison.Ordinal);
        Assert.True(
            adminShortcutGuard >= 0 &&
            adminShortcutOffer > adminShortcutGuard &&
            adminBranchResult > adminShortcutOffer,
            "The default all-users uninstall path should offer the current user's managed-storage shortcut when that profile has WinSticker settings.");
        Assert.Contains("CreateOleObject('WScript.Shell')", code, StringComparison.Ordinal);
        Assert.Contains("ShortcutObject.TargetPath := FolderPath", code, StringComparison.Ordinal);
        Assert.Contains("MB_YESNO or MB_DEFBUTTON1", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Uninstall_RemovesOnlyTheStartupTaskOwnedByCurrentInstall()
    {
        string code = ReadRepositoryFile("installer/WinSticker.Uninstall.iss");

        Assert.Contains(
            "WinStickerStartupTaskNamePrefix = 'WinSticker User Startup'",
            code,
            StringComparison.Ordinal);
        Assert.Contains("CreateOleObject('Schedule.Service')", code, StringComparison.Ordinal);
        Assert.Contains("TaskDefinition.Actions", code, StringComparison.Ordinal);
        Assert.Contains(
            "SameInstallPath(ExtractFileDir(ActionPath), ExpandConstant('{app}'))",
            code,
            StringComparison.Ordinal);
        Assert.Contains(
            "CompareText(ExtractFileName(ActionPath), WinStickerProcessName)",
            code,
            StringComparison.Ordinal);
        Assert.Contains(
            "RootFolder.DeleteTask(TaskName, 0)",
            code,
            StringComparison.Ordinal);
        Assert.True(
            code.LastIndexOf("RemoveStartupScheduledTasks;", StringComparison.Ordinal) <
            code.LastIndexOf("RemoveStartupRegistryEntry;", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("installer/WinSticker.iss")]
    [InlineData("installer/WinSticker.arm64.iss")]
    public void DirectInstaller_SupportsMachineAndCurrentUserScopes(string scriptPath)
    {
        string installer = ReadRepositoryFile(scriptPath);
        string installation = ReadRepositoryFile("installer/WinSticker.Installation.iss");
        string migration = ReadRepositoryFile("installer/WinSticker.Migration.iss");

        Assert.Contains("PrivilegesRequired=admin", installer, StringComparison.Ordinal);
        Assert.Contains("PrivilegesRequiredOverridesAllowed=dialog commandline", installer, StringComparison.Ordinal);
        Assert.Contains("UsePreviousPrivileges=yes", installer, StringComparison.Ordinal);
        Assert.Contains("{autoprograms}", installer, StringComparison.Ordinal);
        Assert.Contains("{autodesktop}", installer, StringComparison.Ordinal);
        Assert.Contains("Root: HKA; Subkey: \"Software\\WinSticker\\DirectInstall\"", installer, StringComparison.Ordinal);
        Assert.Contains("ValueName: \"InstallScope\"", installer, StringComparison.Ordinal);
        Assert.Contains("if (CurStep = ssPostInstall) and (not IsAdminInstallMode) then", installer, StringComparison.Ordinal);
        Assert.DoesNotContain("Root: HKCU; Subkey: \"Software\\WinSticker\"; ValueType: string; ValueName: \"InstallLanguage\"", installer, StringComparison.Ordinal);
        Assert.Contains("ExpandConstant('{autopf}\\WinSticker')", installation, StringComparison.Ordinal);
        Assert.Contains("HKEY_LOCAL_MACHINE, WinStickerInstallStateKey", installation, StringComparison.Ordinal);
        Assert.DoesNotContain("DelTree(LegacyInstallPath", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("WinStickerAdminCleanupParam", migration, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("installer/WinSticker.Installation.iss")]
    [InlineData("installer/WinSticker.Dependencies.iss")]
    [InlineData("installer/WinSticker.Dependencies.arm64.iss")]
    public void InstallerCustomMessagePlaceholders_UseFmtMessage(string path)
    {
        string code = ReadRepositoryFile(path);

        Assert.DoesNotContain("Format(ExpandConstant('{cm:", code, StringComparison.Ordinal);
    }

    [Fact]
    public void UninstallChoice_IsLocalizedForEveryInstallerLanguage()
    {
        string messages =
            ReadRepositoryFile("installer/WinSticker.iss") + Environment.NewLine +
            ReadRepositoryFile("installer/WinSticker.NewLanguageCustomMessages.iss");

        foreach (string language in InstallerLanguages)
        {
            foreach (string message in AppDataChoiceMessages)
            {
                Assert.Contains(
                    $"{language}.{message}=",
                    messages,
                    StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void DependencyMessages_AreLocalizedForEveryInstallerLanguage()
    {
        string messages = ReadRepositoryFile("installer/WinSticker.DependencyCustomMessages.iss");

        foreach (string language in InstallerLanguages)
        {
            foreach (string message in DependencyMessages)
            {
                Assert.Contains(
                    $"{language}.{message}=",
                    messages,
                    StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void FullRetailInstallers_SkipExternalDependenciesAndKeepStandardNames()
    {
        foreach (string script in new[] { "installer/WinSticker.iss", "installer/WinSticker.arm64.iss" })
        {
            string content = ReadRepositoryFile(script);
            Assert.Contains("#ifndef WinStickerBundledRuntime", content, StringComparison.Ordinal);
            Assert.Contains("#define MyAppPackageSuffix \"\"", content, StringComparison.Ordinal);
            Assert.Contains("{#MyAppPackageSuffix}", content, StringComparison.Ordinal);
            Assert.Contains("WinSticker.InstallManifest.txt", content, StringComparison.Ordinal);
            Assert.Contains("BeforeInstall: CleanupWinStickerInstall", content, StringComparison.Ordinal);
            Assert.Contains(
                $"OutputBaseFilename={{#MyAppOutputBaseName}}_{{#MyAppVersion}}_{(script.EndsWith("arm64.iss", StringComparison.Ordinal) ? "arm64" : "x64")}",
                content,
                StringComparison.Ordinal);
            Assert.DoesNotContain("Type: filesandordirs; Name: \"{app}\\Microsoft.WindowsAppRuntime\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "#define MyAppPackageSuffix \"_Full\"",
                content,
                StringComparison.Ordinal);
        }

        string distribution = ReadRepositoryFile("scripts/build-stage-7c1-distribution.ps1");
        Assert.Contains("\"/DWinStickerBundledRuntime=1\"", distribution, StringComparison.Ordinal);
        Assert.Contains(
            "$installerOutputBaseName = \"{0}_{1}_{2}\"",
            distribution,
            StringComparison.Ordinal);
        Assert.DoesNotContain("_Full.exe", distribution, StringComparison.Ordinal);

        foreach (string dependencyScript in new[]
                 {
                     "installer/WinSticker.Dependencies.iss",
                     "installer/WinSticker.Dependencies.arm64.iss"
                 })
        {
            string content = ReadRepositoryFile(dependencyScript);
            Assert.Contains("#if WinStickerBundledRuntime", content, StringComparison.Ordinal);
            Assert.Contains("external runtime dependency setup skipped", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NativeShortcutModule_IsPackagedByX64AndArm64NativeAotInstallers()
    {
        string x64 = ReadRepositoryFile("installer/WinSticker.iss");
        string arm64 = ReadRepositoryFile("installer/WinSticker.arm64.iss");
        string retailPublish = ReadRepositoryFile("scripts/publish-aot-retail.ps1");
        const string nativeSource =
            "Source: \"{#MyAppReleaseDir}\\winsticker_native.dll\"; DestDir: \"{app}\"; Flags: ignoreversion";
        const string nativeExclusions =
            "Excludes: \"WinSticker.Updater.*,winsticker_native.dll,winsticker_native.pdb\"";

        Assert.Contains("-p:WinStickerRustNative=true", retailPublish, StringComparison.Ordinal);
        Assert.Contains(nativeExclusions, x64, StringComparison.Ordinal);
        Assert.Contains(nativeSource, x64, StringComparison.Ordinal);

        Assert.Contains(nativeExclusions, arm64, StringComparison.Ordinal);
        Assert.Contains("#if WinStickerNativeAot", arm64, StringComparison.Ordinal);
        Assert.Contains(nativeSource, arm64, StringComparison.Ordinal);
    }

    [Fact]
    public void ManagedStorageShortcutPrompt_IsLocalizedForEveryInstallerLanguage()
    {
        string messages = ReadRepositoryFile("installer/WinSticker.UninstallCustomMessages.iss");

        foreach (string language in InstallerLanguages)
        {
            foreach (string message in ManagedStorageShortcutMessages)
            {
                Assert.Contains(
                    $"{language}.{message}=",
                    messages,
                    StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Uninstall_StoreEditionProbe_IsThreeStateWithPlatformPrecheck()
    {
        string code = ReadRepositoryFile("installer/WinSticker.Uninstall.iss");

        Assert.Contains("GetStoreWinStickerInstallState", code, StringComparison.Ordinal);
        Assert.Contains("StoreWinStickerStateNotInstalled", code, StringComparison.Ordinal);
        Assert.Contains("StoreWinStickerStateInstalled", code, StringComparison.Ordinal);
        Assert.Contains("StoreWinStickerStateUnknown", code, StringComparison.Ordinal);
        Assert.Contains("Result := StoreWinStickerStateUnknown;", code, StringComparison.Ordinal);
        Assert.DoesNotContain("IsStoreWinStickerInstalled", code, StringComparison.Ordinal);

        // The platform pre-check must decide whether AppX can exist at all
        // with native Inno Setup probes only, without spawning another process.
        Assert.Contains(
            "DirExists(ExpandConstant('{win}\\System32\\WindowsApps'))",
            code,
            StringComparison.Ordinal);
        Assert.Contains(
            "RegKeyExists(HKEY_LOCAL_MACHINE, 'SYSTEM\\CurrentControlSet\\Services\\AppXSvc')",
            code,
            StringComparison.Ordinal);

        // The PowerShell probe must stay scoped to the current user's
        // package registration and keep its documented exit-code contract.
        Assert.Contains("Get-AppxPackage -Name", code, StringComparison.Ordinal);
        Assert.Contains("exit 1", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Uninstall_StoreEditionUnknownState_FailsClosed()
    {
        string code = ReadRepositoryFile("installer/WinSticker.Uninstall.iss");

        // Both the /PURGEUSERDATA path and the interactive keep-or-remove path
        // must branch on the tri-state probe and treat Unknown exactly like
        // installed: the purge stays blocked and the dedicated message is shown.
        Assert.Equal(2, CountOccurrences(code, "case GetStoreWinStickerInstallState of"));
        Assert.Equal(2, CountOccurrences(code, "StoreWinStickerStateUnknown:"));
        Assert.Equal(2, CountOccurrences(code, "{cm:StoreEditionStateUnknown}"));
        Assert.Contains("{cm:StoreEditionDataPreserved}", code, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreEditionMessages_AreLocalizedForEveryInstallerLanguage()
    {
        string messages = ReadRepositoryFile("installer/WinSticker.UninstallCustomMessages.iss");

        foreach (string language in InstallerLanguages)
        {
            foreach (string message in StoreEditionMessages)
            {
                Assert.Contains(
                    $"{language}.{message}=",
                    messages,
                    StringComparison.Ordinal);
            }
        }

        // The corrected copy must not claim the two editions share one
        // application-data directory: the Store edition keeps its data in its
        // own MSIX LocalCache and this uninstaller never touches it.
        Assert.DoesNotContain(
            "Both editions share the same application data",
            messages,
            StringComparison.Ordinal);
        Assert.DoesNotContain("两个版本共用同一应用数据目录", messages, StringComparison.Ordinal);
    }

    [Fact]
    public void UninstallCustomMessages_KeySetsAreAlignedAcrossLanguages()
    {
        string messages = ReadRepositoryFile("installer/WinSticker.UninstallCustomMessages.iss");
        string code = ReadRepositoryFile("installer/WinSticker.Uninstall.iss");
        var tables = InstallerLanguages.ToDictionary(
            language => language,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (string line in messages.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            int dotIndex = line.IndexOf('.');
            int equalsIndex = line.IndexOf('=');
            if (dotIndex <= 0 || equalsIndex <= dotIndex)
            {
                continue;
            }

            string language = line[..dotIndex].Trim();
            if (tables.TryGetValue(language, out HashSet<string>? keys))
            {
                keys.Add(line[(dotIndex + 1)..equalsIndex].Trim());
            }
        }

        HashSet<string> english = tables["english"];
        Assert.Contains("StoreEditionDataPreserved", english);
        Assert.Contains("StoreEditionStateUnknown", english);
        foreach ((string language, HashSet<string> keys) in tables)
        {
            Assert.Equal(
                english.OrderBy(key => key, StringComparer.Ordinal),
                keys.OrderBy(key => key, StringComparer.Ordinal));
        }

        foreach (string message in StoreEditionMessages)
        {
            Assert.Contains("{cm:" + message + "}", code, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CustomMessageKeysAndPlaceholders_AreAlignedAcrossLanguages()
    {
        string messages = string.Join(
            Environment.NewLine,
            ReadRepositoryFile("installer/WinSticker.iss"),
            ReadRepositoryFile("installer/WinSticker.NewLanguageCustomMessages.iss"),
            ReadRepositoryFile("installer/WinSticker.UninstallCustomMessages.iss"),
            ReadRepositoryFile("installer/WinSticker.DependencyCustomMessages.iss"));
        var tables = InstallerLanguages.ToDictionary(
            language => language,
            _ => new Dictionary<string, string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (string line in messages.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            int equalsIndex = line.IndexOf('=');
            int dotIndex = line.IndexOf('.');
            if (dotIndex <= 0 || equalsIndex <= dotIndex)
            {
                continue;
            }

            string language = line[..dotIndex].Trim();
            if (!tables.TryGetValue(language, out Dictionary<string, string>? table))
            {
                continue;
            }

            string key = line[(dotIndex + 1)..equalsIndex].Trim();
            Assert.True(table.TryAdd(key, line[(equalsIndex + 1)..]), $"Duplicate {language}.{key}");
        }

        Dictionary<string, string> english = tables["english"];
        foreach ((string language, Dictionary<string, string> table) in tables)
        {
            Assert.Equal(
                english.Keys.OrderBy(key => key, StringComparer.Ordinal),
                table.Keys.OrderBy(key => key, StringComparer.Ordinal));

            foreach ((string key, string englishValue) in english)
            {
                Assert.Equal(
                    GetInnoPlaceholders(englishValue),
                    GetInnoPlaceholders(table[key]));
            }
        }
    }

    [Fact]
    public void TraditionalChinese_IsImmediatelyAfterSimplifiedChinese()
    {
        string simplified =
            "Name: \"chinesesimplified\"; MessagesFile: \"Languages\\ChineseSimplified.isl\"";
        string traditional =
            "Name: \"chinesetraditional\"; MessagesFile: \"Languages\\ChineseTraditional.isl\"";

        foreach (string script in new[] { "installer/WinSticker.iss", "installer/WinSticker.arm64.iss" })
        {
            string content = ReadRepositoryFile(script);
            int simplifiedIndex = content.IndexOf(simplified, StringComparison.Ordinal);
            int traditionalIndex = content.IndexOf(traditional, StringComparison.Ordinal);

            Assert.True(simplifiedIndex >= 0);
            Assert.True(traditionalIndex > simplifiedIndex);
            Assert.Contains(
                "#include \"WinSticker.DependencyCustomMessages.iss\"",
                content,
                StringComparison.Ordinal);
            Assert.Contains(
                "AppComments={cm:RuntimeDependencyComment}",
                content,
                StringComparison.Ordinal);
        }

        string languageFile = ReadRepositoryFile("installer/Languages/ChineseTraditional.isl");
        Assert.Contains("LanguageName=繁體中文", languageFile, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("hindi", "hi-IN")]
    [InlineData("spanish", "es-ES")]
    [InlineData("french", "fr-FR")]
    [InlineData("arabic", "ar-SA")]
    [InlineData("bengali", "bn-BD")]
    [InlineData("russian", "ru-RU")]
    [InlineData("korean", "ko-KR")]
    [InlineData("chinesetraditional", "zh-TW")]
    public void InstallerLanguage_IsPassedToFirstAppLaunch(
        string installerLanguage,
        string appLanguage)
    {
        foreach (string script in new[] { "installer/WinSticker.iss", "installer/WinSticker.arm64.iss" })
        {
            string content = ReadRepositoryFile(script);
            Assert.Contains(
                $"ActiveLanguage = '{installerLanguage}' then Result := '{appLanguage}'",
                content,
                StringComparison.Ordinal);
            Assert.Contains(
                "#include \"WinSticker.Uninstall.iss\"",
                content,
                StringComparison.Ordinal);
        }
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string[] GetInnoPlaceholders(string value)
    {
        return System.Text.RegularExpressions.Regex.Matches(value, @"%(?:n|\d+)")
            .Select(match => match.Value)
            .OrderBy(placeholder => placeholder, StringComparer.Ordinal)
            .ToArray();
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName,
                    "src",
                    "WinSticker",
                    "WinSticker.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("WinSticker repository root was not found.");
    }
}
