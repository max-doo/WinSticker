namespace WinSticker.Models;

public sealed class QuickCaptureStoreData
{
    public int Version { get; set; } = 5;

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
