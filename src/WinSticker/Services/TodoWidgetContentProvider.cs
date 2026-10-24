using WinSticker.Contracts;
using WinSticker.Controls.WidgetContents;
using WinSticker.Models;

namespace WinSticker.Services;

internal sealed class TodoWidgetContentProvider : IWidgetContentProvider
{
    public WidgetKind WidgetKind => WidgetKind.Todo;

    public bool CanCreateDetachedContent => true;

    public IWidgetContent CreateDetachedContent(WidgetConfig config, WidgetContentProviderContext context)
    {
        if (config.WidgetKind != WidgetKind)
        {
            throw new ArgumentException("Todo content requires a Todo widget config.", nameof(config));
        }

        var store = (context.TodoStoreFactory ?? (widget => new TodoWidgetStore(widget.Id)))(config);
        return context.SettingsService is null
            ? new TodoWidgetContentAdapter(config, store, context.LocalizationService)
            : new TodoWidgetContentAdapter(config, store, context.LocalizationService, context.SettingsService);
    }
}
