using WinSticker.Contracts;
using WinSticker.Controls.WidgetContents;
using WinSticker.Models;

namespace WinSticker.Services;

internal sealed class GlanceWidgetContentProvider : IWidgetContentProvider
{
    public WidgetKind WidgetKind => WidgetKind.Glance;
    public bool CanCreateDetachedContent => true;

    public IWidgetContent CreateDetachedContent(WidgetConfig config, WidgetContentProviderContext context)
    {
        return new GlanceWidgetContentAdapter(
            config,
            context.LocalizationService,
            settingsService: context.SettingsService);
    }
}
