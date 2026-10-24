using WinSticker.Services;
using Windows.ApplicationModel.DataTransfer;

namespace WinSticker.Tests;

public sealed class WinStickerDragDataTests
{
    [Fact]
    public async Task InternalMixedFileAndFolderDrop_PreservesEveryPath()
    {
        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "WinSticker.Tests",
            Guid.NewGuid().ToString("N"));
        string folderPath = Directory.CreateDirectory(
            Path.Combine(tempDirectory, "folder")).FullName;
        string filePath = Path.Combine(tempDirectory, "file.txt");
        File.WriteAllText(filePath, "content");

        try
        {
            var dataPackage = new DataPackage();
            dataPackage.Properties[WinStickerDragData.SourcePathsProperty] =
                new[] { filePath, folderPath };

            IReadOnlyList<DroppedFilePath> internalFiles =
                WinStickerDragData.GetInternalDroppedFiles(dataPackage.GetView());
            using DroppedFileBatch batch =
                await WinStickerDragData.TryGetDroppedFilesAsync(dataPackage.GetView());

            Assert.Equal([filePath, folderPath], internalFiles.Select(file => file.Path));
            Assert.Equal([filePath, folderPath], batch.Files.Select(file => file.Path));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
