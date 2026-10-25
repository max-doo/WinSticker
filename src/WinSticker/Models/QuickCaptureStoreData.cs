namespace WinSticker.Models;

public sealed class QuickCaptureStoreData
{
    public int Version { get; set; } = 6;

    /// <summary>
    /// Storage layout of editable notes: 0 (or absent) = notes live inside
    /// <see cref="Items"/> (legacy aggregate); 1 = each note is an
    /// independent file under quick-capture/notes and <see cref="Items"/>
    /// holds only non-note records. This marker is the only authoritative
    /// migration switch — the notes directory existing alone proves nothing.
    /// </summary>
    public int NotesStorageVersion { get; set; }

    /// <summary>Quick-panel tab history; it never archives or deletes note content.</summary>
    public Dictionary<string, DateTimeOffset>? ClosedQuickPanelNotes { get; set; }

    public QuickCaptureViewMode CurrentView { get; set; } = QuickCaptureViewMode.Records;

    public List<QuickCaptureItem> Items { get; set; } = [];

    public List<QuickCaptureItem> RecentItems { get; set; } = [];
}

public enum QuickCaptureViewMode
{
    Records,
    Pinned,
    Recent
}
