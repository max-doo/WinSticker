using WinSticker.Models;
using WinSticker.Services;
using Xunit;

namespace WinSticker.Tests;

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

    [Fact]
    public void QuickPanelWindow_HeaderDraggingContract_UsesXamlPointerInteractionAndDoesNotReturnHtcaption()
    {
        string xamlPath = TestPaths.FromRepository("src/WinSticker/Views/QuickPanelWindow.xaml");
        string csPath = TestPaths.FromRepository("src/WinSticker/Views/QuickPanelWindow.xaml.cs");
        string xaml = System.IO.File.ReadAllText(xamlPath);
        string cs = System.IO.File.ReadAllText(csPath);

        // XAML header drag wiring
        Assert.Contains("PointerPressed=\"Header_PointerPressed\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PointerMoved=\"Header_PointerMoved\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PointerReleased=\"Header_PointerReleased\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PointerCaptureLost=\"Header_PointerCaptureLost\"", xaml, StringComparison.Ordinal);

        // Code-behind pointer capture interaction
        Assert.Contains("CapturePointer", cs, StringComparison.Ordinal);
        Assert.Contains("ReleasePointerCapture", cs, StringComparison.Ordinal);
        Assert.Contains("TryBeginWindowDrag", cs, StringComparison.Ordinal);
        Assert.Contains("Header_PointerMoved", cs, StringComparison.Ordinal);

        // Never intercept HTCAPTION in SubclassProc (hijacks XAML Island input on borderless WinUI 3 windows)
        Assert.DoesNotContain("HTCAPTION", cs, StringComparison.Ordinal);
    }
}
