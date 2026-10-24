using DeskBox.Models;
using DeskBox.Services;
using Xunit;

namespace DeskBox.Tests;

public sealed class QuickPanelModelsTests
{
    [Fact]
    public void QuickPanelEnums_HaveExpectedMembers()
    {
        Assert.Equal(0, (int)QuickPanelTab.Clipboard);
        Assert.Equal(1, (int)QuickPanelTab.Todo);
        Assert.Equal(2, (int)QuickPanelTab.Notes);

        Assert.Equal(0, (int)QuickPanelFilter.All);
        Assert.Equal(1, (int)QuickPanelFilter.Text);
        Assert.Equal(2, (int)QuickPanelFilter.Images);
        Assert.Equal(3, (int)QuickPanelFilter.Links);
        Assert.Equal(4, (int)QuickPanelFilter.Favorites);

        Assert.Equal(0, (int)QuickPanelTodoViewMode.Today);
        Assert.Equal(1, (int)QuickPanelTodoViewMode.Overdue);
        Assert.Equal(2, (int)QuickPanelTodoViewMode.Unscheduled);
        Assert.Equal(3, (int)QuickPanelTodoViewMode.Timeline);
        Assert.Equal(4, (int)QuickPanelTodoViewMode.Completed);
    }

    [Fact]
    public void QuickPanelNoteItem_DefaultsAreInitialized()
    {
        var note = new QuickPanelNoteItem();
        Assert.False(string.IsNullOrWhiteSpace(note.Id));
        Assert.Equal(string.Empty, note.Title);
        Assert.Equal(string.Empty, note.Body);
        Assert.True(note.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.True(note.UpdatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void AppSettings_ExposesQuickPanelSettingsDefaults()
    {
        var settings = new AppSettings();
        Assert.False(settings.QuickPanelHotkeyEnabled);
        Assert.Equal(HotkeyActivationKind.Chord, settings.QuickPanelHotkeyActivationKind);
        Assert.Equal((int)HotkeyModifierKeys.Alt, settings.QuickPanelHotkeyModifiers);
        Assert.Equal(0x20, settings.QuickPanelHotkeyKey);
        Assert.Equal(QuickPanelTab.Clipboard, settings.QuickPanelDefaultTab);
        Assert.True(settings.QuickPanelAutoPaste);
    }
}
