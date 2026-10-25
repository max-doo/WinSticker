using System.Security.Cryptography;
using System.Text.Json;
using WinSticker.Models;

namespace WinSticker.Services;

/// <summary>
/// One-way migration from the legacy aggregate quick-capture.json (notes
/// embedded in <c>Items</c>) to independent note files under notes/.
///
/// Protocol (plan §6): recovery copy first, generate into a sibling staging
/// directory, re-read and verify every generated file against the source,
/// publish the directory, then commit the new aggregate JSON whose
/// notesStorageVersion marker is the only authoritative switch. Any failure
/// aborts before the marker is written, leaving the old format readable and
/// the staging tree reusable for an idempotent retry.
/// </summary>
internal static class QuickCaptureNoteMigration
{
    internal sealed record Result(int MigratedNotes, int RetainedRecords);

    internal const string RecoveryCopySuffix = ".pre-notes-migration";
    internal const string ProgressFileName = "notes-migration.json";

    internal sealed record MigrationProgress(
        string SourceSha256,
        int SourceNotesStorageVersion,
        List<string> GeneratedNoteIds,
        int TotalNotes);

    public static async Task<QuickCaptureStoreData> MigrateAsync(
        QuickCaptureStore store,
        NoteFileStore noteFiles,
        QuickCaptureStoreData source)
    {
        string sourceDirectory = store.DataDirectory;
        string sourceJsonPath = store.StorePath;

        List<QuickCaptureItem> notes = source.Items
            .Where(item => item is not null && !item.IsDeleted && NoteWorkspace.IsNote(item))
            .ToList();
        List<QuickCaptureItem> retained = source.Items
            .Where(item => item is not null && !(item is { IsDeleted: false } note && NoteWorkspace.IsNote(note)))
            .ToList();

        if (notes.Count == 0 && source.Items.Count == 0 && !File.Exists(noteFiles.NotesDirectory))
        {
            // Fresh profile: no payload to move, just write the marker.
            var fresh = BuildMigratedData(source, retained);
            await store.SaveAsync(fresh);
            return fresh;
        }

        if (notes.Select(note => note.Id).Distinct(StringComparer.Ordinal).Count() != notes.Count)
        {
            throw new InvalidOperationException(
                "The legacy quick-capture store contains duplicate note ids; migration aborted.");
        }

        byte[] sourceBytes = await File.ReadAllBytesAsync(sourceJsonPath);
        string sourceSha256 = Convert.ToHexStringLower(SHA256.HashData(sourceBytes));
        await WriteRecoveryCopyAsync(sourceJsonPath, sourceSha256, source.NotesStorageVersion, notes);

        // Always regenerate staging from the current source so a stale file
        // from an aborted attempt can never leak into the published set.
        string stagingDirectory = Path.Combine(sourceDirectory, NoteFileStore.MigrationDirectoryName);
        if (Directory.Exists(stagingDirectory))
        {
            Directory.Delete(stagingDirectory, recursive: true);
        }

        Directory.CreateDirectory(stagingDirectory);
        foreach (QuickCaptureItem note in notes)
        {
            string path = Path.Combine(
                stagingDirectory,
                NoteFileStore.BuildNoteFileName(note.Id));
            await File.WriteAllBytesAsync(path, NoteFileCodec.EncodeUtf8(note, stagingDirectory));
        }

        // The progress note lives beside the store file, not inside the
        // staging tree, so publishing moves exactly the note documents.
        await File.WriteAllTextAsync(
            Path.Combine(sourceDirectory, ProgressFileName),
            JsonSerializer.Serialize(
                new MigrationProgress(
                    sourceSha256,
                    source.NotesStorageVersion,
                    notes.Select(note => note.Id).ToList(),
                    notes.Count),
                NoteFileJsonContext.Default.NoteMigrationProgress));

        await VerifyStagedFilesAsync(stagingDirectory, notes);

        PublishStagedDirectory(stagingDirectory, noteFiles.NotesDirectory);

        var migrated = BuildMigratedData(source, retained);
        await store.SaveAsync(migrated);

        await VerifyPublishedStateAsync(noteFiles, migrated, notes, retained);

        App.Log(
            $"[QuickCaptureNoteMigration] Migrated {notes.Count} note(s) to independent files; " +
            $"{retained.Count} record(s) stay in the aggregate store.");
        return migrated;
    }

    private static QuickCaptureStoreData BuildMigratedData(
        QuickCaptureStoreData source,
        List<QuickCaptureItem> retainedRecords)
    {
        return new QuickCaptureStoreData
        {
            Version = QuickCaptureStore.CurrentVersion,
            NotesStorageVersion = QuickCaptureStore.CurrentNotesStorageVersion,
            ClosedQuickPanelNotes = source.ClosedQuickPanelNotes is null
                ? null
                : new(source.ClosedQuickPanelNotes, StringComparer.Ordinal),
            CurrentView = source.CurrentView,
            Items = retainedRecords.ToList(),
            RecentItems = source.RecentItems.ToList()
        };
    }

