using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinSticker.Helpers;
using WinSticker.Models;
using WinSticker.Services;
using WinSticker.ViewModels;

namespace WinSticker.Controls.WidgetContents;

public sealed partial class TodoCalendarContent : UserControl
{
    private sealed class DayCellVisuals
    {
        public Border Container { get; }
        public Grid ContentGrid { get; }
        public TextBlock DayText { get; }
        public StackPanel ChipPanel { get; }
        public TextBlock OverflowText { get; }
        public TextBox InlineInput { get; }

        public DayCellVisuals(
            Border container,
            Grid contentGrid,
            TextBlock dayText,
            StackPanel chipPanel,
            TextBlock overflowText,
            TextBox inlineInput)
        {
            Container = container;
            ContentGrid = contentGrid;
            DayText = dayText;
            ChipPanel = chipPanel;
            OverflowText = overflowText;
            InlineInput = inlineInput;
        }
    }

    private readonly DayCellVisuals[] _cells = new DayCellVisuals[42];
    private bool _initialized;

    public TodoCalendarViewModel ViewModel { get; }

    public TodoCalendarContent(TodoCalendarViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();

        Loaded += TodoCalendarContent_Loaded;
        Unloaded += TodoCalendarContent_Unloaded;

        InitializeWeekdayHeaders();
        InitializeMonthGrid();
        UpdateView();
    }

    private void TodoCalendarContent_Loaded(object sender, RoutedEventArgs e)
    {
        // Re-(un)subscribe on every load like TodoWidgetContent: the
        // constructor can miss a Cells rebuild queued between construction
        // and the first load, and a reload after an unload must restore
        // subscriptions the unload dropped.
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.Cells.CollectionChanged -= Cells_CollectionChanged;
        ViewModel.SelectedDayTasks.CollectionChanged -= SelectedDayTasks_CollectionChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Cells.CollectionChanged += Cells_CollectionChanged;
        ViewModel.SelectedDayTasks.CollectionChanged += SelectedDayTasks_CollectionChanged;

        UpdateView();

        if (_initialized) return;
        _initialized = true;
        _ = ViewModel.InitializeAsync();
    }

