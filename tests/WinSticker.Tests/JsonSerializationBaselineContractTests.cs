using System.Text.Json;
using System.Text.RegularExpressions;
using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class JsonSerializationBaselineContractTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "WinSticker.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ProductionInventory_IsFrozenAtFortyTwoFilesAndNinetyEightCalls()
    {
        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["src/WinSticker/App.AotHotkeySmoke.cs"] = 1,
            ["src/WinSticker/App.AotShortcutSmoke.cs"] = 1,
            ["src/WinSticker/App.AotShellSmoke.cs"] = 1,
            ["src/WinSticker/App.AotQuickAccessMutationSmoke.cs"] = 1,
            ["src/WinSticker/App.AotMusicVolumeReadSmoke.cs"] = 1,
            ["src/WinSticker/App.AotMusicVolumeMutationSmoke.cs"] = 2,
            ["src/WinSticker/App.AotMusicVolumeSessionMutationSmoke.cs"] = 2,
            ["src/WinSticker/App.AotManagedUiSmoke.cs"] = 1,
            ["src/WinSticker/App.AotTodoRecurrenceReminderSmoke.cs"] = 1,
            ["src/WinSticker/App.AotTodoNotificationLifecycleSmoke.cs"] = 1,
            ["src/WinSticker/App.AotTodoNotificationActivationSmoke.cs"] = 1,
            ["src/WinSticker/App.AotTodoNotificationForwardingSmoke.cs"] = 1,
            // 3: update-check manifest, GitHub release fallback, and the
            // display-only content-manifest fetch (release-notes window).
            ["src/WinSticker/Services/AppUpdateService.cs"] = 3,
            ["src/WinSticker/Services/CitySearchService.cs"] = 1,
            ["src/WinSticker/Services/WinStickerAttachmentHealthService.cs"] = 1,
            ["src/WinSticker/Services/WinStickerDataBackupService.cs"] = 12,
            ["src/WinSticker/Services/WinStickerDiagnosticsBundleService.cs"] = 1,
            ["src/WinSticker/Services/DesktopAutoOrganizationSuppressionRegistry.cs"] = 2,
            ["src/WinSticker/Services/DesktopOrganizationHistoryStore.cs"] = 2,
            ["src/WinSticker/Services/DesktopOrganizationRecoveryStore.cs"] = 2,
            ["src/WinSticker/Services/FeedbackService.cs"] = 4,
            ["src/WinSticker/Services/GlanceWidgetStore.cs"] = 7,
            ["src/WinSticker/Services/LocalizationService.cs"] = 1,
            ["src/WinSticker/Services/ManagedStorageMigrationService.cs"] = 2,
            ["src/WinSticker/Services/NativeNotificationActivationEnvelopeStore.cs"] = 2,
            // 2: the note-file metadata header (serialize + deserialize).
            ["src/WinSticker/Services/NoteFileCodec.cs"] = 2,
            ["src/WinSticker/Services/QuickCaptureStore.cs"] = 2,
            // 2: migration progress journal + recovery-copy info sidecar.
            ["src/WinSticker/Services/QuickCaptureNoteMigration.cs"] = 2,
            // 2: the wipe-recovery offer marker (read deserialize, atomic save serialize).
            ["src/WinSticker/Services/RecoveryOfferStore.cs"] = 2,
            ["src/WinSticker/Services/SearchHistoryService.cs"] = 2,
            ["src/WinSticker/Services/SettingsMigrationService.cs"] = 2,
            ["src/WinSticker/Services/SettingsService.cs"] = 3,
            // Startup session ledger: read-previous + write-current breadcrumb.
            ["src/WinSticker/Services/StartupSessionLedger.cs"] = 2,
            // Crash event report serialization for the support package.
            ["src/WinSticker/Services/WindowsCrashEventCollector.cs"] = 1,
            ["src/WinSticker/Services/SyncOutboxStore.cs"] = 2,
            ["src/WinSticker/Services/SyncRevisionsStore.cs"] = 2,
            ["src/WinSticker/Services/SyncStateStore.cs"] = 2,
            ["src/WinSticker/Services/TodoWidgetStore.cs"] = 3,
            ["src/WinSticker/Services/WeatherService.cs"] = 5,
            ["src/WinSticker/Services/WidgetFileStackSettings.cs"] = 7,
            ["src/WinSticker/Services/WidgetLayoutStore.cs"] = 2,
            // 3: the title-icon asset store's recent-emoji state file
            // (load deserialize, add deserialize, atomic save serialize).
            ["src/WinSticker/Services/WidgetTitleIconAssetStore.cs"] = 3
        };

        Dictionary<string, int> actual = ProductionSourceFiles()
            .Select(path => new
            {
                Path = RepositoryRelativePath(path),
                Count = Regex.Matches(
                    File.ReadAllText(path),
                    @"JsonSerializer\.(?:SerializeToUtf8Bytes|Serialize|Deserialize)(?:Async)?\b").Count
            })
            .Where(item => item.Count > 0)
            .ToDictionary(item => item.Path, item => item.Count, StringComparer.Ordinal);

        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach ((string path, int expectedCount) in expected)
        {
            Assert.Equal(expectedCount, actual[path]);
        }

        Assert.Equal(42, actual.Count);
        Assert.Equal(98, actual.Values.Sum());

        string[] expectedContextOwners =
        [
            "src/WinSticker/App.AotHotkeySmoke.cs",
            "src/WinSticker/App.AotManagedUiSmoke.cs",
            "src/WinSticker/App.AotMusicVolumeMutationSmoke.cs",
            "src/WinSticker/App.AotMusicVolumeReadSmoke.cs",
            "src/WinSticker/App.AotMusicVolumeSessionMutationSmoke.cs",
            "src/WinSticker/App.AotQuickAccessMutationSmoke.cs",
            "src/WinSticker/App.AotShellSmoke.cs",
            "src/WinSticker/App.AotShortcutSmoke.cs",
            "src/WinSticker/App.AotTodoNotificationActivationSmoke.cs",
            "src/WinSticker/App.AotTodoNotificationForwardingSmoke.cs",
            "src/WinSticker/App.AotTodoNotificationLifecycleSmoke.cs",
            "src/WinSticker/App.AotTodoRecurrenceReminderSmoke.cs",
            "src/WinSticker/Services/AppUpdateService.cs",
            "src/WinSticker/Services/DesktopOrganizationHistoryStore.cs",
            "src/WinSticker/Services/DesktopOrganizationRecoveryStore.cs",
            "src/WinSticker/Services/FeedbackService.cs",
            "src/WinSticker/Services/GlanceWidgetStore.cs",
            "src/WinSticker/Services/LocalizationService.cs",
            "src/WinSticker/Services/ManagedStorageMigrationModels.cs",
            "src/WinSticker/Services/NativeNotificationActivationEnvelopeStore.cs",
            "src/WinSticker/Services/NoteFileCodec.cs",
            "src/WinSticker/Services/QuickCaptureStore.cs",
            "src/WinSticker/Services/RecoveryOfferStore.cs",
            "src/WinSticker/Services/SearchHistoryService.cs",
            "src/WinSticker/Services/SettingsService.cs",
            "src/WinSticker/Services/StartupSessionLedger.cs",
            "src/WinSticker/Services/SyncRevisionsStore.cs",
            "src/WinSticker/Services/SyncStateStore.cs",
            "src/WinSticker/Services/TodoWidgetStore.cs",
            "src/WinSticker/Services/WeatherService.cs",
            "src/WinSticker/Services/WidgetFileStackSettings.cs",
            "src/WinSticker/Services/WidgetLayoutStore.cs",
            "src/WinSticker/Services/WidgetTitleIconAssetStore.cs",
            "src/WinSticker/Services/WindowsCrashEventCollector.cs",
            "src/WinSticker/Services/WinStickerDataBackupService.cs",
            "src/WinSticker/Services/WinStickerDiagnosticsBundleService.cs",
            "src/WinSticker/Sync/SyncJsonContext.cs"
        ];
        string[] actualContextOwners = ProductionSourceFiles()
            .Where(path => File.ReadAllText(path).Contains(
                "JsonSerializerContext",
                StringComparison.Ordinal))
            .Select(RepositoryRelativePath)
            .Order()
            .ToArray();

        Assert.Equal(37, actualContextOwners.Length);
        Assert.Equal(expectedContextOwners, actualContextOwners);
    }

    [Fact]
    public void NonGenericStringEnumConverters_AreEliminated()
    {
        var converterPattern = new Regex(@"new\s+JsonStringEnumConverter\s*\(\s*\)");
        string[] actualOwners = ProductionSourceFiles()
            .Where(path => converterPattern.IsMatch(File.ReadAllText(path)))
            .Select(RepositoryRelativePath)
            .Order()
            .ToArray();
        int converterCount = ProductionSourceFiles()
            .Sum(path => converterPattern.Matches(File.ReadAllText(path)).Count);

        Assert.Empty(actualOwners);
        Assert.Equal(0, converterCount);
        Assert.DoesNotContain(
            ProductionSourceFiles(),
            path => File.ReadAllText(path).Contains("JsonStringEnumConverter<", StringComparison.Ordinal));
    }

    [Fact]
    public void PhaseOneLeafCalls_UseSourceGeneratedTypeInfoAndPreserveOptionProfiles()
    {
        string appUpdate = ReadSource("src/WinSticker/Services/AppUpdateService.cs");
        string citySearch = ReadSource("src/WinSticker/Services/CitySearchService.cs");
        string weather = ReadSource("src/WinSticker/Services/WeatherService.cs");
        string localization = ReadSource("src/WinSticker/Services/LocalizationService.cs");
        string diagnostics = ReadSource(
            "src/WinSticker/Services/WinStickerDiagnosticsBundleService.cs");

        string[] expectedTypeInfoReferences =
        [
            "AppUpdateJsonContext.Default.GitHubRelease",
            "WeatherJsonContext.Default.PredefinedCityList",
            "WeatherJsonContext.Default.GeocodingResult",
            "WeatherJsonContext.Default.OpenMeteoWeather",
            "WeatherJsonContext.Default.MsnWeather",
            "LocalizationJsonContext.Default.LocalizedStrings",
            "DiagnosticsJsonContext.Default.DiagnosticSnapshot"
        ];
        string allPhaseOneSources = string.Join(
            Environment.NewLine,
            appUpdate,
            citySearch,
            weather,
            localization,
            diagnostics);

        foreach (string reference in expectedTypeInfoReferences)
        {
            Assert.Single(
                Regex.Matches(allPhaseOneSources, Regex.Escape(reference)).Cast<Match>());
        }

        // The update manifest deserializes twice: once for the installer
        // check and once for the display-only content fetch.
        Assert.Equal(
            2,
            Regex.Matches(allPhaseOneSources, Regex.Escape("AppUpdateJsonContext.Default.UpdateManifest")).Count);

        Assert.DoesNotContain("JsonSerializerOptions", allPhaseOneSources, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new JsonStringEnumConverter()",
            allPhaseOneSources,
            StringComparison.Ordinal);

        Assert.Contains("JsonSerializerDefaults.Web", appUpdate, StringComparison.Ordinal);
        Assert.Contains("PropertyNameCaseInsensitive = true", appUpdate, StringComparison.Ordinal);
        Assert.Contains("PropertyNameCaseInsensitive = true", weather, StringComparison.Ordinal);
        Assert.DoesNotContain("DictionaryKeyPolicy", localization, StringComparison.Ordinal);
        Assert.Contains(
            "PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase",
            diagnostics,
            StringComparison.Ordinal);
        Assert.Contains("UseStringEnumConverter = true", diagnostics, StringComparison.Ordinal);
        Assert.Contains("WriteIndented = true", diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public void PhaseTwoStoreCalls_UseSourceGeneratedTypeInfoAndPreserveOptionProfiles()
    {
        string settings = ReadSource("src/WinSticker/Services/SettingsService.cs");
        string quickCapture = ReadSource("src/WinSticker/Services/QuickCaptureStore.cs");
        string todo = ReadSource("src/WinSticker/Services/TodoWidgetStore.cs");
        string glancePreferences = ReadSource("src/WinSticker/Services/GlanceWidgetStore.cs");
        string widgetMetadata = ReadSource(
            "src/WinSticker/Services/WidgetFileStackSettings.cs");
        string allPhaseTwoSources = string.Join(
            Environment.NewLine,
            settings,
            quickCapture,
            todo,
            glancePreferences,
            widgetMetadata);

        var expectedTypeInfoReferences = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["SettingsJsonContext.Default.AppSettings"] = 3,
            ["QuickCaptureJsonContext.Default.StoreData"] = 2,
            ["TodoJsonContext.Default.StoreData"] = 3,
            ["GlancePreferencesJsonContext.Default.Preferences"] = 7,
            ["WidgetMetadataJsonContext.Default.StringMap"] = 2,
            ["WidgetMetadataJsonContext.Default.StringListMap"] = 2,
            ["WidgetMetadataJsonContext.Default.StringList"] = 3
        };
        foreach ((string reference, int expectedCount) in expectedTypeInfoReferences)
        {
            Assert.Equal(
                expectedCount,
                Regex.Matches(
                    allPhaseTwoSources,
                    Regex.Escape(reference) + @"\b").Count);
        }

        Assert.Equal(22, expectedTypeInfoReferences.Values.Sum());
        Assert.DoesNotContain("JsonSerializerOptions", allPhaseTwoSources, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new JsonStringEnumConverter()",
            allPhaseTwoSources,
            StringComparison.Ordinal);

        foreach (string stringEnumStore in
                 new[] { settings, quickCapture, todo, glancePreferences })
        {
            Assert.Contains(
                "PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase",
                stringEnumStore,
                StringComparison.Ordinal);
            Assert.Contains("UseStringEnumConverter = true", stringEnumStore, StringComparison.Ordinal);
            Assert.Contains("WriteIndented = true", stringEnumStore, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("PropertyNamingPolicy", widgetMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("DictionaryKeyPolicy", widgetMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("UseStringEnumConverter", widgetMetadata, StringComparison.Ordinal);
    }

    [Fact]
    public void PhaseThreeASearchAndRecoveryCalls_UseSourceGeneratedTypeInfoAndPreserveOptionProfiles()
    {
        string searchHistory = ReadSource("src/WinSticker/Services/SearchHistoryService.cs");
        string desktopRecovery = ReadSource(
            "src/WinSticker/Services/DesktopOrganizationRecoveryStore.cs");
        string allPhaseThreeASources = string.Join(
            Environment.NewLine,
            searchHistory,
            desktopRecovery);

        var expectedTypeInfoReferences = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["SearchHistoryJsonContext.Default.PersistedData"] = 2,
            ["DesktopRecoveryJsonContext.Default.RecoveryJournal"] = 2
        };
        foreach ((string reference, int expectedCount) in expectedTypeInfoReferences)
        {
            Assert.Equal(
                expectedCount,
                Regex.Matches(
                    allPhaseThreeASources,
                    Regex.Escape(reference) + @"\b").Count);
        }

        Assert.Equal(4, expectedTypeInfoReferences.Values.Sum());
        Assert.DoesNotContain("JsonSerializerOptions", allPhaseThreeASources, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonStringEnumConverter", allPhaseThreeASources, StringComparison.Ordinal);

        foreach (string camelCaseSource in new[] { searchHistory, desktopRecovery })
        {
            Assert.Contains(
                "PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase",
                camelCaseSource,
                StringComparison.Ordinal);
            Assert.Contains(
                "GenerationMode = JsonSourceGenerationMode.Metadata",
                camelCaseSource,
                StringComparison.Ordinal);
        }

        Assert.Contains("WriteIndented = true", searchHistory, StringComparison.Ordinal);
        Assert.Contains("WriteIndented = true", desktopRecovery, StringComparison.Ordinal);
        Assert.DoesNotContain("UseStringEnumConverter", searchHistory, StringComparison.Ordinal);
        Assert.DoesNotContain("UseStringEnumConverter", desktopRecovery, StringComparison.Ordinal);
    }

    [Fact]
    public void PhaseThreeBUserDataCalls_UseCompatibleSourceGeneratedTypeInfo()
    {
        string attachmentHealth = ReadSource(
            "src/WinSticker/Services/WinStickerAttachmentHealthService.cs");
        string backup = ReadSource("src/WinSticker/Services/WinStickerDataBackupService.cs");

        var expectedAttachmentTypeInfoReferences = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["s_quickCaptureDataJsonContext.StoreData"] = 1,
            ["s_todoDataJsonContext.StoreData"] = 1
        };
        var expectedBackupTypeInfoReferences = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["s_settingsDataJsonContext.AppSettings"] = 1,
            // +3 each: ValidateScopedRestoreData validates the same store
            // types for cloud-backup domain archives,
            // CountStagedDomainItems reuses them for the restore preview,
            // and CountStagedAttachmentReferences for the dangling-
            // attachment warning count.
            ["s_quickCaptureDataJsonContext.StoreData"] = 6,
            ["s_todoDataJsonContext.StoreData"] = 6
        };
        foreach ((string reference, int expectedCount) in expectedAttachmentTypeInfoReferences)
        {
            Assert.Equal(
                expectedCount,
                Regex.Matches(attachmentHealth, Regex.Escape(reference) + @"\b").Count);
        }

        foreach ((string reference, int expectedCount) in expectedBackupTypeInfoReferences)
        {
            Assert.Equal(
                expectedCount,
                Regex.Matches(backup, Regex.Escape(reference) + @"\b").Count);
        }

        Assert.Equal(
            15,
            expectedAttachmentTypeInfoReferences.Values.Sum() +
            expectedBackupTypeInfoReferences.Values.Sum());
        foreach (string source in new[] { attachmentHealth, backup })
        {
            Assert.Contains("WriteIndented = true", source, StringComparison.Ordinal);
            Assert.Contains(
                "PropertyNamingPolicy = JsonNamingPolicy.CamelCase",
                source,
                StringComparison.Ordinal);
            Assert.Contains("PropertyNameCaseInsensitive = true", source, StringComparison.Ordinal);
            Assert.DoesNotContain("JsonStringEnumConverter", source, StringComparison.Ordinal);
            Assert.Contains("JsonTypeInfo<T> jsonTypeInfo", source, StringComparison.Ordinal);
        }

        Assert.Contains(
            "JsonSerializer.Deserialize(File.ReadAllText(path), jsonTypeInfo)",
            attachmentHealth,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (JsonSerializer.Deserialize(json, jsonTypeInfo) is null)",
            backup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PhaseThreeCBackupControlCalls_UseSourceGeneratedTypeInfoAndPreserveOptionProfile()
    {
        string backup = ReadSource("src/WinSticker/Services/WinStickerDataBackupService.cs");
        int contextStart = backup.IndexOf("[JsonSourceGenerationOptions(", StringComparison.Ordinal);
        Assert.True(contextStart >= 0);
        string backupContext = backup[contextStart..];

        var expectedTypeInfoReferences = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["BackupJsonContext.Default.BackupManifest"] = 3,
            ["BackupJsonContext.Default.PendingRestoreMarker"] = 3
        };
        foreach ((string reference, int expectedCount) in expectedTypeInfoReferences)
        {
            Assert.Equal(
                expectedCount,
                Regex.Matches(backup, Regex.Escape(reference) + @"\b").Count);
        }

        Assert.Equal(6, expectedTypeInfoReferences.Values.Sum());
        Assert.Contains("public sealed partial class WinStickerDataBackupService", backup);
        Assert.Contains(
            "GenerationMode = JsonSourceGenerationMode.Metadata",
            backupContext,
            StringComparison.Ordinal);
        Assert.Contains(
            "PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase",
            backupContext,
            StringComparison.Ordinal);
        Assert.Contains("WriteIndented = true", backupContext, StringComparison.Ordinal);
        Assert.DoesNotContain("PropertyNameCaseInsensitive", backupContext, StringComparison.Ordinal);
        Assert.DoesNotContain("UseStringEnumConverter", backupContext, StringComparison.Ordinal);
        Assert.Contains(
            "typeof(WinStickerBackupManifest),\r\n" +
            "        TypeInfoPropertyName = \"BackupManifest\"",
            backupContext.ReplaceLineEndings("\r\n"),
            StringComparison.Ordinal);
        Assert.Contains(
            "typeof(WinStickerBackupFileManifest),\r\n" +
            "        TypeInfoPropertyName = \"BackupFileManifest\"",
            backupContext.ReplaceLineEndings("\r\n"),
            StringComparison.Ordinal);
        Assert.Contains(
            "typeof(PendingRestoreMarker),\r\n" +
            "        TypeInfoPropertyName = \"PendingRestoreMarker\"",
            backupContext.ReplaceLineEndings("\r\n"),
            StringComparison.Ordinal);
        Assert.DoesNotContain("s_jsonOptions", backup, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "JsonSerializer.DeserializeAsync<WinStickerBackupManifest>",
            backup,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "JsonSerializer.Deserialize<PendingRestoreMarker>",
            backup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GenericJsonHelpers_HaveFiniteFrozenTypeWhitelists()
    {
        string attachmentHealth = ReadSource(
            "src/WinSticker/Services/WinStickerAttachmentHealthService.cs");
        string backup = ReadSource("src/WinSticker/Services/WinStickerDataBackupService.cs");

        Assert.Equal(
            ["QuickCaptureStoreData", "TodoWidgetData"],
            GenericTypeArguments(attachmentHealth, "ReadJson"));
        Assert.Equal(
            ["AppSettings", "DesktopOrganizationHistoryData", "QuickCaptureStoreData", "TodoWidgetData", "WidgetLayoutDocument"],
            GenericTypeArguments(backup, "ValidateJsonFileIfPresent"));
        Assert.Contains(
            "private static T ReadJson<T>(string path, JsonTypeInfo<T> jsonTypeInfo)",
            attachmentHealth,
            StringComparison.Ordinal);
        Assert.Contains(
            "private static void ValidateJsonFileIfPresent<T>(\r\n" +
            "        string path,\r\n" +
            "        JsonTypeInfo<T> jsonTypeInfo)",
            backup.ReplaceLineEndings("\r\n"),
            StringComparison.Ordinal);

        // Six call sites: classic prepare + scoped cloud prepare + the
        // pre-restore safety backup pin + the confirm-dialog item-merge
        // mode update + the full-restore confirmation update + the
        // scoped-restore attempt-count persistence (part of the
        // bounded-retry abandon cap). All must still go through the same
        // atomic-write helper.
        Assert.Equal(
            6,
            Regex.Matches(
                backup,
                @"await\s+WritePendingRestoreMarkerAtomicallyAsync\s*\(").Cast<Match>().Count());
        Assert.Contains(
            "private static async Task WritePendingRestoreMarkerAtomicallyAsync(",
            backup,
            StringComparison.Ordinal);
        Assert.Contains("var marker = new PendingRestoreMarker(", backup, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteJsonAtomicallyAsync", backup, StringComparison.Ordinal);
        Assert.DoesNotContain("WritePendingRestoreMarkerAtomicallyAsync<T>", backup, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchHistoryGolden_KeepsCamelCaseNumericEnumAndForwardCompatibility()
    {
        Directory.CreateDirectory(_tempRoot);
        string storePath = Path.Combine(_tempRoot, "search-history.json");
        var history = new SearchHistoryService(storePath);
        history.RecordResult(new SearchResultItem
        {
            Kind = SearchResultKind.Folder,
            Title = "Saved folder",
            DetailPath = @"C:\Saved"
        });

        using (JsonDocument saved = JsonDocument.Parse(File.ReadAllText(storePath)))
        {
            Assert.True(saved.RootElement.TryGetProperty("recentResults", out JsonElement results));
            Assert.False(saved.RootElement.TryGetProperty("RecentResults", out _));
            JsonElement result = Assert.Single(results.EnumerateArray());
            Assert.Equal(JsonValueKind.Number, result.GetProperty("kind").ValueKind);
            Assert.Equal((int)SearchResultKind.Folder, result.GetProperty("kind").GetInt32());
        }

        File.WriteAllText(
            storePath,
            """
            {
              "recentResults": [
                {
                  "identity": "folder|C:\\Saved",
                  "kind": 4,
                  "title": "Legacy folder",
                  "futureResultField": "ignored"
                }
              ],
              "futureRootField": { "enabled": true }
            }
            """);

        var reloaded = new SearchHistoryService(storePath);
        SearchRecommendationItem recent = Assert.Single(reloaded.RecentResults);
        Assert.Equal(SearchResultKind.Folder, recent.Kind);
        Assert.Equal("Legacy folder", recent.Title);
        Assert.Empty(reloaded.RecentQueries);
        Assert.Empty(reloaded.FavoriteQueries);
    }

    [Fact]
    public async Task DesktopRecoveryGolden_KeepsCamelCaseAndMissingUnknownFieldBehavior()
    {
        Directory.CreateDirectory(_tempRoot);
        string journalPath = Path.Combine(_tempRoot, "desktop-recovery.json");
        var store = new DesktopOrganizationRecoveryStore(journalPath);
        await store.SaveAsync(new DesktopOrganizationRecoveryJournal
        {
            TransactionId = "current",
            StartedAt = DateTimeOffset.Parse("2026-08-20T00:00:00Z"),
            CreatedWidgetIds = ["widget"],
            Items =
            [
                new DesktopOrganizationRecoveryItem
                {
                    SourcePath = @"C:\Source\one.txt",
                    DestinationPath = @"C:\Target\one.txt",
                    TargetWidgetId = "widget",
                    Completed = true
                }
            ]
        });

        string savedJson = await File.ReadAllTextAsync(journalPath);
        Assert.Contains("\n  \"transactionId\"", savedJson, StringComparison.Ordinal);
        using (JsonDocument saved = JsonDocument.Parse(savedJson))
        {
            Assert.True(saved.RootElement.TryGetProperty("createdWidgetIds", out _));
            Assert.False(saved.RootElement.TryGetProperty("CreatedWidgetIds", out _));
        }

        await File.WriteAllTextAsync(
            journalPath,
            """
            {
              "transactionId": "legacy",
              "items": [
                {
                  "sourcePath": "C:\\Source\\legacy.txt",
                  "destinationPath": "C:\\Target\\legacy.txt",
                  "targetWidgetId": "legacy-widget",
                  "completed": true,
                  "futureItemField": 1
                }
              ],
              "futureRootField": true
            }
            """);

        DesktopOrganizationRecoveryJournal loaded = Assert.IsType<DesktopOrganizationRecoveryJournal>(
            await store.LoadAsync());
        Assert.Equal("legacy", loaded.TransactionId);
        Assert.NotEqual(default, loaded.StartedAt);
        Assert.Empty(loaded.CreatedWidgetIds);
        Assert.True(Assert.Single(loaded.Items).Completed);
    }

    [Fact]
    public async Task StringEnumStoreGoldens_WriteNamesAndReadLegacyIntegers()
    {
        string quickDirectory = Path.Combine(_tempRoot, "quick");
        var quickStore = new QuickCaptureStore(quickDirectory);
        await quickStore.SaveAsync(new QuickCaptureStoreData
        {
            CurrentView = QuickCaptureViewMode.Pinned
        });

        using (JsonDocument quickJson = JsonDocument.Parse(
                   await File.ReadAllTextAsync(quickStore.StorePath)))
        {
            JsonElement currentView = quickJson.RootElement.GetProperty("currentView");
            Assert.Equal(JsonValueKind.String, currentView.ValueKind);
            Assert.Equal("Pinned", currentView.GetString());
            Assert.False(quickJson.RootElement.TryGetProperty("CurrentView", out _));
        }

        await File.WriteAllTextAsync(
            quickStore.StorePath,
            """
            {
              "currentView": 2,
              "futureRootField": "ignored"
            }
            """);
        QuickCaptureStoreData legacyQuick = await new QuickCaptureStore(quickDirectory).LoadAsync();
        Assert.Equal(QuickCaptureViewMode.Recent, legacyQuick.CurrentView);
        Assert.Equal(6, legacyQuick.Version);
        Assert.Empty(legacyQuick.Items);
        Assert.Empty(legacyQuick.RecentItems);

        var glanceStore = new GlanceWidgetStore(Path.Combine(_tempRoot, "glance"));
        await glanceStore.SaveAsync(new GlanceWidgetData
        {
            TimeFormat = GlanceTimeFormatMode.Hour24,
            WorldClocks = [new GlanceWorldClockData("UTC", "伦敦")]
        });

        using (JsonDocument glanceJson = JsonDocument.Parse(
                   await File.ReadAllTextAsync(glanceStore.StorePath)))
        {
            Assert.Equal("Hour24", glanceJson.RootElement.GetProperty("timeFormat").GetString());
            Assert.Equal(
                JsonValueKind.String,
                glanceJson.RootElement.GetProperty("timeFormat").ValueKind);
            Assert.Equal(
                "伦敦",
                glanceJson.RootElement.GetProperty("worldClocks")[0].GetProperty("cityName").GetString());
        }

        await File.WriteAllTextAsync(
            glanceStore.StorePath,
            """
            {
              "timeFormat": 2,
              "futurePreferenceField": true
            }
            """);
        GlanceWidgetData legacyGlance = await new GlanceWidgetStore(
            Path.Combine(_tempRoot, "glance")).LoadAsync();
        Assert.Equal(GlanceTimeFormatMode.Hour12, legacyGlance.TimeFormat);
        Assert.Equal(GlanceWidgetData.CurrentVersion, legacyGlance.Version);
    }

    private static string[] GenericTypeArguments(string source, string methodName) =>
        Regex.Matches(source, $@"\b{Regex.Escape(methodName)}<(?<type>[^>]+)>")
            .Select(match => match.Groups["type"].Value)
            .Where(type => !string.Equals(type, "T", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order()
            .ToArray();

    private static IEnumerable<string> ProductionSourceFiles()
    {
        string projectDirectory = TestPaths.FromRepository("src/WinSticker");
        return Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                string relative = Path.GetRelativePath(projectDirectory, path)
                    .Replace(Path.DirectorySeparatorChar, '/');
                return !relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) &&
                       !relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) &&
                       !relative.StartsWith("AppPackages/", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static string RepositoryRelativePath(string path) =>
        Path.GetRelativePath(TestPaths.FromRepository("."), path)
            .Replace(Path.DirectorySeparatorChar, '/');

    private static string ReadSource(string relativePath) =>
        File.ReadAllText(TestPaths.FromRepository(relativePath));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for files briefly held by antivirus/indexing.
        }
    }
}