    private static async Task WriteRecoveryCopyAsync(
        string sourceJsonPath,
        string sourceSha256,
        int sourceNotesStorageVersion,
        List<QuickCaptureItem> notes)
    {
        // An independent recovery copy with the source fingerprint: the
        // store's own .bak rotates on the next save and cannot serve as the
        // migration undo record.
        string recoveryPath = sourceJsonPath + RecoveryCopySuffix;
        if (!File.Exists(recoveryPath))
        {
            File.Copy(sourceJsonPath, recoveryPath, overwrite: false);
        }

        string infoJson = JsonSerializer.Serialize(
            new MigrationProgress(
                sourceSha256,
                sourceNotesStorageVersion,
                notes.Select(note => note.Id).ToList(),
                notes.Count),
            NoteFileJsonContext.Default.NoteMigrationProgress);
        await File.WriteAllTextAsync(recoveryPath + ".info.json", infoJson);
    }

    private static async Task VerifyStagedFilesAsync(string stagingDirectory, List<QuickCaptureItem> notes)
    {
        foreach (QuickCaptureItem note in notes)
        {
            string path = Path.Combine(stagingDirectory, NoteFileStore.BuildNoteFileName(note.Id));
            NoteFileDecodeResult decoded;
            try
            {
                decoded = NoteFileCodec.Decode(
                    await File.ReadAllBytesAsync(path),
                    note.Id,
                    stagingDirectory);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"The staged note file for '{note.Id}' could not be read back: {ex.Message}", ex);
            }

            if (decoded.Status != NoteFileDecodeStatus.Loaded || decoded.Item is null)
            {
                throw new InvalidOperationException(
                    $"The staged note file for '{note.Id}' failed verification: {decoded.Status} {decoded.ErrorMessage}");
            }

            QuickCaptureItem round = decoded.Item;
            if (!string.Equals(round.Id, note.Id, StringComparison.Ordinal) ||
                !string.Equals(round.Body, note.Body, StringComparison.Ordinal) ||
                !string.Equals(round.Title, note.Title, StringComparison.Ordinal) ||
                round.ContentFormat != note.ContentFormat ||
                round.CreatedAt != note.CreatedAt ||
                round.UpdatedAt != note.UpdatedAt ||
                round.SortOrder != note.SortOrder ||
                round.PinnedSortOrder != note.PinnedSortOrder ||
                round.IsPinned != note.IsPinned ||
                round.IsDeleted ||
                round.AppearancePreset != note.AppearancePreset ||
                round.Attachments.Count != note.Attachments.Count ||
                !round.Attachments.Select(a => Path.GetFullPath(a.FilePath)).SequenceEqual(
                    note.Attachments.Select(a => Path.GetFullPath(a.FilePath)), StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The staged note file for '{note.Id}' does not round-trip the source record.");
            }
        }
    }

    /// <summary>
    /// Publishes the staging tree as the notes directory. When the target
    /// already exists (a prior attempt died between publish and marker
    /// write), publication only continues after proving the two trees hold
    /// the same file set with identical bytes — a directory of unknown
    /// provenance aborts the migration instead of being overwritten.
    /// </summary>
    private static void PublishStagedDirectory(string stagingDirectory, string notesDirectory)
    {
        if (Directory.Exists(notesDirectory))
        {
            if (!DirectoriesMatch(stagingDirectory, notesDirectory))
            {
                throw new InvalidOperationException(
                    $"An existing notes directory '{notesDirectory}' does not match this migration's " +
                    "staged output; refusing to overwrite it.");
            }

            Directory.Delete(stagingDirectory, recursive: true);
            return;
        }

        Directory.Move(stagingDirectory, notesDirectory);
    }

    private static bool DirectoriesMatch(string stagingDirectory, string notesDirectory)
    {
        var staged = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            staged[Path.GetFileName(file)] = File.ReadAllBytes(file);
        }

        foreach (string file in Directory.EnumerateFiles(notesDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(file);
            if (!staged.TryGetValue(name, out byte[]? stagedBytes) ||
                !File.ReadAllBytes(file).AsSpan().SequenceEqual(stagedBytes))
            {
                return false;
            }

            staged.Remove(name);
        }

        return staged.Count == 0;
    }

    private static async Task VerifyPublishedStateAsync(
        NoteFileStore noteFiles,
        QuickCaptureStoreData migrated,
        List<QuickCaptureItem> notes,
        List<QuickCaptureItem> retainedRecords)
    {
        // Reload from disk through the same path the service will use and
        // confirm the merged library is exactly the source library.
        QuickCaptureStoreData reloaded = await new QuickCaptureStore(noteFiles.QuickCaptureDirectory).LoadAsync();
        if (reloaded.NotesStorageVersion < QuickCaptureStore.CurrentNotesStorageVersion)
        {
            throw new InvalidOperationException(
                "The migrated aggregate store is missing the independent-notes marker.");
        }

        var loadedIds = new HashSet<string>(
            (await noteFiles.LoadAllAsync())
                .Where(load => load.Item is not null)
                .Select(load => load.Item!.Id),
            StringComparer.Ordinal);
        var expectedIds = notes.Select(note => note.Id).ToHashSet(StringComparer.Ordinal);
        if (!loadedIds.SetEquals(expectedIds))
        {
            throw new InvalidOperationException(
                "The published note files do not match the migrated note ids.");
        }

        var retainedIds = retainedRecords.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var jsonIds = reloaded.Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        if (!jsonIds.SetEquals(retainedIds))
        {
            throw new InvalidOperationException(
                "The migrated aggregate store records do not match the retained source records.");
        }
    }
}
