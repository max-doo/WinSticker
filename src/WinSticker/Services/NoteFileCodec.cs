using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinSticker.Models;

namespace WinSticker.Services;

/// <summary>Outcome of decoding one candidate note file.</summary>
public enum NoteFileDecodeStatus
{
    Loaded,
    /// <summary>The file has no WinSticker header; the whole text is body.</summary>
    NoHeader,
    /// <summary>A header is present but malformed (bad JSON, bad boundary).</summary>
    CorruptHeader,
    /// <summary>The header declares a future format version this build cannot write.</summary>
    UnsupportedVersion,
    /// <summary>The header id conflicts with the id implied by the file name.</summary>
    IdMismatch
}

public sealed record NoteFileDecodeResult(
    NoteFileDecodeStatus Status,
    QuickCaptureItem? Item,
    string? Body,
    string? ErrorMessage)
{
    public NoteFileMetadata? Metadata { get; init; }

    public static NoteFileDecodeResult Error(NoteFileDecodeStatus status, string message) =>
        new(status, null, null, message);
}

/// <summary>
/// Business metadata stored in the HTML-comment header of a note file.
/// Mirrors <see cref="QuickCaptureItem"/> field-for-field except
/// <c>Body</c>, which lives after the header as raw Markdown. Unknown
/// fields round-trip through <see cref="Unrecognized"/> so files written by
/// future versions keep their data when this build re-saves them.
/// </summary>
public sealed class NoteFileMetadata
{
    public int Version { get; set; } = NoteFileCodec.CurrentVersion;
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
    public TextContentFormat ContentFormat { get; set; } = TextContentFormat.Markdown;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public QuickCaptureItemType Type { get; set; } = QuickCaptureItemType.Text;
    public string? Url { get; set; }
    public string? ImagePath { get; set; }
    public string? ContentHash { get; set; }
    public QuickCaptureSourceKind SourceKind { get; set; } = QuickCaptureSourceKind.Manual;
    public List<string>? Tags { get; set; }
    public string? DeviceId { get; set; }
    public QuickCaptureAppearancePreset AppearancePreset { get; set; }
    public bool IsPinned { get; set; }
    public int SortOrder { get; set; }
    public int PinnedSortOrder { get; set; } = -1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public List<TodoAttachment>? Attachments { get; set; }
    public bool IsNote { get; set; } = true;
    public bool IsRecent { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unrecognized { get; set; }
}

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(NoteFileMetadata), TypeInfoPropertyName = "NoteFileMetadata")]
[JsonSerializable(
    typeof(QuickCaptureNoteMigration.MigrationProgress),
    TypeInfoPropertyName = "NoteMigrationProgress")]
internal sealed partial class NoteFileJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Encodes and decodes the single-note file format: a fixed HTML-comment
/// header whose second line is one compact JSON document, then exactly one
/// separator newline, then the verbatim body. The header is recognized only
/// at byte zero, so identical comments inside the body stay body.
/// </summary>
public static class NoteFileCodec
{
    public const int CurrentVersion = 1;
    public const string HeaderFirstLine = "<!-- winsticker-note";
    public const string HeaderEndLine = "-->";
    public const string FileExtension = ".md";

    // Strict decoding: invalid UTF-8 must surface as an error instead of
    // being silently replaced and written back over the source file.
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static string Encode(QuickCaptureItem item, string notesDirectory) =>
        Encode(item, notesDirectory, baseMetadata: null);

    /// <param name="baseMetadata">
    /// Metadata previously decoded from the same file: unrecognized fields
    /// carried there are written back so a round trip through this build
    /// never drops fields a future version added.
    /// </param>
    public static string Encode(
        QuickCaptureItem item,
        string notesDirectory,
        NoteFileMetadata? baseMetadata)
    {
        NoteFileMetadata metadata = ToMetadata(item, notesDirectory);
        if (baseMetadata?.Unrecognized is { Count: > 0 } extras)
        {
            var carried = new Dictionary<string, JsonElement>();
            foreach (KeyValuePair<string, JsonElement> extra in extras)
            {
                // A field this build has learned to write wins over the
                // stale copy; anything still unknown rides along verbatim.
                if (!KnownMetadataKeys.Contains(extra.Key))
                {
                    carried[extra.Key] = extra.Value;
                }
            }

            if (carried.Count > 0)
            {
                metadata.Unrecognized = carried;
            }
        }

        // Single-line JSON with the default JavaScript encoder: '<', '>' and
        // '&' inside strings become \u003C/\u003E/\u0026, so no title, tag or
        // attachment name can terminate the comment early, and a raw newline
        // can never appear inside a JSON string. Keep both properties.
        string json = JsonSerializer.Serialize(
            metadata,
            NoteFileJsonContext.Default.NoteFileMetadata);
        return $"{HeaderFirstLine}\n{json}\n{HeaderEndLine}\n{item.Body ?? string.Empty}";
    }

