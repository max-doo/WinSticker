using System.Security.Cryptography;
using System.Text;
using WinSticker.Models;

namespace WinSticker.Services;

public enum NoteFileLoadStatus
{
    Loaded,
    Missing,
    NoHeader,
    CorruptHeader,
    UnsupportedVersion,
    IdMismatch,
    IoError
}

public sealed record NoteFileLoadResult(
    NoteFileLoadStatus Status,
    QuickCaptureItem? Item,
    string? FileName,
    string? NoteId,
    string? ErrorMessage)
{
    internal static NoteFileLoadResult Failure(
        NoteFileLoadStatus status,
        string noteId,
        string? fileName,
        string message) =>
        new(status, null, fileName, noteId, message);
}

public enum NoteFileCommitStatus
{
    Saved,
    /// <summary>The file changed on disk since our last read/write; nothing was written.</summary>
    Conflict
}

public sealed record NoteFileCommitResult(NoteFileCommitStatus Status, string? ErrorMessage);

/// <summary>
/// Reads and commits one file per note under &lt;quick-capture&gt;/notes.
/// Commits reuse the <see cref="ResilientJsonStore"/> protocol (unique temp
/// file, atomic replace, previous-version .bak, verified in-place fallback)
/// so note files and JSON stores share one reliability contract instead of
/// a second, divergent write path. A per-id content fingerprint detects
/// external edits between our last read and the next commit; a conflict
/// writes nothing and leaves both versions available.
/// </summary>
public sealed class NoteFileStore
{
    public const string NotesDirectoryName = "notes";
    public const string MigrationDirectoryName = "notes-migration";
    private const int MaxFileNameIdLength = 96;

    private readonly string _quickCaptureDirectory;
    private readonly string _notesDirectory;
    private readonly object _fingerprintLock = new();
    private readonly Dictionary<string, byte[]> _knownHashes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NoteFileMetadata> _loadedMetadata = new(StringComparer.Ordinal);

    public NoteFileStore(string quickCaptureDirectory)
    {
        _quickCaptureDirectory = quickCaptureDirectory;
        _notesDirectory = Path.Combine(quickCaptureDirectory, NotesDirectoryName);
    }

    public string QuickCaptureDirectory => _quickCaptureDirectory;

    public string NotesDirectory => _notesDirectory;

    public static string BuildNoteFileName(string noteId) =>
        (IsSafeFileNameSegment(noteId) ? noteId : EncodeSegment(noteId)) + NoteFileCodec.FileExtension;

    public string GetNoteFilePath(string noteId) =>
        Path.Combine(_notesDirectory, BuildNoteFileName(noteId));

