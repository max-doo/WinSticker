namespace WinSticker.Tests;

public sealed class AotStage5B4B2C1ContractTests
{
    [Fact]
    public void GlanceScenario_IsNativeAotOnlyPhaseBoundAndPreviewRootOnly()
    {
        string shared = ReadRepositoryFile("src/WinSticker/App.AotManagedUiSmoke.cs");
        string scenario = ReadRepositoryFile("src/WinSticker/App.AotGlancePersistenceSmoke.cs");

        Assert.Contains("#if WINSTICKER_NATIVE_AOT", shared, StringComparison.Ordinal);
        Assert.Contains("#if WINSTICKER_NATIVE_AOT", scenario, StringComparison.Ordinal);
        Assert.Contains("GlancePersistenceRestart", shared, StringComparison.Ordinal);
        Assert.Contains("WINSTICKER_AOT_MANAGED_UI_GLANCE_PHASE", shared, StringComparison.Ordinal);
        Assert.Contains("Mutate", shared, StringComparison.Ordinal);
        Assert.Contains("VerifyRestore", shared, StringComparison.Ordinal);
        Assert.Contains("Postflight", shared, StringComparison.Ordinal);
        Assert.Contains("RefusedNonPreviewRoot", shared, StringComparison.Ordinal);
        Assert.Contains("AotManagedUiGlanceMutatePhase", scenario, StringComparison.Ordinal);
        Assert.Contains("AotManagedUiGlanceVerifyRestorePhase", scenario, StringComparison.Ordinal);
        Assert.Contains("AotManagedUiGlancePostflightPhase", scenario, StringComparison.Ordinal);
        // The v12 smoke mutates world clocks / size scale instead of a PNG
        // fixture, and the preview-root path check lives in the shared runner.
        Assert.Contains("IsAotManagedUiPathEqualOrInside", shared, StringComparison.Ordinal);
        Assert.Contains("AddWorldClockAsync(", scenario, StringComparison.Ordinal);
        Assert.DoesNotContain("Fixture", scenario, StringComparison.Ordinal);
        Assert.DoesNotContain(".png", scenario, StringComparison.Ordinal);
        Assert.DoesNotContain("GlanceImageService", scenario, StringComparison.Ordinal);
    }

