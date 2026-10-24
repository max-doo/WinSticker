using WinSticker.Controls;
using WinSticker.Models;
using WinSticker.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using IOFileAttributes = System.IO.FileAttributes;

namespace WinSticker.Tests;

public sealed class NativeShellFileDragProviderTests
{
    [Fact]
    public void AreExistingShortcuts_AcceptsOneOrMoreExistingShortcuts()
    {
        Assert.True(NativeShellFileDragProvider.AreExistingShortcuts(
            [@"E:\WinSticker\my\One.lnk", @"E:\WinSticker\my\Two.LNK"],
            _ => true));
    }

    [Fact]
    public void AreExistingShortcuts_RejectsMixedFileTypes()
    {
        Assert.False(NativeShellFileDragProvider.AreExistingShortcuts(
            [@"E:\WinSticker\my\One.lnk", @"E:\WinSticker\my\Readme.txt"],
            _ => true));
    }

    [Fact]
    public void CanAttachPaths_RequiresOneSharedParent()
    {
        Assert.True(NativeShellFileDragProvider.CanAttachPaths(
            [@"E:\WinSticker\my\One.lnk", @"E:\WinSticker\my\Readme.txt"],
            _ => true));
        Assert.False(NativeShellFileDragProvider.CanAttachPaths(
            [@"E:\WinSticker\my\One.lnk", @"E:\WinSticker\other\Two.lnk"],
            _ => true));
        Assert.False(NativeShellFileDragProvider.CanAttachPaths(
            [],
            _ => true));
    }

    [Fact]
    public void RequiresStorageBrokerBypass_AcceptsAllExistingShortcuts()
    {
        string[] paths = [@"E:\WinSticker\my\App.lnk"];

        Assert.True(NativeShellFileDragProvider.RequiresStorageBrokerBypass(
            paths,
            _ => true));
        Assert.False(NativeShellFileDragProvider.RequiresStorageBrokerBypass(
            [@"E:\WinSticker\my\Missing.lnk"],
            _ => false));

        Assert.False(NativeShellFileDragProvider.RequiresStorageBrokerBypass(
            [@"E:\WinSticker\my\Readme.txt"],
            _ => true));
    }

    [Fact]
    public void Provider_WrapsNativeShellDataObjectWithoutVirtualFiles()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src/WinSticker/Controls/NativeShellFileDragProvider.cs"));

        Assert.Contains("SHCreateDataObject(", source, StringComparison.Ordinal);
        Assert.Contains(
            "s_dataObjectProviderInterfaceId",
            source,
            StringComparison.Ordinal);
        Assert.Contains("CF_HDROP", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CreateStreamedFileAsync",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DragPackage_BypassesSynchronousStorageBroker_WithNativeShellData()
    {
        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "WinSticker.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string shortcutPath = Path.Combine(tempDirectory, "Hidden app.lnk");
        File.WriteAllBytes(shortcutPath, [0x4C, 0x00, 0x00, 0x00]);
        File.SetAttributes(
            shortcutPath,
            File.GetAttributes(shortcutPath) |
            IOFileAttributes.Hidden |
            IOFileAttributes.System);

        try
        {
            var dataPackage = new DataPackage();
            int brokerCallCount = 0;
            bool prepared = FileItemDragPackage.TryPrepare(
                dataPackage,
                [new WidgetItem { Path = shortcutPath, IsShortcut = true }],
                "widget-test",
                _ =>
                {
                    brokerCallCount++;
                    return Array.Empty<IStorageItem>();
                },
                _ => "Hidden app.lnk",
                out FileItemDragPackageResult result);

            Assert.True(prepared);
            Assert.True(result.UsesNativeShellDataObject);
            Assert.True(result.HasStorageItems);
            Assert.Equal(0, brokerCallCount);
            Assert.Contains(
                StandardDataFormats.StorageItems,
                dataPackage.GetView().AvailableFormats);
            Assert.Equal(
                "widget-test",
                dataPackage.GetView().Properties[
                    WinStickerDragData.SourceWidgetIdProperty]);
        }
        finally
        {
            if (File.Exists(shortcutPath))
            {
                File.SetAttributes(shortcutPath, IOFileAttributes.Normal);
            }

            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void DragPackage_UsesNativeShellForNormalShortcutWithoutBroker()
    {
        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "WinSticker.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string shortcutPath = Path.Combine(tempDirectory, "Normal app.lnk");
        File.WriteAllBytes(shortcutPath, [0x4C, 0x00, 0x00, 0x00]);

        try
        {
            var dataPackage = new DataPackage();
            int brokerCallCount = 0;
            bool prepared = FileItemDragPackage.TryPrepare(
                dataPackage,
                [new WidgetItem { Path = shortcutPath, IsShortcut = true }],
                "widget-test",
                _ =>
                {
                    brokerCallCount++;
                    return Array.Empty<IStorageItem>();
                },
                _ => "Normal app.lnk",
                out FileItemDragPackageResult result);

            Assert.True(prepared);
            Assert.True(result.UsesNativeShellDataObject);
            Assert.True(result.HasStorageItems);
            Assert.Equal(0, brokerCallCount);
            Assert.Contains(
                StandardDataFormats.StorageItems,
                dataPackage.GetView().AvailableFormats);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void DragPackage_UsesNativeShellForRegularFilesWithoutBroker()
    {
        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "WinSticker.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string firstPath = Path.Combine(tempDirectory, "One.txt");
        string secondPath = Path.Combine(tempDirectory, "Two.txt");
        File.WriteAllText(firstPath, "one");
        File.WriteAllText(secondPath, "two");

        try
        {
            var dataPackage = new DataPackage();
            int brokerCallCount = 0;
            bool prepared = FileItemDragPackage.TryPrepare(
                dataPackage,
                [
                    new WidgetItem { Path = firstPath },
                    new WidgetItem { Path = secondPath }
                ],
                "widget-test",
                _ =>
                {
                    brokerCallCount++;
                    return Array.Empty<IStorageItem>();
                },
                _ => "2",
                out FileItemDragPackageResult result);

            Assert.True(prepared);
            Assert.True(result.UsesNativeShellDataObject);
            Assert.True(result.HasStorageItems);
            Assert.Equal(0, brokerCallCount);
            Assert.Equal(2, result.SourcePaths.Count);
            Assert.Contains(
                StandardDataFormats.StorageItems,
                dataPackage.GetView().AvailableFormats);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName,
                    "src",
                    "WinSticker",
                    "WinSticker.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "WinSticker repository root was not found.");
    }
}
