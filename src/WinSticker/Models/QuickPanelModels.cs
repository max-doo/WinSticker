using CommunityToolkit.Mvvm.ComponentModel;

namespace WinSticker.Models;

/// <summary>
/// Active tab in the Quick Panel.
/// </summary>
public enum QuickPanelTab
{
    Clipboard = 0,
    Todo = 1,
    Notes = 2
}

/// <summary>
/// Clipboard view filter presets.
/// </summary>
public enum QuickPanelFilter
{
    All = 0,
    Text = 1,
    Images = 2,
    Links = 3,
    Favorites = 4
}

/// <summary>
/// A lightweight note item in the Quick Panel Notes tab.
/// </summary>
public sealed partial class QuickPanelNoteItem : ObservableObject
{
    [ObservableProperty]
    public partial QuickCaptureAppearancePreset Appearance { get; set; }
    [ObservableProperty]
    public partial bool IsPinned { get; set; }
    [ObservableProperty]
    public partial string UntitledText { get; set; } = "Untitled note";
    public string DisplayTitle => Services.NoteWorkspace.DisplayTitle(Title, Body, UntitledText);
    public string IndicatorColor => Appearance switch
    {
        QuickCaptureAppearancePreset.Paper => "#DCCCAA", QuickCaptureAppearancePreset.StickyYellow => "#F2BE4A",
        QuickCaptureAppearancePreset.Rose => "#D77790", QuickCaptureAppearancePreset.Mint => "#5BAA8B",
        QuickCaptureAppearancePreset.MistBlue => "#5F98D3", _ => "#808080"
    };
    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(DisplayTitle));
    partial void OnBodyChanged(string value) => OnPropertyChanged(nameof(DisplayTitle));
    partial void OnUntitledTextChanged(string value) => OnPropertyChanged(nameof(DisplayTitle));
    partial void OnAppearanceChanged(QuickCaptureAppearancePreset value) => OnPropertyChanged(nameof(IndicatorColor));
    internal QuickCaptureItem ToRecord() => new()
    {
        Id = Id, IsNote = true, Title = Title, Body = Body, ContentFormat = ContentFormat,
        AppearancePreset = Appearance, IsPinned = IsPinned, Attachments = Attachments.Select(file => file.Clone()).ToList(),
        CreatedAt = CreatedAt, UpdatedAt = UpdatedAt
    };
    public TextContentFormat ContentFormat { get; set; } = TextContentFormat.Markdown;
    public IReadOnlyList<TodoAttachment> Attachments { get; set; } = [];
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Body { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Non-null while the note is soft-closed (hidden from the tab strip).</summary>
    public DateTimeOffset? ClosedAt { get; set; }
}
