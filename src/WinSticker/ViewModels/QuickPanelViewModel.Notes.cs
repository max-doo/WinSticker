using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSticker.Models;
using WinSticker.Services;
using Microsoft.UI.Dispatching;

namespace WinSticker.ViewModels;

public sealed partial class QuickPanelViewModel
{
    [ObservableProperty]
    private QuickPanelNoteItem? _selectedNote;

    [ObservableProperty]
    private string _noteTitle = string.Empty;

    [ObservableProperty]
    private string _noteBody = string.Empty;

    [ObservableProperty]
    private string _noteSaveStatus = "已保存";

    public ObservableCollection<QuickPanelNoteItem> Notes { get; } = [];

    private DispatcherQueueTimer? _noteAutosaveTimer;
    private bool _isInternalNoteSwitch;

    private void InitializeNotes()
    {
        if (_dispatcherQueue is not null)
        {
            _noteAutosaveTimer = _dispatcherQueue.CreateTimer();
            _noteAutosaveTimer.Interval = TimeSpan.FromMilliseconds(350);
            _noteAutosaveTimer.IsRepeating = false;
            _noteAutosaveTimer.Tick += (s, e) => _ = SaveCurrentNoteAsync();
        }
    }

    private void DisposeNotes()
    {
        _noteAutosaveTimer?.Stop();
    }

    partial void OnSelectedNoteChanged(QuickPanelNoteItem? value)
    {
        if (_isInternalNoteSwitch) return;

        _isInternalNoteSwitch = true;
        try
        {
            if (value is not null)
            {
                NoteTitle = value.Title;
                NoteBody = value.Body;
                NoteSaveStatus = "已保存";
            }
            else
            {
                NoteTitle = string.Empty;
                NoteBody = string.Empty;
            }
        }
        finally
        {
            _isInternalNoteSwitch = false;
        }
    }

    partial void OnNoteTitleChanged(string value)
    {
        if (_isInternalNoteSwitch || SelectedNote is null) return;

        SelectedNote.Title = value;
        TriggerNoteAutosave();
    }

    partial void OnNoteBodyChanged(string value)
    {
        if (_isInternalNoteSwitch || SelectedNote is null) return;

        SelectedNote.Body = value;
        TriggerNoteAutosave();
    }

    private void TriggerNoteAutosave()
    {
        NoteSaveStatus = "正在保存...";
        _noteAutosaveTimer?.Stop();
        _noteAutosaveTimer?.Start();
    }

