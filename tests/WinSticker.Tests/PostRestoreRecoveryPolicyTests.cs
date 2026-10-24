using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class PostRestoreRecoveryPolicyTests
{
    [Fact]
    public void ShouldSkipStartupScheduledSnapshot_OnlyOnASuccessfulFullRestore()
    {
        // Only that launch reads raw pre-rebase backup options; a failed
        // apply leaves the original data in place and scoped restores keep
        // the current settings file, so both keep their scheduled snapshot.
        Assert.True(PostRestoreRecoveryPolicy.ShouldSkipStartupScheduledSnapshot(
            WinStickerRestoreApplyKind.Full, applySucceeded: true));
        Assert.False(PostRestoreRecoveryPolicy.ShouldSkipStartupScheduledSnapshot(
            WinStickerRestoreApplyKind.Full, applySucceeded: false));
        Assert.False(PostRestoreRecoveryPolicy.ShouldSkipStartupScheduledSnapshot(
            WinStickerRestoreApplyKind.Scoped, applySucceeded: true));
        Assert.False(PostRestoreRecoveryPolicy.ShouldSkipStartupScheduledSnapshot(
            WinStickerRestoreApplyKind.Scoped, applySucceeded: false));
        Assert.False(PostRestoreRecoveryPolicy.ShouldSkipStartupScheduledSnapshot(
            WinStickerRestoreApplyKind.None, applySucceeded: true));
        Assert.False(PostRestoreRecoveryPolicy.ShouldSkipStartupScheduledSnapshot(
            WinStickerRestoreApplyKind.None, applySucceeded: false));
    }
}
