using WinSticker.Models;
using WinSticker.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace WinSticker.Services;

internal static class GlanceWidgetContextMenuBuilder
{
    private static readonly GlanceDisplayElement[] DisplayElements =
    [
        GlanceDisplayElement.Time,
        GlanceDisplayElement.Date,
        GlanceDisplayElement.Year,
        GlanceDisplayElement.Weekday,
        GlanceDisplayElement.Weather
    ];

    private static readonly GlanceSurfaceMaterialMode[] SurfaceMaterials =
    [
        GlanceSurfaceMaterialMode.FollowSystem,
        GlanceSurfaceMaterialMode.Transparent,
        GlanceSurfaceMaterialMode.Glass
    ];

    public static void Append(
        MenuFlyout flyout,
        GlanceWidgetViewModel viewModel,
        LocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(flyout);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(localizationService);

        flyout.Items.Add(CreateDisplayMenu(viewModel, localizationService));
        flyout.Items.Add(CreateSurfaceMaterialMenu(viewModel, localizationService));
        flyout.Items.Add(new MenuFlyoutSeparator());
    }

    private static MenuFlyoutSubItem CreateDisplayMenu(
        GlanceWidgetViewModel viewModel,
        LocalizationService localizationService)
    {
        var menu = new MenuFlyoutSubItem
        {
            Text = localizationService.T("Glance.Display.Title"),
            Icon = new FontIcon { Glyph = "" }
        };

        foreach (GlanceDisplayElement element in DisplayElements)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = localizationService.T(GetDisplayTextKey(element)),
                IsChecked = GlanceWidgetSettingsPolicy.IsDisplayElementVisible(
                    viewModel.Settings,
                    element)
            };
            item.Click += (_, _) =>
                _ = RunAsync(() => viewModel.SetDisplayElementAsync(
                    element,
                    item.IsChecked));
            menu.Items.Add(item);
        }

        var secondsItem = new ToggleMenuFlyoutItem
        {
            Text = localizationService.T("Glance.Display.Seconds"),
            IsChecked = viewModel.Settings.ShowSeconds
        };
        secondsItem.Click += (_, _) =>
            _ = RunAsync(() => viewModel.SetShowSecondsAsync(secondsItem.IsChecked));
        menu.Items.Add(secondsItem);

        return menu;
    }

    private static MenuFlyoutSubItem CreateSurfaceMaterialMenu(
        GlanceWidgetViewModel viewModel,
        LocalizationService localizationService)
    {
        var menu = new MenuFlyoutSubItem
        {
            Text = localizationService.T("Glance.Surface.Title"),
            Icon = new FontIcon { Glyph = "" }
        };

        foreach (GlanceSurfaceMaterialMode mode in SurfaceMaterials)
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = localizationService.T(GetSurfaceMaterialTextKey(mode)),
                GroupName = "GlanceSurfaceMaterial",
                IsChecked = viewModel.SurfaceMaterial == mode
            };
            item.Click += (_, _) =>
                _ = RunAsync(() => viewModel.SetSurfaceMaterialAsync(mode));
            menu.Items.Add(item);
        }

        return menu;
    }

    private static string GetSurfaceMaterialTextKey(GlanceSurfaceMaterialMode mode) =>
        mode switch
        {
            GlanceSurfaceMaterialMode.Transparent => "Glance.Surface.Transparent",
            GlanceSurfaceMaterialMode.Glass => "Glance.Surface.Glass",
            _ => "Glance.Surface.FollowSystem"
        };

    private static string GetDisplayTextKey(GlanceDisplayElement element) =>
        element switch
        {
            GlanceDisplayElement.Time => "Glance.Display.Time",
            GlanceDisplayElement.Date => "Glance.Display.Date",
            GlanceDisplayElement.Year => "Glance.Display.Year",
            GlanceDisplayElement.Weekday => "Glance.Display.Weekday",
            GlanceDisplayElement.Weather => "Glance.Display.Weather",
            _ => "Glance.Display.Title"
        };

    private static async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            App.Log($"[GlanceContextMenu] Command failed: {ex}");
        }
    }
}
