#if WINSTICKER_NATIVE_AOT
using WinSticker.Controls.WidgetContents;
using WinSticker.Models;
using WinSticker.Services;
using WinSticker.ViewModels;

namespace WinSticker;

public partial class App
{
    private const double AotGlanceMutatedSizeScale = 1.25;
    private const string AotGlanceMutatedWorldClockZone = "UTC";
    private const string AotGlanceMutatedWorldClockName = "London";

    private async Task CaptureAotManagedUiGlancePersistenceAsync(
        AotManagedUiSmokeResult result,
        string phase)
    {
        WidgetManager manager = WidgetManager ??
            throw new InvalidOperationException("WidgetManager is unavailable.");
        AotGlancePersistenceHost host =
            await manager.GetAotGlancePersistenceHostAsync();
        RequireAotManagedUi(
            result,
            host.WindowHandle != 0 && host.HasXamlRoot && host.Visible,
            "GlanceHostReady",
            "The real Glance widget HWND or XamlRoot is unavailable.");

        AotManagedUiGlancePersistenceEvidence evidence =
            result.GlancePersistence ??
            throw new InvalidOperationException("Glance persistence evidence is unavailable.");
        evidence.WindowHandle = host.WindowHandle;
        evidence.HasXamlRoot = host.HasXamlRoot;
        evidence.Visible = host.Visible;

        bool beforeMutation = phase == AotManagedUiGlanceVerifyRestorePhase;
        evidence.Before = await CaptureAotGlanceStateAsync(
            host,
            expectMutation: beforeMutation);
        RequireAotGlanceState(result, evidence.Before, beforeMutation);
        if (beforeMutation)
        {
            RequireAotManagedUi(
                result,
                true,
                "GlanceWorldClockPersisted",
                "The second process did not reload the persisted world clocks.");
        }

        switch (phase)
        {
            case AotManagedUiGlanceMutatePhase:
                await ApplyAotGlanceMutationAsync(host.ViewModel);
                evidence.After = await CaptureAotGlanceStateAsync(
                    host,
                    expectMutation: true);
                RequireAotGlanceState(result, evidence.After, expectMutation: true);
                RequireAotManagedUi(
                    result,
                    true,
                    "GlanceWorldClockPersisted",
                    "The mutated world clocks were not persisted.");
                break;

            case AotManagedUiGlanceVerifyRestorePhase:
                await RestoreAotGlanceBaselineAsync(host.ViewModel);
                evidence.After = await CaptureAotGlanceStateAsync(
                    host,
                    expectMutation: false);
                RequireAotGlanceState(result, evidence.After, expectMutation: false);
                RequireAotManagedUi(
                    result,
                    true,
                    "GlanceBaselineRestoredAfterRestart",
                    "The Restore phase did not return to the clean baseline.");
                break;

            case AotManagedUiGlancePostflightPhase:
                evidence.After = await CaptureAotGlanceStateAsync(
                    host,
                    expectMutation: false);
                RequireAotGlanceState(result, evidence.After, expectMutation: false);
                RequireAotManagedUi(
                    result,
                    true,
                    "GlancePostflightBaselineRetained",
                    "The third process did not retain the restored baseline.");
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported Glance persistence phase '{phase}'.");
        }
    }

    private static async Task ApplyAotGlanceMutationAsync(
        GlanceWidgetViewModel viewModel)
    {
        await viewModel.AddWorldClockAsync(
            AotGlanceMutatedWorldClockZone,
            AotGlanceMutatedWorldClockName);
        await viewModel.SetShowSecondsAsync(true);
        await viewModel.SetSizeScaleAsync(AotGlanceMutatedSizeScale);
        await viewModel.SetDisplayElementAsync(GlanceDisplayElement.Time, isVisible: true);
        await viewModel.SetDisplayElementAsync(GlanceDisplayElement.Date, isVisible: true);
        await viewModel.SetDisplayElementAsync(GlanceDisplayElement.Year, isVisible: true);
        await viewModel.SetDisplayElementAsync(GlanceDisplayElement.Weekday, isVisible: false);
        await viewModel.SetSurfaceMaterialAsync(GlanceSurfaceMaterialMode.Transparent);
    }

