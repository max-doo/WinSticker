using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class WidgetManagerCalendarLifecycleTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly SettingsService _settingsService;
    private readonly WidgetManager _widgetManager;

    public WidgetManagerCalendarLifecycleTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "WinSticker.Tests", Guid.NewGuid().ToString("N"));
        string storageRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "storage")).FullName;
        string desktopRoot = Directory.CreateDirectory(Path.Combine(_tempRoot, "desktop")).FullName;

        _settingsService = new SettingsService(Path.Combine(_tempRoot, "settings"));
        _settingsService.Settings.DefaultManagedStorageRootPath = storageRoot;

        var fileService = new FileService();
        var organizerService = TestOrganizerServices.Create(_settingsService, fileService);
        var themeService = new ThemeService(_settingsService);
        _widgetManager = new WidgetManager(
            _settingsService,
            fileService,
            organizerService,
            themeService,
            new QuickCaptureService(new QuickCaptureStore(Path.Combine(_tempRoot, "quick-capture"))),
            localizationService: null,
            desktopPathProvider: () => desktopRoot,
            recycleManagedFolderDeletes: false);
    }

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
        }
    }

    [Fact]
    public async Task RemovingOneOfTwoCalendarWidgets_KeepsFeatureEnabled()
    {
        var config1 = new WidgetConfig { Id = "cal-1", WidgetKind = WidgetKind.Calendar };
        var config2 = new WidgetConfig { Id = "cal-2", WidgetKind = WidgetKind.Calendar };
        _settingsService.Settings.Widgets.Add(config1);
        _settingsService.Settings.Widgets.Add(config2);
        FeatureWidgetSettings.SetEnabled(_settingsService.Settings, WidgetKind.Calendar, true);

        await _widgetManager.RemoveWidgetAsync("cal-1");

        Assert.True(FeatureWidgetSettings.IsEnabled(_settingsService.Settings, WidgetKind.Calendar));
        Assert.Contains(_settingsService.Settings.Widgets, w => w.Id == "cal-2");
        Assert.DoesNotContain(_settingsService.Settings.Widgets, w => w.Id == "cal-1");
    }

    [Fact]
    public async Task RemovingLastCalendarWidget_DisablesFeature()
    {
        var config1 = new WidgetConfig { Id = "cal-solo", WidgetKind = WidgetKind.Calendar };
        _settingsService.Settings.Widgets.Add(config1);
        FeatureWidgetSettings.SetEnabled(_settingsService.Settings, WidgetKind.Calendar, true);

        await _widgetManager.RemoveWidgetAsync("cal-solo");

        Assert.False(FeatureWidgetSettings.IsEnabled(_settingsService.Settings, WidgetKind.Calendar));
        Assert.DoesNotContain(_settingsService.Settings.Widgets, w => w.Id == "cal-solo");
    }
}
