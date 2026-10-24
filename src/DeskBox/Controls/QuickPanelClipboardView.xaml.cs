using System;
using CommunityToolkit.WinUI.Controls;
using DeskBox.Models;
using DeskBox.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DeskBox.Controls;

public sealed partial class QuickPanelClipboardView : UserControl
{
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
            QuickPanelFilter.All => 0,
            QuickPanelFilter.Text => 1,
            QuickPanelFilter.Images => 2,
            QuickPanelFilter.Links => 3,
            QuickPanelFilter.Favorites => 4,
            _ => 0
        };
    }

    private void FilterSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        ViewModel.ClipboardFilter = FilterSegmented.SelectedIndex switch
        {
            0 => QuickPanelFilter.All,
            1 => QuickPanelFilter.Text,
            2 => QuickPanelFilter.Images,
            3 => QuickPanelFilter.Links,
            4 => QuickPanelFilter.Favorites,
            _ => QuickPanelFilter.All
        };
        ViewModel.ApplyClipboardFilter();
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

    private void FlyoutDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is QuickCaptureItem item && ViewModel is not null)
        {
            ViewModel.DeleteClipboardItemCommand.Execute(item);
        }
    }

    private void ItemRoot_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
    }

    private void ItemRoot_PointerExited(object sender, PointerRoutedEventArgs e)
    {
    }
}
