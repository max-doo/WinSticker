using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using WinSticker.Models;

namespace WinSticker.Services;

public sealed class WinStickerAttachmentHealthService
{
    private static readonly QuickCaptureJsonContext s_quickCaptureDataJsonContext =
        new(CreateDataJsonOptions());
    private static readonly TodoJsonContext s_todoDataJsonContext =
        new(CreateDataJsonOptions());

    private readonly string _dataDirectory;

    public WinStickerAttachmentHealthService()
        : this(WinStickerDataPathService.Current.DataDirectory)
    {
    }

    internal WinStickerAttachmentHealthService(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _dataDirectory = Path.GetFullPath(dataDirectory);
    }

    public Task<WinStickerAttachmentHealthReport> ScanAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ScanCore(cancellationToken), cancellationToken);
    }

    private WinStickerAttachmentHealthReport ScanCore(CancellationToken cancellationToken)
    {
        var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingLinkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingManagedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int unreadableStoreCount = 0;

        string quickCapturePath = Path.Combine(
            _dataDirectory,
            "quick-capture",
            "quick-capture.json");
        if (File.Exists(quickCapturePath))
        {
            try
            {
                QuickCaptureStoreData data = ReadJson<QuickCaptureStoreData>(
                    quickCapturePath,
                    s_quickCaptureDataJsonContext.StoreData);
                AddAttachments(
                    (data.Items ?? []).Concat(data.RecentItems ?? [])
                    .SelectMany(item => item.Attachments ?? []),
                    referencedPaths,
                    missingLinkedPaths,
                    missingManagedPaths);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                unreadableStoreCount++;
                App.Log($"[AttachmentHealth] Failed to read '{quickCapturePath}': {ex.Message}");
            }
        }

        string widgetsDirectory = Path.Combine(_dataDirectory, "widgets");
        if (Directory.Exists(widgetsDirectory))
        {
            foreach (string todoPath in Directory.EnumerateFiles(
                         widgetsDirectory,
                         "todo.json",
                         SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    TodoWidgetData data = ReadJson<TodoWidgetData>(
                        todoPath,
                        s_todoDataJsonContext.StoreData);
                    AddAttachments(
                        (data.Items ?? []).SelectMany(item => item.Attachments ?? []),
                        referencedPaths,
                        missingLinkedPaths,
                        missingManagedPaths);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    unreadableStoreCount++;
                    App.Log($"[AttachmentHealth] Failed to read '{todoPath}': {ex.Message}");
                }
            }
        }

        var orphanManagedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string managedDirectory in EnumerateManagedAttachmentDirectories(widgetsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (string filePath in Directory.EnumerateFiles(
                         managedDirectory,
                         "*",
                         SearchOption.AllDirectories))
            {
                string normalizedPath = Path.GetFullPath(filePath);
                if (!referencedPaths.Contains(normalizedPath))
                {
                    orphanManagedPaths.Add(normalizedPath);
                }
            }
        }

        return new WinStickerAttachmentHealthReport(
            referencedPaths.Count,
            missingLinkedPaths.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            missingManagedPaths.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            orphanManagedPaths.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            unreadableStoreCount);
    }

    private IEnumerable<string> EnumerateManagedAttachmentDirectories(string widgetsDirectory)
    {
        string quickCaptureAttachments = Path.Combine(_dataDirectory, "quick-capture", "attachments");
        if (Directory.Exists(quickCaptureAttachments))
        {
            yield return quickCaptureAttachments;
        }

        if (!Directory.Exists(widgetsDirectory))
        {
            yield break;
        }

        foreach (string widgetDirectory in Directory.EnumerateDirectories(widgetsDirectory))
        {
            string attachmentDirectory = Path.Combine(widgetDirectory, "attachments");
            if (Directory.Exists(attachmentDirectory))
            {
                yield return attachmentDirectory;
            }
        }
    }

    private static JsonSerializerOptions CreateDataJsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static T ReadJson<T>(string path, JsonTypeInfo<T> jsonTypeInfo)
    {
        return JsonSerializer.Deserialize(File.ReadAllText(path), jsonTypeInfo) ??
               throw new JsonException("The JSON document contains null.");
    }

    private static void AddAttachments(
        IEnumerable<TodoAttachment> attachments,
        HashSet<string> referencedPaths,
        HashSet<string> missingLinkedPaths,
        HashSet<string> missingManagedPaths)
    {
        foreach (TodoAttachment attachment in attachments.Where(attachment =>
                     attachment is not null && !string.IsNullOrWhiteSpace(attachment.FilePath)))
        {
            string path;
            try
            {
                path = Path.GetFullPath(attachment.FilePath);
            }
            catch
            {
                path = attachment.FilePath;
            }

            referencedPaths.Add(path);
            if (File.Exists(path))
            {
                continue;
            }

            if (attachment.IsManagedCopy)
            {
                missingManagedPaths.Add(path);
            }
            else
            {
                missingLinkedPaths.Add(path);
            }
        }
    }
}

public sealed record WinStickerAttachmentHealthReport(
    int ReferencedFileCount,
    IReadOnlyList<string> MissingLinkedFiles,
    IReadOnlyList<string> MissingManagedFiles,
    IReadOnlyList<string> OrphanManagedFiles,
    int UnreadableStoreCount)
{
    public bool IsHealthy =>
        MissingLinkedFiles.Count == 0 &&
        MissingManagedFiles.Count == 0 &&
        OrphanManagedFiles.Count == 0 &&
        UnreadableStoreCount == 0;
}
