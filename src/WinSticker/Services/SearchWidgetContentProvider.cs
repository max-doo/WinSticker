using WinSticker.Contracts;
using WinSticker.Controls.WidgetContents;
using WinSticker.Models;

namespace WinSticker.Services;

internal sealed class SearchWidgetContentProvider : IWidgetContentProvider
{
    public WidgetKind WidgetKind => WidgetKind.Search;

    public bool CanCreateDetachedContent => true;

    public IWidgetContent CreateDetachedContent(WidgetConfig config, WidgetContentProviderContext context)
    {
        if (config.WidgetKind != WidgetKind)
        {
            throw new ArgumentException("Search content requires a Search widget config.", nameof(config));
        }

        return new SearchWidgetContentAdapter(
            config,
            context.LocalizationService,
            context.SettingsService);
    }
}
