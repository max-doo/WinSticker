using WinSticker.Contracts;
using WinSticker.Controls.WidgetContents;
using WinSticker.Models;

namespace WinSticker.Services;

internal sealed class MusicWidgetContentProvider : IWidgetContentProvider
{
    public WidgetKind WidgetKind => WidgetKind.Music;

    public bool CanCreateDetachedContent => true;

    public IWidgetContent CreateDetachedContent(WidgetConfig config, WidgetContentProviderContext context)
    {
        if (config.WidgetKind != WidgetKind)
        {
            throw new ArgumentException("Music content requires a Music widget config.", nameof(config));
        }

        return new MusicWidgetContentAdapter(
            config,
            context.LocalizationService,
            context.SettingsService);
    }
}