    private static async Task RestoreAotGlanceBaselineAsync(
        GlanceWidgetViewModel viewModel)
    {
        await viewModel.RemoveWorldClockAsync(AotGlanceMutatedWorldClockZone);
        await viewModel.SetShowSecondsAsync(false);
        await viewModel.SetSizeScaleAsync(1);
        await viewModel.SetDisplayElementAsync(GlanceDisplayElement.Time, isVisible: true);
        await viewModel.SetDisplayElementAsync(GlanceDisplayElement.Year, isVisible: false);
        await viewModel.SetDisplayElementAsync(GlanceDisplayElement.Date, isVisible: true);
        await viewModel.SetDisplayElementAsync(GlanceDisplayElement.Weekday, isVisible: true);
        await viewModel.SetSurfaceMaterialAsync(GlanceSurfaceMaterialMode.FollowSystem);
    }

    private async Task<AotManagedUiGlanceStateEvidence> CaptureAotGlanceStateAsync(
        AotGlancePersistenceHost host,
        bool expectMutation)
    {
        AotGlanceViewModelSnapshot viewModel =
            await WaitForAotGlanceViewModelAsync(host.ViewModel, expectMutation);
        AotGlanceSurfaceSnapshot surface =
            await host.Surface.WaitForAotGlanceSurfaceAsync(expectMutation);
        GlanceWidgetData store = await GlanceWidgetStore
            .ForWidget(AotManagedUiGlanceWidgetId)
            .LoadAsync();

        return new AotManagedUiGlanceStateEvidence
        {
            Store = MapAotGlanceStore(store),
            ViewModel = MapAotGlanceViewModel(viewModel),
            Surface = MapAotGlanceSurface(surface)
        };
    }

    private static async Task<AotGlanceViewModelSnapshot> WaitForAotGlanceViewModelAsync(
        GlanceWidgetViewModel viewModel,
        bool expectMutation)
    {
        AotGlanceViewModelSnapshot last = viewModel.CaptureAotGlanceSnapshot();
        for (int attempt = 0; attempt < 120; attempt++)
        {
            last = viewModel.CaptureAotGlanceSnapshot();
            bool ready = expectMutation
                ? IsAotGlanceViewModelMutation(last)
                : IsAotGlanceViewModelBaseline(last);
            if (ready)
            {
                return last;
            }

            await Task.Delay(50);
        }

        throw new InvalidOperationException(
            $"The Glance ViewModel did not stabilize. Snapshot={last}");
    }

    private static AotManagedUiGlanceStoreEvidence MapAotGlanceStore(
        GlanceWidgetData store)
    {
        return new AotManagedUiGlanceStoreEvidence
        {
            Version = store.Version,
            ShowTime = store.ShowTime,
            ShowDate = store.ShowDate,
            ShowYear = store.ShowYear,
            ShowWeekday = store.ShowWeekday,
            ShowWeather = store.ShowWeather,
            ShowSeconds = store.ShowSeconds,
            SizeScale = store.SizeScale,
            SurfaceMaterial = store.SurfaceMaterial.ToString(),
            WorldClockCount = store.WorldClocks.Count
        };
    }

    private static AotManagedUiGlanceViewModelEvidence MapAotGlanceViewModel(
        AotGlanceViewModelSnapshot viewModel)
    {
        return new AotManagedUiGlanceViewModelEvidence
        {
            ShowTime = viewModel.ShowTime,
            ShowDate = viewModel.ShowDate,
            ShowYear = viewModel.ShowYear,
            ShowWeekday = viewModel.ShowWeekday,
            ShowWeather = viewModel.ShowWeather,
            ShowSeconds = viewModel.ShowSeconds,
            SizeScale = viewModel.SizeScale,
            SurfaceMaterial = viewModel.SurfaceMaterial,
            WorldClockCount = viewModel.WorldClockCount,
            HasWorldClocks = viewModel.HasWorldClocks
        };
    }