    public async Task LoadNotesAsync()
    {
        try
        {
            QuickCaptureStoreData data = await _quickCaptureService.GetDataAsync();

            var noteRecords = data.Items
                .Where(i => i.Type == QuickCaptureItemType.Text && !i.IsRecent && !i.IsDeleted)
                .OrderBy(i => i.SortOrder)
                .ToList();

            List<QuickPanelNoteItem> noteItems = [];
            if (noteRecords.Count == 0)
            {
                var created = await _quickCaptureService.AddDetailedItemAsync(
                    title: "便签 1",
                    body: string.Empty,
                    appearancePreset: QuickCaptureAppearancePreset.Default);

                noteItems.Add(new QuickPanelNoteItem
                {
                    Id = created.Id,
                    Title = string.IsNullOrWhiteSpace(created.Title) ? "便签 1" : created.Title,
                    Body = created.Body ?? string.Empty,
                    CreatedAt = created.CreatedAt,
                    UpdatedAt = created.UpdatedAt
                });
            }
            else
            {
                foreach (var record in noteRecords)
                {
                    noteItems.Add(new QuickPanelNoteItem
                    {
                        Id = record.Id,
                        Title = string.IsNullOrWhiteSpace(record.Title) ? "便签" : record.Title,
                        Body = record.Body ?? string.Empty,
                        CreatedAt = record.CreatedAt,
                        UpdatedAt = record.UpdatedAt
                    });
                }
            }

            RunOnUi(() =>
            {
                _isInternalNoteSwitch = true;
                try
                {
                    Notes.Clear();
                    foreach (var item in noteItems)
                    {
                        Notes.Add(item);
                    }

                    SelectedNote = Notes.Count > 0 ? Notes[0] : null;
                    if (SelectedNote is not null)
                    {
                        NoteTitle = SelectedNote.Title;
                        NoteBody = SelectedNote.Body;
                        NoteSaveStatus = "已保存";
                    }
                }
                finally
                {
                    _isInternalNoteSwitch = false;
                }
            });
        }
        catch (Exception ex)
        {
            App.Log($"[QuickPanel] Failed to load notes: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task CreateNewNoteAsync()
    {
        int maxIndex = 0;
        foreach (var note in Notes)
        {
            var match = Regex.Match(note.Title ?? string.Empty, @"^便签\s*(\d+)$");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
            {
                maxIndex = Math.Max(maxIndex, num);
            }
        }
        int nextIndex = Math.Max(Notes.Count + 1, maxIndex + 1);

        var newNote = new QuickPanelNoteItem
        {
            Title = $"便签 {nextIndex}",
            Body = string.Empty
        };

        var created = await _quickCaptureService.AddDetailedItemAsync(
            title: newNote.Title,
            body: newNote.Body,
            appearancePreset: QuickCaptureAppearancePreset.Default);

        newNote.Id = created.Id;
        Notes.Add(newNote);
        SelectedNote = newNote;
        ShowToast("已创建新便签");
    }

    [RelayCommand]
    public async Task CloseNoteAsync(QuickPanelNoteItem? note)
    {
        if (note is null || Notes.Count <= 1) return;

        int index = Notes.IndexOf(note);
        Notes.Remove(note);

        if (ReferenceEquals(SelectedNote, note))
        {
            int nextIndex = Math.Clamp(index, 0, Notes.Count - 1);
            SelectedNote = Notes[nextIndex];
        }

        await _quickCaptureService.DeleteItemAsync(note.Id);
    }

    [RelayCommand]
    public async Task DeleteCurrentNoteAsync()
    {
        if (SelectedNote is null) return;

        var noteToDelete = SelectedNote;
        await _quickCaptureService.DeleteItemAsync(noteToDelete.Id);

        Notes.Remove(noteToDelete);
        if (Notes.Count == 0)
        {
            await CreateNewNoteAsync();
        }
        else
        {
            SelectedNote = Notes[0];
        }

        ShowToast("便签已删除");
    }

    public async Task SaveCurrentNoteAsync()
    {
        if (SelectedNote is null) return;

        try
        {
            SelectedNote.UpdatedAt = DateTimeOffset.UtcNow;
            if (string.IsNullOrWhiteSpace(SelectedNote.Id))
            {
                var created = await _quickCaptureService.AddDetailedItemAsync(
                    title: SelectedNote.Title,
                    body: SelectedNote.Body,
                    appearancePreset: QuickCaptureAppearancePreset.Default);
                SelectedNote.Id = created.Id;
            }
            else
            {
                await _quickCaptureService.UpdateItemDetailsAsync(
                    SelectedNote.Id,
                    SelectedNote.Title,
                    SelectedNote.Body,
                    QuickCaptureAppearancePreset.Default);
            }

            RunOnUi(() =>
            {
                NoteSaveStatus = "已保存";
            });
        }
        catch (Exception ex)
        {
            App.Log($"[QuickPanel] Autosave note failed: {ex.Message}");
            RunOnUi(() =>
            {
                NoteSaveStatus = "保存失败";
            });
        }
    }

    [RelayCommand]
    public async Task ExtractTodosFromNoteAsync()
    {
        string content = NoteBody;
        if (string.IsNullOrWhiteSpace(content))
        {
            ShowToast("便签内容为空");
            return;
        }

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var extractedTasks = new List<string>();

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            string taskTitle = string.Empty;

            if (line.StartsWith("- [ ]", StringComparison.OrdinalIgnoreCase))
            {
                taskTitle = line[5..].Trim();
            }
            else if (line.StartsWith("- [x]", StringComparison.OrdinalIgnoreCase))
            {
                taskTitle = line[5..].Trim();
            }
            else if (line.StartsWith("* ") || line.StartsWith("- "))
            {
                taskTitle = line[2..].Trim();
            }
            else if (Regex.IsMatch(line, @"^\d+\.\s"))
            {
                int dotIndex = line.IndexOf('.');
                taskTitle = line[(dotIndex + 1)..].Trim();
            }

            if (!string.IsNullOrWhiteSpace(taskTitle))
            {
                if (taskTitle.Length > 200)
                {
                    taskTitle = taskTitle[..200];
                }
                extractedTasks.Add(taskTitle);
            }
        }

        if (extractedTasks.Count == 0)
        {
            ShowToast("未检测到待办格式（如 - [ ]、*、- 或 1.）");
            return;
        }

        string widgetId = GetActiveTodoWidgetId();
        var store = _todoStoreFactory(widgetId);
        var data = await store.LoadAsync();

        foreach (var title in extractedTasks)
        {
            data.Items.Insert(0, new TodoItem
            {
                Text = title,
                DueDate = DateTimeOffset.Now,
                SortOrder = data.Items.Count,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await store.SaveAsync(data);
        await LoadTodosAsync();
        ShowToast($"已成功提取 {extractedTasks.Count} 条待办任务");
    }
}
