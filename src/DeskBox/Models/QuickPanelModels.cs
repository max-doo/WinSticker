using CommunityToolkit.Mvvm.ComponentModel;

namespace DeskBox.Models;

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
/// Todo view filter presets.
/// </summary>
public enum QuickPanelTodoFilter
{
    All = 0,
    Today = 1,
    Scheduled = 2,
    Unscheduled = 3,
    Important = 4,
    Completed = 5
}

/// <summary>
/// Todo view modes for Quick Panel.
/// </summary>
public enum QuickPanelTodoViewMode
{
    Today = 0,
    Overdue = 1,
    Unscheduled = 2,
    Timeline = 3,
    Completed = 4
}

/// <summary>
/// A lightweight note item in the Quick Panel Notes tab.
/// </summary>
public sealed partial class QuickPanelNoteItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Body { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