    private static AotManagedUiGlanceSurfaceEvidence MapAotGlanceSurface(
        AotGlanceSurfaceSnapshot surface)
    {
        return new AotManagedUiGlanceSurfaceEvidence
        {
            IsLoaded = surface.IsLoaded,
            HasXamlRoot = surface.HasXamlRoot,
            DataContextMatchesViewModel = surface.DataContextMatchesViewModel,
            ActualWidth = surface.ActualWidth,
            ActualHeight = surface.ActualHeight,
            ContentVisible = surface.ContentVisible,
            ClockColumnVisible = surface.ClockColumnVisible,
            WorldClocksVisible = surface.WorldClocksVisible,
            WorldClockRowCount = surface.WorldClockRowCount
        };
    }

    private static void RequireAotGlanceState(
        AotManagedUiSmokeResult result,
        AotManagedUiGlanceStateEvidence state,
        bool expectMutation)
    {
        bool valid = expectMutation
            ? IsAotGlanceMutation(state)
            : IsAotGlanceBaseline(state);
        RequireAotManagedUi(
            result,
            valid,
            expectMutation ? "GlanceMutationApplied" : "GlanceBaselineVerified",
            expectMutation
                ? "The persisted Glance mutation did not reach the store, ViewModel, and real surface."
                : "The Glance baseline was not restored across the store, ViewModel, and real surface.");
    }

    private static bool IsAotGlanceBaseline(AotManagedUiGlanceStateEvidence state)
    {
        return IsAotGlanceStoreBaseline(state.Store) &&
            IsAotGlanceViewModelBaseline(state.ViewModel) &&
            IsAotGlanceSurfaceHealthy(state.Surface) &&
            !state.Surface.WorldClocksVisible &&
            state.Surface.WorldClockRowCount == 0;
    }

    private static bool IsAotGlanceMutation(AotManagedUiGlanceStateEvidence state)
    {
        return IsAotGlanceStoreMutation(state.Store) &&
            IsAotGlanceViewModelMutation(state.ViewModel) &&
            IsAotGlanceSurfaceHealthy(state.Surface) &&
            state.Surface.WorldClocksVisible &&
            state.Surface.WorldClockRowCount == 1;
    }

    private static bool IsAotGlanceSurfaceHealthy(AotManagedUiGlanceSurfaceEvidence surface)
    {
        return surface.IsLoaded &&
            surface.HasXamlRoot &&
            surface.DataContextMatchesViewModel &&
            surface.ActualWidth > 0 &&
            surface.ActualHeight > 0 &&
            surface.ContentVisible &&
            surface.ClockColumnVisible;
    }

    private static bool IsAotGlanceStoreBaseline(AotManagedUiGlanceStoreEvidence store)
    {
        return store.Version == GlanceWidgetData.CurrentVersion &&
            store.ShowTime &&
            store.ShowDate &&
            !store.ShowYear &&
            store.ShowWeekday &&
            store.ShowWeather &&
            !store.ShowSeconds &&
            Math.Abs(store.SizeScale - 1) < 0.001 &&
            store.SurfaceMaterial == nameof(GlanceSurfaceMaterialMode.FollowSystem) &&
            store.WorldClockCount == 0;
    }

    private static bool IsAotGlanceStoreMutation(AotManagedUiGlanceStoreEvidence store)
    {
        return store.Version == GlanceWidgetData.CurrentVersion &&
            store.ShowTime &&
            store.ShowDate &&
            store.ShowYear &&
            !store.ShowWeekday &&
            store.ShowWeather &&
            store.ShowSeconds &&
            Math.Abs(store.SizeScale - AotGlanceMutatedSizeScale) < 0.001 &&
            store.SurfaceMaterial == nameof(GlanceSurfaceMaterialMode.Transparent) &&
            store.WorldClockCount == 1;
    }

    private static bool IsAotGlanceViewModelBaseline(AotGlanceViewModelSnapshot viewModel)
    {
        return viewModel.ShowTime &&
            viewModel.ShowDate &&
            !viewModel.ShowYear &&
            viewModel.ShowWeekday &&
            viewModel.ShowWeather &&
            !viewModel.ShowSeconds &&
            Math.Abs(viewModel.SizeScale - 1) < 0.001 &&
            viewModel.SurfaceMaterial == nameof(GlanceSurfaceMaterialMode.FollowSystem) &&
            !viewModel.HasWorldClocks;
    }

