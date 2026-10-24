using System;
using DeskBox.Models;
using DeskBox.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Controls;

public sealed partial class QuickPanelNotesView : UserControl
{
    public QuickPanelViewModel? ViewModel => DataContext as QuickPanelViewModel;

    public QuickPanelNotesView()
    {
        InitializeComponent();
    }

    private void AddNoteButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.CreateNewNoteCommand.Execute(null);
    }

    private void DeleteNoteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is QuickPanelNoteItem note && ViewModel is not null)
        {
            ViewModel.CloseNoteCommand.Execute(note);
        }
    }

    private void ExtractTasksButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ExtractTodosFromNoteCommand.Execute(null);
    }
}