    private void TodoCalendarContent_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.Cells.CollectionChanged -= Cells_CollectionChanged;
        ViewModel.SelectedDayTasks.CollectionChanged -= SelectedDayTasks_CollectionChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (e.PropertyName == nameof(TodoCalendarViewModel.MonthTitle))
            {
                MonthTitleText.Text = ViewModel.MonthTitle;
            }
            else if (e.PropertyName == nameof(TodoCalendarViewModel.MonthAnchor) ||
                     e.PropertyName == nameof(TodoCalendarViewModel.SelectedDate) ||
                     e.PropertyName == nameof(TodoCalendarViewModel.Cells))
            {
                // Cells: RebuildCells reuses identical cell instances for a
                // same-month rebuild, so CollectionChanged stays silent and
                // the view only hears about it here.
                UpdateView();
            }
        });
    }

    private void Cells_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue?.TryEnqueue(UpdateView);
    }

    private void SelectedDayTasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue?.TryEnqueue(UpdateSelectedDayTasksView);
    }

    private void InitializeWeekdayHeaders()
    {
        WeekdayHeaderGrid.Children.Clear();
        DayOfWeek start = DayOfWeek.Monday;
        try
        {
            start = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        }
        catch { }

        string[] names = ["日", "一", "二", "三", "四", "五", "六"];
        for (int i = 0; i < 7; i++)
        {
            int dayIndex = ((int)start + i) % 7;
            var tb = new TextBlock
            {
                Text = names[dayIndex],
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = (dayIndex is 0 or 6) ? 0.6 : 0.85
            };
            Grid.SetColumn(tb, i);
            WeekdayHeaderGrid.Children.Add(tb);
        }
    }

    private void InitializeMonthGrid()
    {
        MonthGrid.Children.Clear();

        for (int i = 0; i < 42; i++)
        {
            int row = i / 7;
            int col = i % 7;

            var container = new Border
            {
                Margin = new Thickness(1),
                Padding = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                Background = new SolidColorBrush(Colors.Transparent),
                AllowDrop = true
            };

            var contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var dayText = new TextBlock
            {
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 1)
            };
            Grid.SetRow(dayText, 0);
            contentGrid.Children.Add(dayText);

            var chipPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 1
            };
            Grid.SetRow(chipPanel, 1);
            contentGrid.Children.Add(chipPanel);

            var overflowText = new TextBlock
            {
                FontSize = 9,
                Opacity = 0.6,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(overflowText, 2);
            contentGrid.Children.Add(overflowText);

            var inlineInput = new TextBox
            {
                FontSize = 11,
                Padding = new Thickness(2),
                Margin = new Thickness(0, 2, 0, 0),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(inlineInput, 1);
            contentGrid.Children.Add(inlineInput);

            container.Child = contentGrid;
            Grid.SetRow(container, row);
            Grid.SetColumn(container, col);

            int cellIndex = i;
            container.PointerPressed += (s, e) =>
            {
                if (cellIndex < ViewModel.Cells.Count)
                {
                    _ = ViewModel.SelectDateAsync(ViewModel.Cells[cellIndex].Date);
                }
            };

            container.DoubleTapped += (s, e) =>
            {
                if (cellIndex < ViewModel.Cells.Count)
                {
                    ShowInlineInput(cellIndex);
                }
            };

            container.DragOver += Cell_DragOver;
            container.Drop += Cell_Drop;

            inlineInput.KeyDown += async (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    string text = inlineInput.Text.Trim();
                    inlineInput.Visibility = Visibility.Collapsed;
                    chipPanel.Visibility = Visibility.Visible;
                    if (!string.IsNullOrWhiteSpace(text) && cellIndex < ViewModel.Cells.Count)
                    {
                        await ViewModel.CreateTaskOnDateAsync(ViewModel.Cells[cellIndex].Date, text);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    inlineInput.Visibility = Visibility.Collapsed;
                    chipPanel.Visibility = Visibility.Visible;
                    e.Handled = true;
                }
            };

            MonthGrid.Children.Add(container);
            _cells[i] = new DayCellVisuals(container, contentGrid, dayText, chipPanel, overflowText, inlineInput);
        }
    }
    public void OpenInlineCreateForSelectedDate()
    {
        var targetDate = ViewModel.SelectedDate ?? DateOnly.FromDateTime(DateTime.Today);
        for (int i = 0; i < ViewModel.Cells.Count; i++)
        {
            if (ViewModel.Cells[i].Date == targetDate)
            {
                ShowInlineInput(i);
                return;
            }
        }

        if (ViewModel.Cells.Count > 0)
        {
            ShowInlineInput(0);
        }
    }

    private void ShowInlineInput(int index)
    {
        var v = _cells[index];
        v.ChipPanel.Visibility = Visibility.Collapsed;
        v.InlineInput.Text = string.Empty;
        v.InlineInput.Visibility = Visibility.Visible;
        v.InlineInput.Focus(FocusState.Programmatic);
    }

    public void UpdateView()
    {
        MonthTitleText.Text = ViewModel.MonthTitle;

        var selectedDate = ViewModel.SelectedDate;
        var cells = ViewModel.Cells;

        for (int i = 0; i < 42 && i < cells.Count; i++)
        {
            var cellModel = cells[i];
            var visuals = _cells[i];

            visuals.Container.Tag = cellModel;
            visuals.DayText.Text = cellModel.DayNumber.ToString(CultureInfo.InvariantCulture);

            // Opacity & styling based on month and today
            if (cellModel.IsToday)
            {
                visuals.DayText.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
                visuals.DayText.Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
            }
            else
            {
                visuals.DayText.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
                visuals.DayText.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            }

            visuals.Container.Opacity = cellModel.IsCurrentMonth ? 1.0 : 0.35;

            // Selection indicator
            if (cellModel.Date == selectedDate)
            {
                visuals.Container.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
                visuals.Container.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
            }
            else
            {
                visuals.Container.BorderBrush = new SolidColorBrush(Colors.Transparent);
                visuals.Container.Background = new SolidColorBrush(Colors.Transparent);
            }

            // Chips
            visuals.ChipPanel.Children.Clear();
            foreach (var chip in cellModel.Tasks)
            {
                visuals.ChipPanel.Children.Add(CreateChipElement(chip));
            }

            // Overflow
            if (cellModel.OverflowCount > 0)
            {
                visuals.OverflowText.Text = $"+{cellModel.OverflowCount}";
                visuals.OverflowText.Visibility = Visibility.Visible;
            }
            else
            {
                visuals.OverflowText.Visibility = Visibility.Collapsed;
            }
        }

        UpdateSelectedDayTasksView();
    }

    private UIElement CreateChipElement(TodoCalendarTaskChip chip)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(2, 0, 2, 0),
            Margin = new Thickness(0, 0, 0, 1),
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            Tag = chip,
            CanDrag = true
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };

        if (!string.IsNullOrWhiteSpace(chip.ColorHex))
        {
            var dot = new Border
            {
                Width = 4,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0x78, 0xD4)),
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(dot);
        }

        var tb = new TextBlock
        {
            Text = chip.Text,
            FontSize = 10,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = chip.IsCompleted ? 0.45 : 0.95
        };

        if (chip.IsCompleted)
        {
            tb.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
        }

        sp.Children.Add(tb);

        if (!string.IsNullOrEmpty(chip.StepProgress))
        {
            sp.Children.Add(new TextBlock
            {
                Text = chip.StepProgress,
                FontSize = 9,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.9
            });
        }

        border.Child = sp;
        ToolTipService.SetToolTip(border, chip.Text);

        border.DragStarting += Chip_DragStarting;

        // Context menu
        var menu = new MenuFlyout();
        var toggleItem = new MenuFlyoutItem
        {
            Text = chip.IsCompleted ? "标为未完成" : "标为已完成"
        };
        toggleItem.Click += async (s, e) => await ViewModel.ToggleCompletedAsync(chip.Id);
        menu.Items.Add(toggleItem);

        var todayItem = new MenuFlyoutItem { Text = "设为今天" };
        todayItem.Click += async (s, e) =>
        {
            var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, TimeZoneInfo.Local);
            await ViewModel.MoveTaskToDateAsync(chip.Id, DateOnly.FromDateTime(localNow.DateTime));
        };
        menu.Items.Add(todayItem);

        var deleteItem = new MenuFlyoutItem { Text = "删除任务" };
        deleteItem.Click += async (s, e) => await ViewModel.Todo.DeleteItemAsync(chip.Id);
        menu.Items.Add(deleteItem);

        border.ContextFlyout = menu;

        return border;
    }

    private void UpdateSelectedDayTasksView()
    {
        SelectedDayTasksList.Children.Clear();
        foreach (var task in ViewModel.SelectedDayTasks)
        {
            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var cb = new CheckBox
            {
                IsChecked = task.IsCompleted,
                Margin = new Thickness(0, 0, 4, 0),
                MinWidth = 20,
                MinHeight = 20
            };
            cb.Click += async (s, e) =>
            {
                await ViewModel.ToggleCompletedAsync(task.Id);
            };
            Grid.SetColumn(cb, 0);
            itemGrid.Children.Add(cb);

            var tb = new TextBlock
            {
                Text = task.Text,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Opacity = task.IsCompleted ? 0.5 : 1.0
            };
            if (task.IsCompleted)
            {
                tb.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
            }
            Grid.SetColumn(tb, 1);
            itemGrid.Children.Add(tb);

            SelectedDayTasksList.Children.Add(itemGrid);
        }
    }

    private void Chip_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is FrameworkElement fe && fe.Tag is TodoCalendarTaskChip chip)
        {
            args.Data.SetText(chip.Id);
            args.Data.RequestedOperation = DataPackageOperation.Move;
        }
    }

    private void Cell_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.Text))
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            e.DragUIOverride.IsContentVisible = true;
            e.DragUIOverride.Caption = "移动至该日期";
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private async void Cell_Drop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TodoCalendarDayCell cell)
        {
            if (e.DataView.Contains(StandardDataFormats.Text))
            {
                string taskId = await e.DataView.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(taskId))
                {
                    await ViewModel.MoveTaskToDateAsync(taskId, cell.Date);
                }
            }
        }
    }

    private async void PrevMonthButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ShowMonthAsync(ViewModel.MonthAnchor.AddMonths(-1));
    }

    private async void NextMonthButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ShowMonthAsync(ViewModel.MonthAnchor.AddMonths(1));
    }

    private async void TodayButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.GoToTodayAsync();
    }

    private async void MonthGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        int delta = point.Properties.MouseWheelDelta;
        if (delta > 0)
        {
            await ViewModel.ShowMonthAsync(ViewModel.MonthAnchor.AddMonths(-1));
        }
        else if (delta < 0)
        {
            await ViewModel.ShowMonthAsync(ViewModel.MonthAnchor.AddMonths(1));
        }
        e.Handled = true;
    }

    private void TodoCalendarContent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double availableHeight = MonthGrid.ActualHeight;
        if (availableHeight > 60)
        {
            double cellHeight = availableHeight / 6.0;
            int maxChips = (int)Math.Clamp((cellHeight - 20) / 18.0, 0, 5);
            if (ViewModel.MaxTasksPerCell != maxChips)
            {
                ViewModel.MaxTasksPerCell = maxChips;
            }
        }

        if (SelectedDaySection is not null)
        {
            SelectedDaySection.Visibility = (e.NewSize.Width >= 320 && e.NewSize.Height >= 380)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
