using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class RestoreMachineLocalPathRebaseTests
{
    private const string AliceDataPath = @"C:\Users\alice\AppData\Local\WinSticker\data";
    private const string AliceProfile = @"C:\Users\alice";
    private const string BobProfile = @"C:\Users\bob";

    [Theory]
    [InlineData(RestoreMachineLocalPathRebaseTests.AliceDataPath, @"C:\Users\alice")]
    [InlineData(
        @"C:\Users\alice\AppData\Local\Packages\Corp.WinSticker_abc\LocalCache\WinSticker\data",
        @"C:\Users\alice")]
    [InlineData(@"D:\profiles\alice\AppData\Local\WinSticker\data", @"D:\profiles\alice")]
    public void TryDeriveSourceProfileRoot_ExtractsTheProfileAboveTheAppDataSegment(
        string sourceDataPath,
        string expected)
    {
        Assert.Equal(
            expected,
            RestoreMachineLocalPathRebase.TryDeriveSourceProfileRoot(sourceDataPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"D:\dev-data\WinSticker\data")]
    [InlineData(@"AppData\Local\WinSticker\data")]
    public void TryDeriveSourceProfileRoot_ReturnsNullWithoutAnAppDataSegment(
        string? sourceDataPath)
    {
        Assert.Null(RestoreMachineLocalPathRebase.TryDeriveSourceProfileRoot(sourceDataPath));
    }

    [Fact]
    public void Rebase_SkipsWhenTheProfilesMatch()
    {
        var settings = new AppSettings
        {
            DefaultManagedStorageRootPath = @"C:\Users\alice\WinSticker",
        };

        RestorePathRebaseResult result = RestoreMachineLocalPathRebase.Rebase(
            settings,
            AliceDataPath,
            AliceProfile);

        Assert.False(result.Performed);
        Assert.False(result.Changed);
        Assert.Equal(@"C:\Users\alice\WinSticker", settings.DefaultManagedStorageRootPath);
    }

    [Fact]
    public void Rebase_SkipsWithoutASourceDataPath()
    {
        var settings = new AppSettings
        {
            DefaultManagedStorageRootPath = @"C:\Users\alice\WinSticker",
        };

        RestorePathRebaseResult result = RestoreMachineLocalPathRebase.Rebase(
            settings,
            null,
            BobProfile);

        Assert.False(result.Performed);
        Assert.False(result.Changed);
    }

    [Fact]
    public void Rebase_MovesProfileLocalSettingsPathsToTheCurrentProfile()
    {
        var settings = new AppSettings
        {
            DefaultManagedStorageRootPath = @"C:\Users\alice\WinSticker",
            AutomaticBackupDirectory = @"C:\Users\alice\Backups\WinSticker",
            ManagedStorageDesktopShortcutPath = @"C:\Users\alice\Desktop",
        };

        RestorePathRebaseResult result = RestoreMachineLocalPathRebase.Rebase(
            settings,
            AliceDataPath,
            BobProfile);

        Assert.True(result.Performed);
        Assert.Equal(3, result.SettingsPathsRebased);
        Assert.Equal(@"C:\Users\bob\WinSticker", settings.DefaultManagedStorageRootPath);
        Assert.Equal(@"C:\Users\bob\Backups\WinSticker", settings.AutomaticBackupDirectory);
        Assert.Equal(@"C:\Users\bob\Desktop", settings.ManagedStorageDesktopShortcutPath);
        Assert.Equal(
            @"C:\Users\bob\WinSticker",
            result.RebasedManagedStorageRootPath);
    }

    [Fact]
    public void Rebase_LeavesPathsOutsideTheSourceProfileAlone()
    {
        var settings = new AppSettings
        {
            DefaultManagedStorageRootPath = @"D:\WinSticker",
            AutomaticBackupDirectory = @"D:\Backups",
        };

        RestorePathRebaseResult result = RestoreMachineLocalPathRebase.Rebase(
            settings,
            AliceDataPath,
            BobProfile);

        Assert.True(result.Performed);
        Assert.Equal(0, result.SettingsPathsRebased);
        Assert.False(result.Changed);
        Assert.Equal(@"D:\WinSticker", settings.DefaultManagedStorageRootPath);
        Assert.Equal(@"D:\Backups", settings.AutomaticBackupDirectory);
        Assert.Null(result.RebasedManagedStorageRootPath);
    }

    [Fact]
    public void Rebase_RepointsOnlyDefaultStorageFileWidgets()
    {
        var followsDefault = new WidgetConfig
        {
            Id = "follows",
            WidgetKind = WidgetKind.File,
            FollowsDefaultStoragePath = true,
            ManagedFolderName = "Work",
            MappedFolderPath = @"C:\Users\alice\WinSticker\Work",
        };
        var followsDefaultElsewhere = new WidgetConfig
        {
            Id = "elsewhere",
            WidgetKind = WidgetKind.File,
            FollowsDefaultStoragePath = true,
            MappedFolderPath = @"D:\Shared",
        };
        var customUnderSourceProfile = new WidgetConfig
        {
            Id = "custom",
            WidgetKind = WidgetKind.File,
            FollowsDefaultStoragePath = false,
            MappedFolderPath = @"C:\Users\alice\Documents\Projects",
        };
        var todoWidget = new WidgetConfig
        {
            Id = "todo",
            WidgetKind = WidgetKind.Todo,
            FollowsDefaultStoragePath = true,
            MappedFolderPath = @"C:\Users\alice\WinSticker\Todo",
        };
        var settings = new AppSettings();
        settings.WidgetLayout.Widgets.AddRange(
            [followsDefault, followsDefaultElsewhere, customUnderSourceProfile, todoWidget]);

        RestorePathRebaseResult result = RestoreMachineLocalPathRebase.Rebase(
            settings,
            AliceDataPath,
            BobProfile);

        Assert.Equal(1, result.WidgetsRebased);
        Assert.Equal(@"C:\Users\bob\WinSticker\Work", followsDefault.MappedFolderPath);
        Assert.Equal(@"D:\Shared", followsDefaultElsewhere.MappedFolderPath);
        // Custom folders are the user's own locations: rebasing them would
        // manufacture plausible-looking empty folders on this machine.
        Assert.Equal(@"C:\Users\alice\Documents\Projects", customUnderSourceProfile.MappedFolderPath);
        Assert.Equal(@"C:\Users\alice\WinSticker\Todo", todoWidget.MappedFolderPath);
    }

    [Fact]
    public void Rebase_DoesNotMatchSiblingAccountPrefixes()
    {
        var settings = new AppSettings
        {
            DefaultManagedStorageRootPath = @"C:\Users\alice2\WinSticker",
        };

        RestorePathRebaseResult result = RestoreMachineLocalPathRebase.Rebase(
            settings,
            AliceDataPath,
            BobProfile);

        Assert.True(result.Performed);
        Assert.Equal(0, result.SettingsPathsRebased);
        Assert.Equal(@"C:\Users\alice2\WinSticker", settings.DefaultManagedStorageRootPath);
    }

    [Fact]
    public void Rebase_MatchesTheSourceProfileCaseInsensitively()
    {
        var settings = new AppSettings
        {
            DefaultManagedStorageRootPath = @"c:\users\ALICE\WinSticker",
        };

        RestorePathRebaseResult result = RestoreMachineLocalPathRebase.Rebase(
            settings,
            AliceDataPath,
            BobProfile);

        Assert.Equal(1, result.SettingsPathsRebased);
        Assert.Equal(@"C:\Users\bob\WinSticker", settings.DefaultManagedStorageRootPath);
    }
}
