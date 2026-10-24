using WinSticker.Models;
using WinSticker.Services;
using Xunit;

namespace WinSticker.Tests;

public sealed class TodoTargetResolverTests
{
    [Fact]
    public void ResolveWidgetId_PrefersFirstEnabledTodo()
    {
        var settings = new AppSettings();
        settings.Widgets.Add(new WidgetConfig { Id = "todo-a", WidgetKind = WidgetKind.Todo, IsDisabled = true });
        settings.Widgets.Add(new WidgetConfig { Id = "todo-b", WidgetKind = WidgetKind.Todo });
        settings.Widgets.Add(new WidgetConfig { Id = "todo-c", WidgetKind = WidgetKind.Todo });

        Assert.Equal("todo-b", TodoTargetResolver.ResolveWidgetId(settings));
    }

    [Fact]
    public void ResolveWidgetId_FallsBackWhenNoTodoWidget()
    {
        var settings = new AppSettings();
        settings.Widgets.Add(new WidgetConfig { Id = "other", WidgetKind = WidgetKind.Glance });

        Assert.Equal("todo", TodoTargetResolver.ResolveWidgetId(settings));
        Assert.Null(TodoTargetResolver.ResolveConfig(settings));
    }

    [Fact]
    public void ResolveWidgetId_FallsBackWhenAllDisabled()
    {
        var settings = new AppSettings();
        settings.Widgets.Add(new WidgetConfig { Id = "todo-a", WidgetKind = WidgetKind.Todo, IsDisabled = true });

        Assert.Equal("todo", TodoTargetResolver.ResolveWidgetId(settings));
    }
}
