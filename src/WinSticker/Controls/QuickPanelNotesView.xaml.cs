using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using WinSticker.Models;
using WinSticker.Services;
using WinSticker.ViewModels;

namespace WinSticker.Controls;

public sealed partial class QuickPanelNotesView : UserControl
{
    private QuickPanelViewModel? _attachedViewModel;
    private bool _selecting;
    public QuickPanelViewModel? ViewModel => DataContext as QuickPanelViewModel;
    public QuickPanelNotesView()
    {
        InitializeComponent();
        // Loading RichEdit inside WinUI's attach/arrange transaction can re-enter layout.
        DataContextChanged += (_, _) => QueueAttach();
        Loaded += (_, _) => QueueAttach();
        Unloaded += (_, _) => { AttachViewModel(null); NotePane.Dispose(); };
    }
    private void QueueAttach() => DispatcherQueue.TryEnqueue(() =>
    {
        if (IsLoaded) AttachViewModel(DataContext as QuickPanelViewModel);
    });
    private void AttachViewModel(QuickPanelViewModel? viewModel)
    {
        if (ReferenceEquals(_attachedViewModel, viewModel)) return;
        if (_attachedViewModel is { } old)
        {
            FlushEditor();
            old.PropertyChanged -= ViewModel_PropertyChanged;
            if (old.FlushNoteEditor == FlushEditor) old.FlushNoteEditor = null;
        }
        _attachedViewModel = viewModel;
        if (viewModel is not null)
        {
            NotePane.Configure(viewModel.NoteService, viewModel.NoteSettings, viewModel.NoteLocalization, viewModel.NoteTodoStoreFactory);
            viewModel.FlushNoteEditor = FlushEditor;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            string add = viewModel.NoteLocalization.T("Notes.New");
            string library = viewModel.NoteLocalization.T("Notes.Library");
            AutomationProperties.SetName(AddNoteButton, add); ToolTipService.SetToolTip(AddNoteButton, add);
            AutomationProperties.SetName(LibraryButton, library); ToolTipService.SetToolTip(LibraryButton, library);
        }
        if (viewModel is not null) LoadSelectedEditor();
    }
    private void LoadSelectedEditor()
    {
        bool selecting = _selecting;
        _selecting = true;
        try
        {
            NoteTabs.SelectedItem = _attachedViewModel?.SelectedNote;
            if (NoteTabs.SelectedItem is { } selected) NoteTabs.ScrollIntoView(selected);
        }
        finally { _selecting = selecting; }
        NotePane.Open(_attachedViewModel?.SelectedNote?.ToRecord());
    }
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(QuickPanelViewModel.SelectedNote)) LoadSelectedEditor();
    }
    /// <summary>Synchronize the shared native draft before the quick-panel host hides.</summary>
    public void FlushEditor() => NotePane.FlushNative();
    private async void AddNoteButton_Click(object sender, RoutedEventArgs args)
    {
        if (_attachedViewModel is null) return;
        await _attachedViewModel.CreateNewNoteAsync();
        NotePane.FocusBody();
    }
    private async void CloseNoteButton_Click(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { DataContext: QuickPanelNoteItem note } && _attachedViewModel is not null)
            await _attachedViewModel.CloseNoteAsync(note);
    }
    private async void NoteTabs_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_selecting || _attachedViewModel is null || args.AddedItems.FirstOrDefault() is not QuickPanelNoteItem note || ReferenceEquals(note, _attachedViewModel.SelectedNote)) return;
        _selecting = true;
        try
        {
            if (!await _attachedViewModel.SelectNoteAsync(note)) NoteTabs.SelectedItem = _attachedViewModel.SelectedNote;
        }
        finally { _selecting = false; }
    }
    private async void Library_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is QuickPanelNoteItem note && _attachedViewModel is not null && await _attachedViewModel.SelectNoteAsync(note)) ClosedNotesFlyout.Hide();
    }
    private async void LibraryDeleteButton_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement { DataContext: QuickPanelNoteItem note } || _attachedViewModel is null) return;
        ClosedNotesFlyout.Hide();
        LocalizationService localization = _attachedViewModel.NoteLocalization;
        bool confirmed = await ConfirmDialog.ShowAsync(
            XamlRoot,
            localization,
            localization.T("QuickCapture.DeleteConfirm.Title"),
            localization.Format("QuickCapture.DeleteConfirm.Body", note.DisplayTitle),
            localization.T("Common.Delete"));
        if (confirmed) await _attachedViewModel.DeleteNoteAsync(note);
    }
}
