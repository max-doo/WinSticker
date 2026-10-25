using WinSticker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinSticker.Controls.WidgetContents;

public sealed partial class TodoWidgetContent
{
    // Master-card step surface. The chip toggles expansion; the expanded rows
    // toggle completion only. All writes funnel through the same
    // TodoWidgetViewModel operations the detail page uses, so the two surfaces
    // can never disagree.

    private void TodoStepProgressButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TodoItemViewModel item })
        {
            item.IsStepsExpanded = !item.IsStepsExpanded;
        }
    }

    private async void CardStepCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox ||
            checkBox.DataContext is not TodoStepViewModel step)
        {
            return;
        }

        // The row template's CheckBox binds to the step's owner item only
        // through the visual tree, so resolve the parent card's item here.
        TodoItemViewModel? item = FindCardStepOwnerItem(checkBox);
        if (item is null || ViewModel is null)
        {
            checkBox.IsChecked = step.IsCompleted;
            return;
        }

        bool updated = await ViewModel.SetStepCompletedAsync(
            item.Id,
            step.Id,
            checkBox.IsChecked == true);
        if (!updated)
        {
            checkBox.IsChecked = step.IsCompleted;
        }
    }

    private async void TodoStepNudgeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TodoItemViewModel item })
        {
            return;
        }

        if (await SetCompletedWithFeedbackAsync(item, isCompleted: true))
        {
            SynchronizeTodoCompletionCheckBox(item);
            // Completing the parent hides the nudge via StepNudgeVisibility;
            // collapsing the expansion keeps the finished card compact.
            item.IsStepsExpanded = false;
        }
    }

    private TodoItemViewModel? FindCardStepOwnerItem(FrameworkElement stepElement)
    {
        DependencyObject current = stepElement;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: TodoItemViewModel item })
            {
                return item;
            }

            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