    public static byte[] EncodeUtf8(QuickCaptureItem item, string notesDirectory) =>
        StrictUtf8.GetBytes(Encode(item, notesDirectory));

    public static byte[] EncodeUtf8(
        QuickCaptureItem item,
        string notesDirectory,
        NoteFileMetadata? baseMetadata) =>
        StrictUtf8.GetBytes(Encode(item, notesDirectory, baseMetadata));

    private static readonly HashSet<string> KnownMetadataKeys = new(StringComparer.Ordinal)
    {
        "version", "id", "title", "contentFormat", "createdAt", "updatedAt", "type",
        "url", "imagePath", "contentHash", "sourceKind", "tags", "deviceId",
        "appearancePreset", "isPinned", "sortOrder", "pinnedSortOrder", "isDeleted",
        "archivedAt", "attachments", "isNote", "isRecent"
    };

    /// <summary>
    /// Decodes raw file bytes. Throws <see cref="DecoderFallbackException"/>
    /// for invalid UTF-8 — callers must route that into single-file error
    /// handling, never re-encode a replacement string over the file.
    /// </summary>
    public static NoteFileDecodeResult Decode(
        ReadOnlySpan<byte> utf8Bytes,
        string fileNameNoteId,
        string notesDirectory)
    {
        // A UTF-8 BOM is the U+FEFF character; its u8 literal is exactly EF BB BF.
        if (utf8Bytes.StartsWith("\uFEFF"u8))
        {
            utf8Bytes = utf8Bytes[3..];
        }

        string text = StrictUtf8.GetString(utf8Bytes);
        if (!text.StartsWith(HeaderFirstLine, StringComparison.Ordinal))
        {
            return new NoteFileDecodeResult(
                NoteFileDecodeStatus.NoHeader,
                ItemFromHeaderless(text, fileNameNoteId, notesDirectory),
                text,
                null);
        }

        (int? headerEnd, string? error) = FindHeaderBoundary(text);
        if (headerEnd is null)
        {
            return NoteFileDecodeResult.Error(NoteFileDecodeStatus.CorruptHeader, error!);
        }

        int firstLineEnd = text.IndexOf('\n');
        int secondLineEnd = text.IndexOf('\n', firstLineEnd + 1);
        string jsonLine = text[(firstLineEnd + 1)..secondLineEnd].TrimEnd('\r');

        NoteFileMetadata metadata;
        try
        {
            metadata = JsonSerializer.Deserialize(
                    jsonLine,
                    NoteFileJsonContext.Default.NoteFileMetadata) ??
                throw new JsonException("The note header deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            return NoteFileDecodeResult.Error(
                NoteFileDecodeStatus.CorruptHeader,
                $"The note header is not valid metadata JSON: {ex.Message}");
        }

        if (metadata.Version > CurrentVersion)
        {
            return NoteFileDecodeResult.Error(
                NoteFileDecodeStatus.UnsupportedVersion,
                $"The note file format version {metadata.Version} is newer than supported version {CurrentVersion}.");
        }

        string noteId = metadata.Id?.Trim() ?? string.Empty;
        if (noteId.Length == 0)
        {
            noteId = fileNameNoteId;
        }
        else if (!string.Equals(noteId, fileNameNoteId, StringComparison.Ordinal))
        {
            return NoteFileDecodeResult.Error(
                NoteFileDecodeStatus.IdMismatch,
                $"The header id '{noteId}' does not match the file identity '{fileNameNoteId}'.");
        }

        // Exactly one separator newline after "-->" is header, not body.
        string body = text[(headerEnd.Value + 1)..];
        return new NoteFileDecodeResult(
            NoteFileDecodeStatus.Loaded,
            ToItem(metadata, noteId, body, notesDirectory),
            body,
            null)
        {
            Metadata = metadata
        };
    }

    /// <summary>
    /// Returns the index just past the single newline that follows the
    /// "-->" line, or an error when the boundary is malformed.
    /// </summary>
    private static (int? HeaderEndExclusive, string? Error) FindHeaderBoundary(string text)
    {
        int firstLineEnd = text.IndexOf('\n');
        int secondLineEnd = text.IndexOf('\n', firstLineEnd + 1);
        if (firstLineEnd < 0 || secondLineEnd < 0)
        {
            return (null, "The note header ends before the JSON line.");
        }

        if (text[..firstLineEnd].TrimEnd('\r') != HeaderFirstLine)
        {
            return (null, "The first header line does not match the WinSticker note marker.");
        }

        int thirdLineEnd = text.IndexOf('\n', secondLineEnd + 1);
        if (thirdLineEnd < 0)
        {
            return (null, "The note header has no closing boundary line.");
        }

        if (text[(secondLineEnd + 1)..thirdLineEnd].TrimEnd('\r') != HeaderEndLine)
        {
            return (null, "The JSON line is not followed by the header end marker.");
        }

        return (thirdLineEnd, null);
    }

