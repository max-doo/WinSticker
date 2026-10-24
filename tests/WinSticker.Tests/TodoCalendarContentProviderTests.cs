using WinSticker.Contracts;
using WinSticker.Controls.WidgetContents;
using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class TodoCalendarContentProviderTests
{
    private static void Cleanup(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void Provider_ExposesCorrectWidgetKindAndCapabilities()
    {
        var provider = new TodoCalendarContentProvider();
        Assert.Equal(WidgetKind.Calendar, provider.WidgetKind);
        Assert.True(provider.CanCreateDetachedContent);
    }

    [Fact]
    public void CreateDetachedContent_WithoutSettingsService_Throws()
    {
        var provider = new TodoCalendarContentProvider();
        string tempDir = Path.Combine(Path.GetTempPath(), "cal-provider-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var settings = new SettingsService(tempDir);
            var loc = new LocalizationService(settings);
            var context = new WidgetContentProviderContext(loc, null, null, _ => null!);
            var config = new WidgetConfig { Id = "cal", WidgetKind = WidgetKind.Calendar };

            Assert.Throws<InvalidOperationException>(() =>
            {
                provider.CreateDetachedContent(config, context);
            });
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public void CreateDetachedContent_RejectsNonCalendarConfig()
    {
        var provider = new TodoCalendarContentProvider();
        string tempDir = Path.Combine(Path.GetTempPath(), "cal-provider-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var settings = new SettingsService(tempDir);
            var loc = new LocalizationService(settings);
            var context = new WidgetContentProviderContext(loc, settings, null, _ => null!);
            var config = new WidgetConfig { Id = "file1", WidgetKind = WidgetKind.File };

            Assert.Throws<ArgumentException>(() =>
            {
                provider.CreateDetachedContent(config, context);
            });
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public void CreateDetachedContent_ResolvesActiveTodoStoreNotOwnId()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "cal-provider-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var settings = new SettingsService(tempDir);
            var activeTodo = new WidgetConfig
            {
                Id = "todo-active",
                WidgetKind = WidgetKind.Todo,
                IsDisabled = false
            };
            settings.Settings.WidgetLayout.Widgets.Add(activeTodo);

            var loc = new LocalizationService(settings);
            WidgetConfig? capturedStoreConfig = null;
            var context = new WidgetContentProviderContext(
                loc,
                settings,
                cfg =>
                {
                    capturedStoreConfig = cfg;
                    return new TodoWidgetStore(Path.Combine(tempDir, "widgets"), cfg.Id);
                },
                _ => null!);

            var calendarConfig = new WidgetConfig
            {
                Id = "calendar-1",
                WidgetKind = WidgetKind.Calendar
            };

            var provider = new TodoCalendarContentProvider();
            var content = provider.CreateDetachedContent(calendarConfig, context);

            var adapter = Assert.IsType<TodoCalendarContentAdapter>(content);
            Assert.Equal(WidgetKind.Calendar, adapter.WidgetKind);
            Assert.NotNull(capturedStoreConfig);
            Assert.Equal("todo-active", capturedStoreConfig.Id);
            Assert.Equal("todo-active", adapter.ViewModel.Todo.Store.StorePath.Contains("todo-active") ? "todo-active" : "");
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public void CreateDetachedContent_WhenNoActiveTodo_UsesFallbackStore()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "cal-provider-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var settings = new SettingsService(tempDir);
            // No todo widgets in layout
            var loc = new LocalizationService(settings);
            WidgetConfig? capturedStoreConfig = null;
            var context = new WidgetContentProviderContext(
                loc,
                settings,
                cfg =>
                {
                    capturedStoreConfig = cfg;
                    return new TodoWidgetStore(Path.Combine(tempDir, "widgets"), cfg.Id);
                },
                _ => null!);

            var calendarConfig = new WidgetConfig
            {
                Id = "calendar-1",
                WidgetKind = WidgetKind.Calendar
            };

            var provider = new TodoCalendarContentProvider();
            var content = provider.CreateDetachedContent(calendarConfig, context);

            var adapter = Assert.IsType<TodoCalendarContentAdapter>(content);
            Assert.NotNull(capturedStoreConfig);
            Assert.Equal(TodoTargetResolver.FallbackWidgetId, capturedStoreConfig.Id);
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public void Adapter_DoesNotImplementIWidgetGroupContentCacheable()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "cal-adapter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var store = new TodoWidgetStore(Path.Combine(tempDir, "widgets"), "test-todo");
            var settings = new SettingsService(tempDir);
            var loc = new LocalizationService(settings);
            var config = new WidgetConfig { Id = "cal", WidgetKind = WidgetKind.Calendar };

            var adapter = new TodoCalendarContentAdapter(config, store, loc, settings);

            Assert.False((object)adapter is IWidgetGroupContentCacheable);
            Assert.True(adapter is IWidgetResponsiveLayoutContent);
            Assert.True(adapter is IWidgetInteractiveResizeContent);
            Assert.True(adapter is IWidgetFeedbackSource);
            Assert.True(adapter is IWidgetAddActionContent);
        }
        finally
        {
            Cleanup(tempDir);
        }
    }
}
