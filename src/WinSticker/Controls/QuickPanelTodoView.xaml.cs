using System;
using CommunityToolkit.WinUI.Controls;
using WinSticker.Models;
using WinSticker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace WinSticker.Controls;

public sealed partial class QuickPanelTodoView : UserControl
{
    public QuickPanelViewModel? ViewModel => DataContext as QuickPanelViewModel;

    public QuickPanelTodoView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ViewModel is not null)
        {
            UpdateSegmentedSelection();
        }
    }

    private void UpdateSegmentedSelection()
    {
        if (ViewModel is null) return;
        TodoModeSegmented.SelectedIndex = ViewModel.TodoViewMode switch
        {
            QuickPanelTodoViewMode.Today => 0,
            QuickPanelTodoViewMode.Overdue => 1,
            QuickPanelTodoViewMode.Unscheduled => 2,
            QuickPanelTodoViewMode.Timeline => 3,
            QuickPanelTodoViewMode.Completed => 4,
            _ => 0
        };
    }

    private void TodoModeSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.TodoViewMode = TodoModeSegmented.SelectedIndex switch
        {
            0 => QuickPanelTodoViewMode.Today,
            1 => QuickPanelTodoViewMode.Overdue,
            2 => QuickPanelTodoViewMode.Unscheduled,
            3 => QuickPanelTodoViewMode.Timeline,
            4 => QuickPanelTodoViewMode.Completed,
            _ => QuickPanelTodoViewMode.Today
        };
        ViewModel.ApplyTodoFilter();
    }

    private void ComposerTitleBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            ViewModel?.SaveComposerTodoCommand.Execute(null);
        }
    }

    private void ComposerImportant_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.ComposerIsImportant = !ViewModel.ComposerIsImportant;
        }
    }

    private void ComposerAdd_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.SaveComposerTodoCommand.Execute(null);
    }

    private void DateTodayChip_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.ComposerDueDate = DateTimeOffset.Now.Date.AddDays(1).AddSeconds(-1);
        }
    }

    private void DateTomorrowChip_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.ComposerDueDate = DateTimeOffset.Now.Date.AddDays(2).AddSeconds(-1);
        }
    }

    private void DatePickChip_Click(object sender, RoutedEventArgs e)
    {
    }

    private void ComposerDatePicker_DatePicked(DatePickerFlyout sender, DatePickedEventArgs args)
    {
        if (ViewModel is not null)
        {
            ViewModel.ComposerDueDate = args.NewDate.Date.AddDays(1).AddSeconds(-1);
        }
    }

    private void TodoCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is TodoItem item && ViewModel is not null)
        {
            ViewModel.ToggleCompleteTodoCommand.Execute(item);
        }
    }

    private void DeleteTodoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TodoItem item && ViewModel is not null)
        {
            ViewModel.DeleteTodoCommand.Execute(item);
        }
    }
}