    internal static NoteFileMetadata ToMetadata(QuickCaptureItem item, string notesDirectory)
    {
        return new NoteFileMetadata
        {
            Version = CurrentVersion,
            Id = item.Id,
            Title = item.Title,
            ContentFormat = item.ContentFormat,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            Type = item.Type,
            Url = item.Url,
            ImagePath = RelativizeManagedPath(item.ImagePath, notesDirectory),
            ContentHash = item.ContentHash,
            SourceKind = item.SourceKind,
            Tags = item.Tags is { Count: > 0 } ? [.. item.Tags] : [],
            DeviceId = item.DeviceId,
            AppearancePreset = item.AppearancePreset,
            IsPinned = item.IsPinned,
            SortOrder = item.SortOrder,
            PinnedSortOrder = item.PinnedSortOrder,
            IsDeleted = item.IsDeleted,
            ArchivedAt = item.ArchivedAt,
            Attachments = item.Attachments is { Count: > 0 }
                ? item.Attachments.Select(attachment => CloneWithRelativePath(attachment, notesDirectory)).ToList()
                : [],
            IsNote = item.IsNote,
            IsRecent = item.IsRecent
        };
    }

    private static TodoAttachment CloneWithRelativePath(TodoAttachment attachment, string notesDirectory)
    {
        var clone = attachment.Clone();
        clone.FilePath = RelativizeManagedPath(attachment.FilePath, notesDirectory) ?? string.Empty;
        return clone;
    }

    private static TodoAttachment CloneWithResolvedPath(TodoAttachment attachment, string notesDirectory)
    {
        var clone = attachment.Clone();
        clone.FilePath = ResolveManagedPath(attachment.FilePath, notesDirectory) ?? string.Empty;
        return clone;
    }

    internal static QuickCaptureItem ToItem(
        NoteFileMetadata metadata,
        string noteId,
        string body,
        string notesDirectory)
    {
        return new QuickCaptureItem
        {
            Id = noteId,
            Title = metadata.Title,
            Body = body,
            ContentFormat = metadata.ContentFormat,
            CreatedAt = metadata.CreatedAt,
            UpdatedAt = metadata.UpdatedAt,
            Type = metadata.Type,
            Url = metadata.Url,
            ImagePath = ResolveManagedPath(metadata.ImagePath, notesDirectory),
            ContentHash = metadata.ContentHash,
            SourceKind = metadata.SourceKind,
            Tags = metadata.Tags ?? [],
            DeviceId = metadata.DeviceId,
            AppearancePreset = metadata.AppearancePreset,
            IsPinned = metadata.IsPinned,
            SortOrder = metadata.SortOrder,
            PinnedSortOrder = metadata.PinnedSortOrder,
            IsDeleted = metadata.IsDeleted,
            ArchivedAt = metadata.ArchivedAt,
            Attachments = metadata.Attachments?.Select(attachment => CloneWithResolvedPath(attachment, notesDirectory)).ToList() ?? [],
            IsNote = metadata.IsNote,
            IsRecent = metadata.IsRecent
        };
    }

    private static QuickCaptureItem ItemFromHeaderless(
        string body,
        string fileNameNoteId,
        string notesDirectory)
    {
        // Cold start without a header: keep the file-name identity and the
        // body, use default business fields; a later save writes the header
        // back. Cold-start defaults, not corruption.
        var item = ToItem(
            new NoteFileMetadata { Id = fileNameNoteId },
            fileNameNoteId,
            body,
            notesDirectory);
        item.ContentFormat = TextContentFormat.PlainText;
        item.IsNote = true;
        return item;
    }

    /// <summary>
    /// Managed references that live inside the quick-capture root are stored
    /// relative to the note file's directory (e.g. "../attachments/&lt;id&gt;/f.png")
    /// so a data directory move or backup restore keeps them valid. External
    /// linked files keep their absolute paths.
    /// </summary>
    internal static string? RelativizeManagedPath(string? path, string notesDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            string trimmed = path.Trim();
            if (!Path.IsPathRooted(trimmed))
            {
                return trimmed;
            }

            string full = Path.GetFullPath(trimmed);
            string root = QuickCaptureRoot(notesDirectory);
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return full;
            }

            return Path.GetRelativePath(notesDirectory, full);
        }
        catch
        {
            return path;
        }
    }

    internal static string? ResolveManagedPath(string? path, string notesDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string trimmed = path.Trim();
        try
        {
            if (!Path.IsPathRooted(trimmed))
            {
                return Path.GetFullPath(Path.Combine(notesDirectory, trimmed));
            }
        }
        catch
        {
            // Fall through and return the trimmed value.
        }

        return trimmed;
    }

    private static string QuickCaptureRoot(string notesDirectory)
    {
        string root = Path.GetFullPath(Path.GetDirectoryName(notesDirectory.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))!);
        return root;
    }
}