    /// <summary>
    /// Maps a file in the notes directory back to its note id. Returns false
    /// for temp/backup files and anything that is not a note document.
    /// </summary>
    public static bool TryGetNoteIdFromFileName(string fileName, out string noteId)
    {
        noteId = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        string name = Path.GetFileName(fileName);
        if (!name.EndsWith(NoteFileCodec.FileExtension, StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(NoteFileCodec.FileExtension + ".bak", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string segment = name[..^NoteFileCodec.FileExtension.Length];
        if (segment.Length == 0)
        {
            return false;
        }

        if (segment[0] == '~')
        {
            string hex = segment[1..];
            if (hex.Length == 0 || hex.Length % 2 != 0 || hex.Any(c => !Uri.IsHexDigit(c)))
            {
                return false;
            }

            noteId = DecodeSegment(hex);
            return noteId.Length > 0;
        }

        noteId = segment;
        return true;
    }

    public async Task<NoteFileLoadResult> LoadAsync(string noteId)
    {
        string path = GetNoteFilePath(noteId);
        byte[] bytes;
        try
        {
            if (!File.Exists(path))
            {
                return NoteFileLoadResult.Failure(
                    NoteFileLoadStatus.Missing, noteId, Path.GetFileName(path),
                    "The note file does not exist.");
            }

            bytes = await File.ReadAllBytesAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return NoteFileLoadResult.Failure(
                NoteFileLoadStatus.IoError, noteId, Path.GetFileName(path), ex.Message);
        }

        NoteFileLoadResult result = DecodeBytes(bytes, noteId, path);
        if (result.Status is not (NoteFileLoadStatus.Loaded or NoteFileLoadStatus.NoHeader))
        {
            return result;
        }

        RememberFingerprint(noteId, SHA256.HashData(bytes));
        return result;
    }

    private NoteFileLoadResult DecodeBytes(byte[] bytes, string noteId, string path)
    {
        string fileName = Path.GetFileName(path);
        try
        {
            NoteFileDecodeResult decoded = NoteFileCodec.Decode(bytes, noteId, GetNotesDirectoryFor(path));
            if (decoded.Status is NoteFileDecodeStatus.Loaded && decoded.Metadata is { } metadata)
            {
                lock (_fingerprintLock)
                {
                    _loadedMetadata[noteId] = metadata;
                }
            }

            return decoded.Status switch
            {
                NoteFileDecodeStatus.Loaded => new NoteFileLoadResult(
                    NoteFileLoadStatus.Loaded, decoded.Item, fileName, noteId, null),
                NoteFileDecodeStatus.NoHeader => new NoteFileLoadResult(
                    NoteFileLoadStatus.NoHeader, decoded.Item, fileName, noteId, null),
                NoteFileDecodeStatus.UnsupportedVersion => NoteFileLoadResult.Failure(
                    NoteFileLoadStatus.UnsupportedVersion, noteId, fileName, decoded.ErrorMessage!),
                NoteFileDecodeStatus.IdMismatch => NoteFileLoadResult.Failure(
                    NoteFileLoadStatus.IdMismatch, noteId, fileName, decoded.ErrorMessage!),
                _ => NoteFileLoadResult.Failure(
                    NoteFileLoadStatus.CorruptHeader, noteId, fileName, decoded.ErrorMessage!)
            };
        }
        catch (Exception ex) when (ex is DecoderFallbackException or ArgumentException)
        {
            // Invalid UTF-8: preserve the file and report; never re-encode a
            // replacement string over the source bytes.
            return NoteFileLoadResult.Failure(
                NoteFileLoadStatus.CorruptHeader, noteId, fileName,
                $"The note file is not valid UTF-8: {ex.Message}");
        }
    }

    private static string GetNotesDirectoryFor(string noteFilePath) =>
        Path.GetDirectoryName(noteFilePath)!;

    public async Task<NoteFileCommitResult> SaveAsync(
        QuickCaptureItem item,
        bool bypassConflictCheck = false)
    {
        string path = GetNoteFilePath(item.Id);
        NoteFileMetadata? baseMetadata;
        lock (_fingerprintLock)
        {
            _loadedMetadata.TryGetValue(item.Id, out baseMetadata);
        }

        byte[] bytes = NoteFileCodec.EncodeUtf8(item, _notesDirectory, baseMetadata);
        byte[] newHash = SHA256.HashData(bytes);

        Directory.CreateDirectory(_notesDirectory);
        if (!bypassConflictCheck && File.Exists(path))
        {
            byte[]? known = LookupFingerprint(item.Id);
            if (known is null)
            {
                // Never loaded through this instance: adopt the on-disk bytes
                // as the baseline before the first overwrite.
                known = await TryHashFileAsync(path) ?? [];
            }

            if (known.Length > 0)
            {
                byte[]? current = await TryHashFileAsync(path);
                if (current is not null && !current.AsSpan().SequenceEqual(known))
                {
                    return new NoteFileCommitResult(
                        NoteFileCommitStatus.Conflict,
                        $"The note file '{Path.GetFileName(path)}' changed outside WinSticker.");
                }
            }
        }

        try
        {
            await ResilientJsonStore.SaveAsync(
                path,
                async tempPath => await File.WriteAllBytesAsync(tempPath, bytes));
        }
        catch (Exception ex)
        {
            return new NoteFileCommitResult(
                NoteFileCommitStatus.Conflict,
                $"The note file '{Path.GetFileName(path)}' could not be committed: {ex.Message}");
        }

        RememberFingerprint(item.Id, newHash);
        return new NoteFileCommitResult(NoteFileCommitStatus.Saved, null);
    }

    public async Task<IReadOnlyList<NoteFileLoadResult>> LoadAllAsync()
    {
        var results = new List<NoteFileLoadResult>();
        if (!Directory.Exists(_notesDirectory))
        {
            return results;
        }

        // Sequential async reads: note counts are three orders of magnitude
        // below where unbounded task fan-out would pay for itself, and one
        // read in flight keeps cold start predictable on slow storage.
        foreach (string file in Directory
                     .EnumerateFiles(_notesDirectory, "*", SearchOption.TopDirectoryOnly)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!TryGetNoteIdFromFileName(Path.GetFileName(file), out string noteId))
            {
                continue;
            }

            results.Add(await LoadAsync(noteId));
        }

        return results;
    }

    public bool FileExists(string noteId) => File.Exists(GetNoteFilePath(noteId));

    /// <summary>Removes a note document and its backup; for tombstone cap maintenance.</summary>
    public void TryDeleteNoteFiles(string noteId)
    {
        foreach (string path in new[] { GetNoteFilePath(noteId), GetNoteFilePath(noteId) + ".bak" })
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                App.Log($"[NoteFileStore] Failed to delete '{Path.GetFileName(path)}': {ex.Message}");
            }
        }

        lock (_fingerprintLock)
        {
            _knownHashes.Remove(noteId);
            _loadedMetadata.Remove(noteId);
        }
    }

