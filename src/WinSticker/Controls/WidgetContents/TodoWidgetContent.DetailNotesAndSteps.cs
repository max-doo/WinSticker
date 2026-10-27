using WinSticker.Services;
using WinSticker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace WinSticker.Controls.WidgetContents;

public sealed partial class TodoWidgetContent
{
    private readonly MarkdownDocumentService _markdownDocumentService = new();
    private MarkdownEditingSession? _notesSession;
    private string? _notesEditingItemId;
    private bool _notesExitRequested;

    private void InitializeDetailNotesAndSteps()
    {
        _notesSession = new MarkdownEditingSession(
            new DispatcherQueueSessionTimer(
                DispatcherQueue,
                TimeSpan.FromMilliseconds(600)));
        _notesSession.SaveCore = PersistActiveNotesAsync;
        _notesSession.SaveFailed += (_, _) =>
        {
            if (_notesEditingItemId is not null)
            {
                DetailNotesSaveFailure.Visibility = Visibility.Visible;
            }
        };
        DetailNotesView.AttachmentResolver = ResolveSelectedTodoAttachment;
        DetailNotesView.AttachmentOpenRequested += DetailNotesView_AttachmentOpenRequested;
        DetailNotesReaderHost.AddHandler(
            UIElement.DoubleTappedEvent,
            new DoubleTappedEventHandler(DetailNotesReaderHost_DoubleTapped),
            handledEventsToo: true);
        ApplyNotesEditingVisualState(isEditing: false);
    }

    private Task<TodoItemViewModel?> GetSelectedDetailItemAsync()
    {
        return Task.FromResult(ViewModel?.SelectedDetailItem);
    }

    private async void DetailAddStepButton_Click(object sender, RoutedEventArgs e) =>
        await AddDetailStepAsync();