    [Fact]
    public void WorldClockMutation_ReusesOrdinarySettingsProductPolicy()
    {
        string policy = ReadRepositoryFile("src/WinSticker/Services/GlanceWidgetSettingsPolicy.cs");
        string settings = ReadRepositoryFile(
            "src/WinSticker/Views/SettingsSections/GlanceWidgetSettingsSection.xaml.cs");
        string viewModel = ReadRepositoryFile("src/WinSticker/ViewModels/GlanceWidgetViewModel.cs");
        string scenario = ReadRepositoryFile("src/WinSticker/App.AotGlancePersistenceSmoke.cs");

        Assert.Contains("public static void SetDisplayElement(", policy, StringComparison.Ordinal);
        Assert.Contains("public static void SetSurfaceMaterial(", policy, StringComparison.Ordinal);
        Assert.Contains("GlanceWidgetSettingsPolicy.SetDisplayElement(", settings, StringComparison.Ordinal);
        Assert.Contains("GlanceWidgetSettingsPolicy.SetSurfaceMaterial(settings, mode)", settings, StringComparison.Ordinal);
        Assert.Contains("public Task AddWorldClockAsync", viewModel, StringComparison.Ordinal);
        Assert.Contains("public Task SetSizeScaleAsync", viewModel, StringComparison.Ordinal);
        Assert.Contains("viewModel.AddWorldClockAsync(", scenario, StringComparison.Ordinal);
        Assert.Contains("viewModel.SetSizeScaleAsync(", scenario, StringComparison.Ordinal);
        // The v12 product dropped the local-image mutation surface entirely.
        Assert.DoesNotContain("SetLocalImageFiles", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("ClearLocalSource", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLocalImageFilesAsync", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize", scenario, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllText", scenario, StringComparison.Ordinal);
    }

    [Fact]
    public void Preferences_UseExistingDisplayElementAndSurfaceProductPaths()
    {
        string scenario = ReadRepositoryFile("src/WinSticker/App.AotGlancePersistenceSmoke.cs");
        string viewModel = ReadRepositoryFile("src/WinSticker/ViewModels/GlanceWidgetViewModel.cs");

        Assert.Contains("SetDisplayElementAsync", scenario, StringComparison.Ordinal);
        Assert.Contains("SetSurfaceMaterialAsync", scenario, StringComparison.Ordinal);
        Assert.Contains("return _store.UpdateAsync", viewModel, StringComparison.Ordinal);
        Assert.Contains("GlanceWidgetSettingsPolicy.SetDisplayElement", viewModel, StringComparison.Ordinal);
        Assert.Contains("GlanceWidgetSettingsPolicy.SetSurfaceMaterial", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLayoutAsync", scenario, StringComparison.Ordinal);
        Assert.DoesNotContain("SetPhotoPlaybackAsync", scenario, StringComparison.Ordinal);
        Assert.DoesNotContain("GlanceWidgetSettingsPolicy.SetLayout", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("GlanceWidgetSettingsPolicy.SetPhotoPlayback", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void GlanceRuntimeBinding_HasNarrowAotProvidersWithExactPropertyLists()
    {
        string bindable = ReadRepositoryFile(
            "src/WinSticker/ViewModels/GlanceWidgetViewModel.AotBindableProperties.cs");
        string viewModel = ReadRepositoryFile("src/WinSticker/ViewModels/GlanceWidgetViewModel.cs");

        Assert.Contains("#if WINSTICKER_NATIVE_AOT", bindable, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(bindable, "[WinRT.GeneratedBindableCustomProperty"));
        Assert.Equal(29, CountOccurrences(bindable, "nameof("));
        Assert.Contains("nameof(DateFontSize)", bindable, StringComparison.Ordinal);
        Assert.Contains("nameof(WeatherTempFontSize)", bindable, StringComparison.Ordinal);
        Assert.Contains("nameof(WeatherSubFontSize)", bindable, StringComparison.Ordinal);
        Assert.Contains("nameof(WeatherIconSize)", bindable, StringComparison.Ordinal);
        Assert.Contains("nameof(TimeText)", bindable, StringComparison.Ordinal);
        Assert.Contains("nameof(HasWorldClocks)", bindable, StringComparison.Ordinal);
        Assert.Contains("nameof(ShowWeatherBlock)", bindable, StringComparison.Ordinal);
        Assert.Contains("nameof(WeatherLocationName)", bindable, StringComparison.Ordinal);
        Assert.Contains("public sealed partial class GlanceWorldClockRow", bindable, StringComparison.Ordinal);
        Assert.Contains("nameof(CityName)", bindable, StringComparison.Ordinal);
        Assert.Contains("nameof(RelativeDayText)", bindable, StringComparison.Ordinal);
        Assert.Contains("nameof(IsDaytime)", bindable, StringComparison.Ordinal);
        Assert.Contains("nameof(SettingsDisplayName)", bindable, StringComparison.Ordinal);
        Assert.DoesNotContain("nameof(IsEditorialLayout)", bindable, StringComparison.Ordinal);
        Assert.DoesNotContain("nameof(ReadabilityOpacity)", bindable, StringComparison.Ordinal);
        Assert.DoesNotContain("nameof(ShowPhotoControls)", bindable, StringComparison.Ordinal);
        Assert.Contains("public sealed partial class GlanceWidgetViewModel", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void SurfaceEvidence_ObservesClockWeatherAndWorldClockSurface()
    {
        string xaml = ReadRepositoryFile(
            "src/WinSticker/Controls/WidgetContents/GlanceWidgetContent.xaml");
        string surface = ReadRepositoryFile(
            "src/WinSticker/Controls/WidgetContents/GlanceWidgetContent.AotPersistenceSmoke.cs");

        Assert.Contains("x:Name=\"RootGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ContrastPlate\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ContentHost\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ClockColumn\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TimeTextBlock\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WeatherColumn\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding WorldClockRows}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:DataType=\"vm:GlanceWorldClockRow\"", xaml, StringComparison.Ordinal);
        Assert.Contains("WaitForAotGlanceSurfaceAsync(", surface, StringComparison.Ordinal);
        Assert.Contains("bool expectWorldClocks", surface, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(DataContext, _viewModel)", surface, StringComparison.Ordinal);
        Assert.Contains("ContentHost.Visibility == Visibility.Visible", surface, StringComparison.Ordinal);
        Assert.Contains("ClockColumn.Visibility == Visibility.Visible", surface, StringComparison.Ordinal);
        Assert.Contains("_viewModel.WorldClockRows.Count", surface, StringComparison.Ordinal);
        Assert.DoesNotContain("ImageBrush", surface, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadabilityLayer", surface, StringComparison.Ordinal);
        Assert.DoesNotContain("ActionLayer", surface, StringComparison.Ordinal);
    }

    [Fact]
    public void ManagerHost_AllowsOnlyFixedOwnedGlanceWidgetAndRealAdapter()
    {
        string manager = ReadRepositoryFile(
            "src/WinSticker/Services/WidgetManager.AotGlancePersistenceSmoke.cs");

        Assert.Contains("aot-5b4b2c1-glance", manager, StringComparison.Ordinal);
        Assert.Contains("_contentWidgets.TryGetValue", manager, StringComparison.Ordinal);
        Assert.Contains("window.ContentReadyTask", manager, StringComparison.Ordinal);
        Assert.Contains("GlanceWidgetContentAdapter", manager, StringComparison.Ordinal);
        Assert.Contains("adapter.View is GlanceWidgetContent", manager, StringComparison.Ordinal);
        Assert.Contains("adapter.ViewModel", manager, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedTrayFixtureRouting_SelectsGlanceKindAndOwnedId()
    {
        string shared = ReadRepositoryFile("src/WinSticker/App.AotManagedUiSmoke.cs");

        Assert.Contains("bool isGlancePersistence", shared, StringComparison.Ordinal);
        Assert.Contains("? AotManagedUiGlanceWidgetId", shared, StringComparison.Ordinal);
        Assert.Contains("? WidgetKind.Glance", shared, StringComparison.Ordinal);
        Assert.Contains("public AotManagedUiGlancePersistenceEvidence? GlancePersistence", shared, StringComparison.Ordinal);
    }

    [Fact]
    public void PerWidgetStore_RemainsSourceGeneratedAndNormalizesWorldClocks()
    {
        string store = ReadRepositoryFile("src/WinSticker/Services/GlanceWidgetStore.cs");
        string policy = ReadRepositoryFile("src/WinSticker/Services/GlanceWorldClockPolicy.cs");

        Assert.Contains("[JsonSerializable(", store, StringComparison.Ordinal);
        Assert.Contains("typeof(GlanceWidgetData)", store, StringComparison.Ordinal);
        Assert.Contains("GlancePreferencesJsonContext.Default.Preferences", store, StringComparison.Ordinal);
        Assert.Contains("GlanceWorldClockPolicy.NormalizeWorldClocks", store, StringComparison.Ordinal);
        Assert.Contains("GlanceWorldClockPolicy.SetSizeScale", store, StringComparison.Ordinal);
        Assert.Contains("TimeZoneInfo.FindSystemTimeZoneById", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadAllBytes", store, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadAllBytesAsync", store, StringComparison.Ordinal);
        Assert.False(File.Exists(TestPaths.FromRepository("src/WinSticker/Services/GlanceImageService.cs")));
    }

    [Fact]
    public void OuterRunner_SeedsLegacyPerWidgetBaselineWithoutOwnedImages()
    {
        string script = ReadRepositoryFile("scripts/run-aot-managed-ui-smoke.ps1");

        Assert.Contains("glance\\widgets", script, StringComparison.Ordinal);
        Assert.Contains("aot-5b4b2c1-glance.json", script, StringComparison.Ordinal);
        // The v12 seed still ships the retired wallpaper fields so the
        // migration gate proves Normalize drops them and stamps version 12.
        Assert.Contains("backgroundSource = \"LocalFiles\"", script, StringComparison.Ordinal);
        Assert.Contains("localImagePaths = @()", script, StringComparison.Ordinal);
        Assert.Contains("version = 11", script, StringComparison.Ordinal);
        Assert.DoesNotContain("glance-local.png", script, StringComparison.Ordinal);
        Assert.DoesNotContain("WINSTICKER_AOT_MANAGED_UI_GLANCE_FIXTURE", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$glanceImageFixturePath", script, StringComparison.Ordinal);
    }

    [Fact]
    public void OuterRunner_UsesThreeFreshProcessesStoreHashEqualityAndCleanupGates()
    {
        string script = ReadRepositoryFile("scripts/run-aot-managed-ui-smoke.ps1");

        Assert.Contains("Invoke-GlancePersistencePhase", script, StringComparison.Ordinal);
        Assert.Contains("glanceNaturalExit", script, StringComparison.Ordinal);
        Assert.Contains("$processIds | Sort-Object -Unique", script, StringComparison.Ordinal);
        Assert.Contains("phaseExecutableHashes", script, StringComparison.Ordinal);
        Assert.Contains("storeSha256Before", script, StringComparison.Ordinal);
        Assert.Contains("storeSha256After", script, StringComparison.Ordinal);
        Assert.Contains("GlanceWorldClockPersisted", script, StringComparison.Ordinal);
        Assert.Contains("GlanceBaselineRestoredAfterRestart", script, StringComparison.Ordinal);
        Assert.Contains("GlancePostflightBaselineRetained", script, StringComparison.Ordinal);
        Assert.Contains("surface.worldClocksVisible", script, StringComparison.Ordinal);
        Assert.Contains("surface.worldClockRowCount", script, StringComparison.Ordinal);
        Assert.DoesNotContain("surface.activeImageUri", script, StringComparison.Ordinal);
        Assert.DoesNotContain("surface.immersiveLayoutVisible", script, StringComparison.Ordinal);
        Assert.DoesNotContain("surface.calendarLayoutVisible", script, StringComparison.Ordinal);
        Assert.Contains("Assert-GlanceStateEqual", script, StringComparison.Ordinal);
        Assert.Contains("productionDataFingerprintBefore", script, StringComparison.Ordinal);
        Assert.Contains("glancePreviewProcessesAfter", script, StringComparison.Ordinal);
        Assert.Contains("previewRootCleaned", script, StringComparison.Ordinal);
        Assert.Contains("final-glance.json", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Scenario_DoesNotEnterOnlineNetworkLocationPickerFolderOrRustPaths()
    {
        string scenario = ReadRepositoryFile("src/WinSticker/App.AotGlancePersistenceSmoke.cs");
        string surface = ReadRepositoryFile(
            "src/WinSticker/Controls/WidgetContents/GlanceWidgetContent.AotPersistenceSmoke.cs");
        string manager = ReadRepositoryFile(
            "src/WinSticker/Services/WidgetManager.AotGlancePersistenceSmoke.cs");
        string combined = scenario + surface + manager;

        Assert.DoesNotContain("RefreshOnline", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpClient", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FileOpenPicker", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FolderPicker", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "GlanceBackgroundSource.LocalFolder",
            combined,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NativeBackend", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LibraryImport", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CreateWidget", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RemoveWidget", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scenario_ReusesSingleSourceGeneratedResultWriter()
    {
        string shared = ReadRepositoryFile("src/WinSticker/App.AotManagedUiSmoke.cs");

        Assert.Equal(1, CountOccurrences(shared, "JsonSerializer.Serialize("));
        Assert.Contains(
            "AotManagedUiSmokeJsonContext.Default.AotManagedUiSmokeResult",
            shared,
            StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize", shared, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingManagedUiScenarios_RemainIndependentAndRunnable()
    {
        string shared = ReadRepositoryFile("src/WinSticker/App.AotManagedUiSmoke.cs");
        string script = ReadRepositoryFile("scripts/run-aot-managed-ui-smoke.ps1");

        foreach (string scenario in new[]
        {
            "BasicReadOnly",
            "DeepSettingsReadOnly",
            "SettingsWidgetPersistenceRestart",
            "QuickCapturePersistenceRestart",
            "TodoPersistenceRestart",
            "TodoStepsPersistenceRestart",
            "TodoAttachmentsPersistenceRestart"
        })
        {
            Assert.Contains(scenario, shared, StringComparison.Ordinal);
            Assert.Contains(scenario, script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RustAbi_RemainsAtVersionTwoWithCurrentCapabilitiesAndExports()
    {
        string native = ReadRepositoryFile("native/winsticker-native/src/lib.rs");
        string header = ReadRepositoryFile("native/include/winsticker_native.h");

        Assert.Contains("WINSTICKER_NATIVE_ABI_VERSION: u32 = 2", native, StringComparison.Ordinal);
        Assert.Contains("WINSTICKER_NATIVE_CAPABILITY_QUICK_ACCESS_V1: u64 = 1 << 7", native, StringComparison.Ordinal);
        Assert.Equal(9, CountOccurrences(native, "pub const WINSTICKER_NATIVE_CAPABILITY_"));
        Assert.Equal(10, CountOccurrences(native, "#[unsafe(no_mangle)]"));
        Assert.DoesNotContain("glance", header, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Stage5B4B2C1_ProfileSchemaProjectAndLauncherAdvanceTogether()
    {
        string audit = ReadRepositoryFile("scripts/publish-aot-audit.ps1");
        string launcher = ReadRepositoryFile("scripts/start-aot-preview.ps1");
        string project = ReadRepositoryFile("src/WinSticker/WinSticker.csproj");

        Assert.Contains("$auditProfileVersion = 59", audit, StringComparison.Ordinal);
        Assert.Contains("schemaVersion = 55", audit, StringComparison.Ordinal);
        Assert.Contains("stage5B4B2C1", audit, StringComparison.Ordinal);
        Assert.Contains("$RequiredAuditProfileVersion = 59", launcher, StringComparison.Ordinal);
        Assert.Contains("$RequiredSummarySchemaVersion = 55", launcher, StringComparison.Ordinal);
        Assert.Contains("stage 5B-4C3B2B1", project, StringComparison.Ordinal);
        Assert.Contains("Glance world-clock/preferences/reload/restore/postflight", project, StringComparison.Ordinal);
    }

    [Fact]
    public void AuditFreezesGlanceRunnerSurfaceProductManagerScopeAndWarnings()
    {
        string audit = ReadRepositoryFile("scripts/publish-aot-audit.ps1");

        Assert.Contains("stage5B4B2C1SourceFiles", audit, StringComparison.Ordinal);
        Assert.Contains("stage5B4B2C1RequiredRunnerPatterns", audit, StringComparison.Ordinal);
        Assert.Contains("stage5B4B2C1RequiredSurfacePatterns", audit, StringComparison.Ordinal);
        Assert.Contains("stage5B4B2C1RequiredProductPatterns", audit, StringComparison.Ordinal);
        Assert.Contains("stage5B4B2C1RequiredManagerPatterns", audit, StringComparison.Ordinal);
        Assert.Contains("stage5B4B2C1RequiredSmokeScriptPatterns", audit, StringComparison.Ordinal);
        Assert.Contains("stage5B4B2C1ForbiddenScopePatterns", audit, StringComparison.Ordinal);
        Assert.Contains("stage5B4B2C1GeneratedBindableCount", audit, StringComparison.Ordinal);
        Assert.Contains("stage5B4B2C1BindablePropertyCount", audit, StringComparison.Ordinal);
        Assert.Contains("stage5B4B2C1JsonSerializeCallCount", audit, StringComparison.Ordinal);
        Assert.Contains("stage5B4B2C1SourceWarningMessages", audit, StringComparison.Ordinal);
        Assert.Contains("stage5B4B2C1ExpectedWmc1510Count", audit, StringComparison.Ordinal);
        // v12 freezes the world-clock policy instead of the retired image service.
        Assert.Contains("src/WinSticker/Services/GlanceWorldClockPolicy.cs", audit, StringComparison.Ordinal);
        Assert.Contains("src/WinSticker/Controls/WidgetContents/GlanceWidgetContentAdapter.cs", audit, StringComparison.Ordinal);
        Assert.Contains("$stage5B4B2C1BindablePropertyCount -ne 25", audit, StringComparison.Ordinal);
        Assert.Contains("$stage5B4B2C1GeneratedBindableCount -ne 2", audit, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(TestPaths.FromRepository(relativePath));
}