    internal byte[]? LookupFingerprint(string noteId)
    {
        lock (_fingerprintLock)
        {
            return _knownHashes.TryGetValue(noteId, out byte[]? hash) ? hash : null;
        }
    }

    private void RememberFingerprint(string noteId, byte[] hash)
    {
        lock (_fingerprintLock)
        {
            _knownHashes[noteId] = hash;
        }
    }

    private static async Task<byte[]?> TryHashFileAsync(string path)
    {
        try
        {
            return SHA256.HashData(await File.ReadAllBytesAsync(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsSafeFileNameSegment(string noteId)
    {
        if (string.IsNullOrWhiteSpace(noteId) ||
            noteId.Length > MaxFileNameIdLength ||
            noteId[0] == '~' ||
            noteId.EndsWith('.') ||
            noteId.EndsWith(' '))
        {
            return false;
        }

        if (noteId.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (char c in noteId)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-' or '.'))
            {
                return false;
            }
        }

        return !IsReservedDeviceName(noteId);
    }

    private static bool IsReservedDeviceName(string segment)
    {
        // Windows reserves the stem before the first dot ("nul.txt" is as
        // reserved as "nul").
        int dot = segment.IndexOf('.');
        string name = dot > 0 ? segment[..dot] : segment;
        return name.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
               (name.Length == 4 &&
                (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                char.IsAsciiDigit(name[3]));
    }

    // Reversible safe-name encoding for historical ids that cannot map to a
    // file name directly: '~' marks an encoded segment, the payload is the
    // UTF-8 hex of the original id. Literal ids never start with '~', so the
    // two namespaces cannot collide.
    private static string EncodeSegment(string noteId)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(noteId);
        if (bytes.Length * 2 > MaxFileNameIdLength)
        {
            throw new InvalidOperationException(
                $"The note id cannot be encoded into a file name (length {bytes.Length * 2}).");
        }

        return "~" + Convert.ToHexStringLower(bytes);
    }

    private static string DecodeSegment(string hex)
    {
        byte[] bytes = Convert.FromHexString(hex);
        return Encoding.UTF8.GetString(bytes);
    }
}
