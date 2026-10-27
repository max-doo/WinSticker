using System;
using WinSticker.Models;
using WinSticker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace WinSticker.Controls;

public sealed partial class QuickPanelClipboardView : UserControl
{
    public event EventHandler? KeyboardInputRequested;
    public QuickPanelViewModel? ViewModel => DataContext as QuickPanelViewModel;

    public QuickPanelClipboardView()
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
        FilterSegmented.SelectedIndex = ViewModel.ClipboardFilter switch
        {
            QuickPanelFilter.Text => 1,
            QuickPanelFilter.Images => 2,
            QuickPanelFilter.Favorites => 3,
            _ => 0
        };
    }

    private void FilterSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.ClipboardFilter = FilterSegmented.SelectedIndex switch
        {
            1 => QuickPanelFilter.Text,
            2 => QuickPanelFilter.Images,
            3 => QuickPanelFilter.Favorites,
            _ => QuickPanelFilter.All
        };
        ViewModel.ApplyClipboardFilter();
    }

    private void SearchToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (ClipboardSearchBox.Visibility == Visibility.Collapsed)
        {
            OpenSearch();
        }
        else
        {
            CollapseSearchBox();
        }
    }

    public void OpenSearch()
    {
        KeyboardInputRequested?.Invoke(this, EventArgs.Empty);
        ClipboardSearchBox.Visibility = Visibility.Visible;
        ClipboardSearchBox.UpdateLayout();
        ClipboardSearchBox.Focus(FocusState.Programmatic);
    }

    public void HandleNavigationKey(Windows.System.VirtualKey key)
    {
        int count = HistoryListView.Items.Count;
        if (count == 0) return;
        int index = HistoryListView.SelectedIndex;
        if (key == Windows.System.VirtualKey.Enter)
        {
            if (HistoryListView.Items[Math.Max(0, index)] is QuickCaptureItem item)
                ViewModel?.PasteClipboardItemCommand.Execute(item);
            return;
        }
        index = key switch
        {
            Windows.System.VirtualKey.Home => 0,
            Windows.System.VirtualKey.End => count - 1,
            Windows.System.VirtualKey.Up => index < 0 ? count - 1 : Math.Max(0, index - 1),
            Windows.System.VirtualKey.Down => Math.Min(count - 1, index + 1),
            _ => index
        };
        if (index >= 0)
        {
            HistoryListView.SelectedIndex = index;
            HistoryListView.ScrollIntoView(HistoryListView.Items[index]);
        }
    }

    private void ClipboardSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CollapseSearchBox();
            e.Handled = true;
        }
    }

    private void CollapseSearchBox()
    {
        ClipboardSearchBox.Visibility = Visibility.Collapsed;
        if (ViewModel is { } viewModel && !string.IsNullOrEmpty(viewModel.SearchText))
        {
            viewModel.SearchText = string.Empty;
        }
    }

    private void HistoryListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is QuickCaptureItem item && ViewModel is not null)
        {
            if (ViewModel.AutoPaste)
            {
                ViewModel.PasteClipboardItemCommand.Execute(item);
            }
        }
    }

    private void HistoryListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement fe && fe.DataContext is QuickCaptureItem item && ViewModel is not null)
        {
            ViewModel.PasteClipboardItemCommand.Execute(item);
        }
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is QuickCaptureItem item && ViewModel is not null)
        {
            ViewModel.ToggleFavoriteCommand.Execute(item);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is QuickCaptureItem item && ViewModel is not null)
        {
            ViewModel.CopyClipboardItemCommand.Execute(item);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is QuickCaptureItem item && ViewModel is not null)
        {
            ViewModel.DeleteClipboardItemCommand.Execute(item);
        }
    }

    private void FlyoutCopy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is QuickCaptureItem item && ViewModel is not null)
        {
            ViewModel.CopyClipboardItemCommand.Execute(item);
        }
    }

    private void FlyoutPaste_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is QuickCaptureItem item && ViewModel is not null)
        {
            ViewModel.PasteClipboardItemCommand.Execute(item);
        }
    }

    private void FlyoutFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is QuickCaptureItem item && ViewModel is not null)
        {
            ViewModel.ToggleFavoriteCommand.Execute(item);
        }
    }

    private void FlyoutSaveAsNote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is QuickCaptureItem item && ViewModel is not null)
        {
            ViewModel.SaveClipboardAsNoteCommand.Execute(item);
        }
    }

    private void FlyoutDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is QuickCaptureItem item && ViewModel is not null)
        {
            ViewModel.DeleteClipboardItemCommand.Execute(item);
        }
    }

    private void ClearAllCancel_Click(object sender, RoutedEventArgs e)
    {
        ClearAllFlyout.Hide();
    }

    private void ClearAllConfirm_Click(object sender, RoutedEventArgs e)
    {
        ClearAllFlyout.Hide();
        ViewModel?.ClearAllClipboardItemsCommand.Execute(null);
    }

    // Hover reveal mirrors the sticky-note/todo item pattern: the card keeps
    // its clean content look until the pointer enters, then the hover wash
    // and the action button overlay fade in and become hit-testable.
    private void ItemRoot_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        SetClipboardItemHoverState(sender as DependencyObject, true);
    }

    private void ItemRoot_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        SetClipboardItemHoverState(sender as DependencyObject, false);
    }

    private static void SetClipboardItemHoverState(DependencyObject? itemRoot, bool isHovered)
    {
        if (itemRoot is null)
        {
            return;
        }

        if (FindVisualChild<Border>(itemRoot, "ClipboardItemHoverBackground") is { } hoverBackground)
        {
            hoverBackground.Opacity = isHovered ? 1 : 0;
        }

        if (FindVisualChild<Border>(itemRoot, "ClipboardItemActionHost") is { } actions)
        {
            actions.Opacity = isHovered ? 1 : 0;
            actions.IsHitTestVisible = isHovered;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent, string? name = null)
        where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typed &&
                (name is null || string.Equals(typed.Name, name, StringComparison.Ordinal)))
            {
                return typed;
            }

            if (FindVisualChild<T>(child, name) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
