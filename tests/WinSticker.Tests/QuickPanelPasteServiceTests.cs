using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class QuickPanelPasteServiceTests
{
    private static readonly IntPtr Target = (IntPtr)10;
    private static readonly IntPtr Panel = (IntPtr)20;
    private static readonly IntPtr Other = (IntPtr)30;

    [Fact]
    public async Task FocusedBrowser_IsNeverReactivatedBeforePaste()
    {
        var desktop = new Desktop();
        var service = new QuickPanelPasteService(desktop);
        Assert.True(await service.PrepareTargetAsync(Target));
        Assert.True(await service.SendPasteAsync(Target, () => true));
        Assert.Equal(0, desktop.Activations);
        Assert.Equal(1, desktop.Pastes);
    }

    [Fact]
    public async Task SearchMode_RestoresTargetBeforePasting()
    {
        var desktop = new Desktop { ForegroundWindow = Panel };
        var service = new QuickPanelPasteService(desktop);
        Assert.True(await service.PrepareTargetAsync(Target));
        Assert.True(await service.SendPasteAsync(Target, () => true));
        Assert.Equal(1, desktop.Activations);
        Assert.Equal(Target, desktop.ForegroundWindow);
        Assert.Equal(1, desktop.Pastes);
    }

    [Fact]
    public async Task UserSwitchedApplications_DoesNotStealFocus()
    {
        var desktop = new Desktop { ForegroundWindow = Other };
        Assert.False(await new QuickPanelPasteService(desktop).PrepareTargetAsync(Target));
        Assert.Equal(0, desktop.Activations);
        Assert.Equal(0, desktop.Pastes);
    }

    [Fact]
    public async Task ClosedTarget_IsNotActivated()
    {
        var desktop = new Desktop { Valid = false, ForegroundWindow = Panel };
        Assert.False(await new QuickPanelPasteService(desktop).PrepareTargetAsync(Target));
        Assert.Equal(0, desktop.Activations);
    }

    [Fact]
    public async Task ActivationDenied_DoesNotSendPaste()
    {
        var desktop = new Desktop { ForegroundWindow = Panel, ActivationAllowed = false };
        Assert.False(await new QuickPanelPasteService(desktop).PrepareTargetAsync(Target));
        Assert.Equal(0, desktop.Pastes);
    }

    [Fact]
    public async Task ActivationWait_StopsWhenUserSwitchesToAnotherApp()
    {
        var desktop = new Desktop { ForegroundWindow = Panel, ActivateImmediately = false };
        var service = new QuickPanelPasteService(desktop, _ =>
        {
            desktop.ForegroundWindow = Other;
            return Task.CompletedTask;
        });
        Assert.False(await service.PrepareTargetAsync(Target));
        Assert.Equal(1, desktop.Activations);
        Assert.Equal(0, desktop.Pastes);
    }

    [Fact]
    public async Task ModifiersHeld_WaitsForReleaseBeforeSendingOnePaste()
    {
        var desktop = new Desktop { ArePasteKeysReleased = false };
        int waits = 0;
        var service = new QuickPanelPasteService(desktop, _ =>
        {
            Assert.Equal(0, desktop.Pastes);
            if (++waits == 3) desktop.ArePasteKeysReleased = true;
            return Task.CompletedTask;
        });
        Assert.True(await service.SendPasteAsync(Target, () => true));
        Assert.Equal(3, waits);
        Assert.Equal(1, desktop.Pastes);
    }

    [Fact]
    public async Task ModifiersNeverReleased_TimesOutWithoutPasting()
    {
        var desktop = new Desktop { ArePasteKeysReleased = false };
        int waits = 0;
        var service = new QuickPanelPasteService(desktop, _ => { waits++; return Task.CompletedTask; });
        Assert.False(await service.SendPasteAsync(Target, () => true));
        Assert.InRange(waits, 1, 100);
        Assert.Equal(0, desktop.Pastes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TargetChangesWhileWaiting_Aborts(bool closed)
    {
        var desktop = new Desktop { ArePasteKeysReleased = false };
        var service = new QuickPanelPasteService(desktop, _ =>
        {
            desktop.Valid = !closed;
            if (!closed) desktop.ForegroundWindow = Other;
            desktop.ArePasteKeysReleased = true;
            return Task.CompletedTask;
        });
        Assert.False(await service.SendPasteAsync(Target, () => true));
        Assert.Equal(0, desktop.Pastes);
    }

    [Fact]
    public async Task PanelReopenedDuringPaste_CancelsOldRequest()
    {
        var desktop = new Desktop { ArePasteKeysReleased = false };
        bool current = true;
        var service = new QuickPanelPasteService(desktop, _ =>
        {
            current = false;
            desktop.ArePasteKeysReleased = true;
            return Task.CompletedTask;
        });
        Assert.False(await service.SendPasteAsync(Target, () => current));
        Assert.Equal(0, desktop.Pastes);
    }

    [Fact]
    public async Task InputInjectionFailure_IsReported()
    {
        var desktop = new Desktop { SendAllowed = false };
        Assert.False(await new QuickPanelPasteService(desktop).SendPasteAsync(Target, () => true));
    }

    private sealed class Desktop : IQuickPanelPasteDesktop
    {
        public bool Valid { get; set; } = true;
        public IntPtr ForegroundWindow { get; set; } = Target;
        public bool ActivationAllowed { get; set; } = true;
        public bool ActivateImmediately { get; set; } = true;
        public bool ArePasteKeysReleased { get; set; } = true;
        public bool SendAllowed { get; set; } = true;
        public int Activations { get; private set; }
        public int Pastes { get; private set; }
        public bool IsValidTarget(IntPtr target) => target == Target && Valid;
        public bool IsPanelWindow(IntPtr window) => window == Panel;
        public bool ActivateTarget(IntPtr target)
        {
            Activations++;
            if (ActivationAllowed && ActivateImmediately) ForegroundWindow = target;
            return ActivationAllowed;
        }
        public bool SendPaste() { Pastes++; return SendAllowed; }
    }
}