    private static bool IsAotGlanceViewModelBaseline(AotManagedUiGlanceViewModelEvidence viewModel)
    {
        return viewModel.ShowTime &&
            viewModel.ShowDate &&
            !viewModel.ShowYear &&
            viewModel.ShowWeekday &&
            viewModel.ShowWeather &&
            !viewModel.ShowSeconds &&
            Math.Abs(viewModel.SizeScale - 1) < 0.001 &&
            viewModel.SurfaceMaterial == nameof(GlanceSurfaceMaterialMode.FollowSystem) &&
            !viewModel.HasWorldClocks;
    }

    private static bool IsAotGlanceViewModelMutation(AotGlanceViewModelSnapshot viewModel)
    {
        return viewModel.ShowTime &&
            viewModel.ShowDate &&
            viewModel.ShowYear &&
            !viewModel.ShowWeekday &&
            viewModel.ShowWeather &&
            viewModel.ShowSeconds &&
            Math.Abs(viewModel.SizeScale - AotGlanceMutatedSizeScale) < 0.001 &&
            viewModel.SurfaceMaterial == nameof(GlanceSurfaceMaterialMode.Transparent) &&
            viewModel.HasWorldClocks &&
            viewModel.WorldClockCount == 1;
    }

    private static bool IsAotGlanceViewModelMutation(AotManagedUiGlanceViewModelEvidence viewModel)
    {
        return viewModel.ShowTime &&
            viewModel.ShowDate &&
            viewModel.ShowYear &&
            !viewModel.ShowWeekday &&
            viewModel.ShowWeather &&
            viewModel.ShowSeconds &&
            Math.Abs(viewModel.SizeScale - AotGlanceMutatedSizeScale) < 0.001 &&
            viewModel.SurfaceMaterial == nameof(GlanceSurfaceMaterialMode.Transparent) &&
            viewModel.HasWorldClocks &&
            viewModel.WorldClockCount == 1;
    }
}

internal sealed class AotManagedUiGlancePersistenceEvidence
{
    public string Phase { get; set; } = string.Empty;
    public bool NormalShutdownRequested { get; set; }
    public long WindowHandle { get; set; }
    public bool HasXamlRoot { get; set; }
    public bool Visible { get; set; }
    public AotManagedUiGlanceStateEvidence Before { get; set; } = new();
    public AotManagedUiGlanceStateEvidence After { get; set; } = new();
}

internal sealed class AotManagedUiGlanceStateEvidence
{
    public AotManagedUiGlanceStoreEvidence Store { get; set; } = new();
    public AotManagedUiGlanceViewModelEvidence ViewModel { get; set; } = new();
    public AotManagedUiGlanceSurfaceEvidence Surface { get; set; } = new();
}

internal sealed class AotManagedUiGlanceStoreEvidence
{
    public int Version { get; set; }
    public bool ShowTime { get; set; }
    public bool ShowDate { get; set; }
    public bool ShowYear { get; set; }
    public bool ShowWeekday { get; set; }
    public bool ShowWeather { get; set; }
    public bool ShowSeconds { get; set; }
    public double SizeScale { get; set; }
    public string SurfaceMaterial { get; set; } = string.Empty;
    public int WorldClockCount { get; set; }
}

internal sealed class AotManagedUiGlanceViewModelEvidence
{
    public bool ShowTime { get; set; }
    public bool ShowDate { get; set; }
    public bool ShowYear { get; set; }
    public bool ShowWeekday { get; set; }
    public bool ShowWeather { get; set; }
    public bool ShowSeconds { get; set; }
    public double SizeScale { get; set; }
    public string SurfaceMaterial { get; set; } = string.Empty;
    public int WorldClockCount { get; set; }
    public bool HasWorldClocks { get; set; }
}

internal sealed class AotManagedUiGlanceSurfaceEvidence
{
    public bool IsLoaded { get; set; }
    public bool HasXamlRoot { get; set; }
    public bool DataContextMatchesViewModel { get; set; }
    public double ActualWidth { get; set; }
    public double ActualHeight { get; set; }
    public bool ContentVisible { get; set; }
    public bool ClockColumnVisible { get; set; }
    public bool WorldClocksVisible { get; set; }
    public int WorldClockRowCount { get; set; }
}
#endif