    private async void DetailNewStepTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            await AddDetailStepAsync();
        }
        else if (e.Key == VirtualKey.Escape)
        {
            DetailNewStepTextBox.Text = string.Empty;
            e.Handled = true;
        }
    }

    private async Task AddDetailStepAsync()
    {
        string text = DetailNewStepTextBox.Text;
        if (string.IsNullOrWhiteSpace(text) || ViewModel is null)
        {
            return;
        }

        TodoItemViewModel? item = await GetSelectedDetailItemAsync();
        if (item is null || await ViewModel.AddStepAsync(item.Id, text) is null)
        {
            return;
        }

        DetailNewStepTextBox.Text = string.Empty;
        DetailNewStepTextBox.Focus(FocusState.Programmatic);
    }

    private async void DetailStepCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            await SetDetailStepCompletedAsync(checkBox);
        }
    }

    private async Task<bool> SetDetailStepCompletedAsync(CheckBox checkBox)
    {
        if (checkBox.DataContext is not TodoStepViewModel step ||
            ViewModel?.SelectedDetailItem is not { } item)
        {
            return false;
        }

        return await ViewModel.SetStepCompletedAsync(
            item.Id,
            step.Id,
            checkBox.IsChecked == true);
    }

    private async void DetailStepTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            await SaveDetailStepTextAsync(textBox);
        }
    }

    private async Task<bool> SaveDetailStepTextAsync(TextBox textBox)
    {
        if (textBox.DataContext is not TodoStepViewModel step ||
            ViewModel?.SelectedDetailItem is not { } item)
        {
            return false;
        }

        bool saved = await ViewModel.UpdateStepTextAsync(
            item.Id,
            step.Id,
            textBox.Text);
        if (!saved)
        {
            textBox.Text = step.Text;
        }

        return saved;
    }

    private void DetailStepTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is not (VirtualKey.Enter or VirtualKey.Escape) || sender is not TextBox textBox)
        {
            return;
        }

        e.Handled = true;
        if (e.Key == VirtualKey.Escape && textBox.DataContext is TodoStepViewModel step)
        {
            textBox.Text = step.Text;
        }

        Focus(FocusState.Programmatic);
    }

    private async void DetailDeleteStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            await DeleteDetailStepAsync(element);
        }
    }

    private async Task<bool> DeleteDetailStepAsync(FrameworkElement element)
    {
        if (element.DataContext is not TodoStepViewModel step ||
            ViewModel?.SelectedDetailItem is not { } item)
        {
            return false;
        }

        return await ViewModel.DeleteStepAsync(item.Id, step.Id);
    }

    private async void DetailNotesReaderHost_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel?.SelectedDetailItem is not null)
        {
            await BeginNotesEditingAsync();
            e.Handled = true;
        }
    }

    private async void DetailNotesEditButton_Click(object sender, RoutedEventArgs e) =>
        await BeginNotesEditingAsync();

    private async void DetailNotesDoneButton_Click(object sender, RoutedEventArgs e) =>
        await SaveActiveNotesAsync(keepEditing: false);

    private async void DetailNotesRetryButton_Click(object sender, RoutedEventArgs e) =>
        await SaveActiveNotesAsync(keepEditing: !_notesExitRequested);

    private async void DetailNotesReaderHost_DoubleTapped(
        object sender,
        DoubleTappedRoutedEventArgs e)
    {
        await BeginNotesEditingAsync();
        e.Handled = true;
    }

    private async Task BeginNotesEditingAsync()
    {
        TodoItemViewModel? item = await GetSelectedDetailItemAsync();
        if (item is null)
        {
            return;
        }

        _notesEditingItemId = item.Id;
        _notesExitRequested = false;
        _notesSession?.Reset();
        DetailNotesEditor.AttachmentResolver = ResolveSelectedTodoAttachment;
        DetailNotesEditor.Text = item.Notes;
        DetailNotesSaveFailure.Visibility = Visibility.Collapsed;
        ApplyNotesEditingVisualState(isEditing: true);
        DetailNotesEditor.FocusEditor();
    }

    private void DetailNotesEditor_EditorTextChanged(object? sender, EventArgs e) =>
        ScheduleNotesAutoSave();

    private void ScheduleNotesAutoSave()
    {
        if (_notesEditingItemId is null || _notesSession is null)
        {
            return;
        }

        _notesExitRequested = false;
        DetailNotesSaveFailure.Visibility = Visibility.Collapsed;
        _notesSession.NotifyChanged();
    }

    /// <summary>Session save callback: persist the live editor's text.</summary>
    private async Task<bool> PersistActiveNotesAsync()
    {
        if (_notesEditingItemId is not { } itemId || ViewModel is not { } viewModel)
        {
            return true;
        }

        if (await GetSelectedDetailItemAsync() is null)
        {
            return false;
        }

        try
        {
            // FlushAndGetText publishes any debounced typing so the save
            // never races the editor's 150ms serialization idle window.
            return await viewModel.UpdateNotesAsync(itemId, DetailNotesEditor.FlushAndGetText());
        }
        catch (Exception ex)
        {
            App.Log($"[Todo] Failed to save notes for {itemId}: {ex}");
            return false;
        }
    }

    private async Task<bool> SaveActiveNotesAsync(bool keepEditing)
    {
        if (!keepEditing)
        {
            _notesExitRequested = true;
        }

        if (_notesEditingItemId is not { } itemId ||
            ViewModel is null ||
            _notesSession is null)
        {
            if (!keepEditing)
            {
                EndNotesEditing();
            }

            return true;
        }

        bool saved = await _notesSession.SaveAsync(completeEditing: !keepEditing);
        if (!string.Equals(_notesEditingItemId, itemId, StringComparison.Ordinal))
        {
            return saved;
        }

        if (!saved)
        {
            DetailNotesSaveFailure.Visibility = Visibility.Visible;
            ApplyNotesEditingVisualState(isEditing: true);
            return false;
        }

        DetailNotesSaveFailure.Visibility = Visibility.Collapsed;
        if (_notesExitRequested && !_notesSession.HasUnsavedChanges)
        {
            EndNotesEditing();
        }

        return true;
    }

    private void EndNotesEditing()
    {
        _notesSession?.Reset();
        _notesEditingItemId = null;
        _notesExitRequested = false;
        DetailNotesSaveFailure.Visibility = Visibility.Collapsed;
        ApplyNotesEditingVisualState(isEditing: false);
    }

    private void ApplyNotesEditingVisualState(bool isEditing)
    {
        DetailNotesReaderHost.Visibility = isEditing ? Visibility.Collapsed : Visibility.Visible;
        DetailNotesEditorHost.Visibility = isEditing ? Visibility.Visible : Visibility.Collapsed;
        DetailNotesEditButton.Visibility = Visibility.Collapsed;
        DetailNotesDoneButton.Visibility = Visibility.Collapsed;
    }

    private async Task<bool> PrepareForDetailSelectionChangeAsync(string? nextItemId)
    {
        if (ViewModel?.IsCreatingDetailItem == true &&
            !string.Equals(ViewModel.SelectedDetailItem?.Id, nextItemId, StringComparison.Ordinal))
            return await DiscardCreatingDetailAsync();

        if (_notesEditingItemId is null ||
            string.Equals(_notesEditingItemId, nextItemId, StringComparison.Ordinal))
        {
            return true;
        }

        return await SaveActiveNotesAsync(keepEditing: false);
    }

    private async Task<TodoItemViewModel?> OpenDetailItemAsync(string itemId)
    {
        if (ViewModel is null || !await PrepareForDetailSelectionChangeAsync(itemId))
        {
            return null;
        }

        return ViewModel.OpenDetail(itemId);
    }

    private void SynchronizeDetailNotes()
    {
        TodoItemViewModel? item = ViewModel?.SelectedDetailItem;
        if (item is not null && string.Equals(_notesEditingItemId, item.Id, StringComparison.Ordinal))
        {
            return;
        }

        if (_notesEditingItemId is not null)
        {
            // Selection/filter changes must not discard a debounce window that
            // has not elapsed yet. The save captures the old item ID and source.
            _ = SaveActiveNotesAsync(keepEditing: false);
        }

        _notesSession?.Reset();
        _notesEditingItemId = null;
        _notesExitRequested = false;
        DetailNotesSaveFailure.Visibility = Visibility.Collapsed;
        ApplyNotesEditingVisualState(isEditing: false);
        DetailNotesView.AttachmentResolver = ResolveSelectedTodoAttachment;
        DetailNotesView.Refresh();
    }

    private async void DetailNotesView_TaskToggleRequested(
        object? sender,
        MarkdownTaskToggleRequestedEventArgs e)
    {
        if (ViewModel?.SelectedDetailItem is not { } item ||
            !_markdownDocumentService.TryToggleTask(item.Notes, e.TaskIndex, out string updated))
        {
            return;
        }

        await ViewModel.UpdateNotesAsync(item.Id, updated);
    }

    private string? ResolveSelectedTodoAttachment(string attachmentId) =>
        ViewModel?.SelectedDetailItem?.Attachments
            .FirstOrDefault(attachment => string.Equals(
                attachment.Id,
                attachmentId,
                StringComparison.Ordinal))
            ?.FilePath;

    private async void DetailNotesView_AttachmentOpenRequested(
        object? sender,
        MarkdownAttachmentRequestedEventArgs e)
    {
        TodoAttachmentViewModel? attachment = ViewModel?.SelectedDetailItem?.Attachments
            .FirstOrDefault(candidate => string.Equals(
                candidate.Id,
                e.AttachmentId,
                StringComparison.Ordinal));
        if (attachment is not null)
        {
            await OpenTodoAttachmentAsync(attachment);
        }
    }
}
