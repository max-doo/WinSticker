using WinSticker.Contracts;
using WinSticker.Controls.WidgetContents;
using WinSticker.Models;

namespace WinSticker.Services;

internal sealed class TodoCalendarContentProvider : IWidgetContentProvider
{
    public WidgetKind WidgetKind => WidgetKind.Calendar;

    public bool CanCreateDetachedContent => true;

    public IWidgetContent CreateDetachedContent(WidgetConfig config, WidgetContentProviderContext context)
    {
        if (config.WidgetKind != WidgetKind)
        {
            throw new ArgumentException("Calendar content requires a Calendar widget config.", nameof(config));
        }

        if (context.SettingsService is null)
        {
            throw new InvalidOperationException("Calendar widget requires SettingsService to resolve active Todo widget.");
        }

        var todoConfig = TodoTargetResolver.ResolveConfig(context.SettingsService.Settings)
            ?? new WidgetConfig { Id = TodoTargetResolver.FallbackWidgetId, WidgetKind = WidgetKind.Todo };

        var store = (context.TodoStoreFactory ?? (widget => new TodoWidgetStore(widget.Id)))(todoConfig);

        return new TodoCalendarContentAdapter(config, store, context.LocalizationService, context.SettingsService);
    }
}
