using WinSticker.Models;
using WinSticker.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace WinSticker.Controls;

/// <summary>
/// Hybrid live Markdown editor ported from the WorkCat MarkdownEditor
/// interaction model: the block holding the caret is shown as editable
/// source while every other block is rendered live. Clicking a rendered
/// block activates it; leaving it (or pressing Escape) commits the source
/// and re-renders. Task checkboxes toggle the underlying Markdown source
/// directly. Semantic colors follow the app theme accent exactly like
/// <see cref="MarkdownDocumentView"/>.
/// </summary>
public sealed partial class MarkdownLiveEditor : UserControl
{
    private readonly MarkdownLiveDocumentModel _model = new();
    private readonly ScrollViewer _scroller = new();
    private readonly StackPanel _blockList = new();
    private readonly TextBlock _placeholder = new();
    private bool _isSynchronizing;

    public MarkdownLiveEditor()
    {
        _scroller.Content = _blockList;
        Content = _scroller;
        _placeholder.IsHitTestVisible = false;
        _placeholder.Visibility = Visibility.Collapsed;
        _placeholder.Opacity = 0.55;
        _model.DocumentChanged += Model_DocumentChanged;
        RebuildBlocks();
        Loaded += (_, _) => ApplyPlaceholder();
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(MarkdownLiveEditor),
        new PropertyMetadata(string.Empty, OnTextPropertyChanged));

    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText),
        typeof(string),
        typeof(MarkdownLiveEditor),
        new PropertyMetadata(string.Empty, OnPlaceholderPropertyChanged));

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly),
        typeof(bool),
        typeof(MarkdownLiveEditor),
        new PropertyMetadata(false, OnReadOnlyPropertyChanged));

    public static readonly DependencyProperty AllowRemoteImagesProperty = DependencyProperty.Register(
        nameof(AllowRemoteImages),
        typeof(bool),
        typeof(MarkdownLiveEditor),
        new PropertyMetadata(false, OnDocumentPropertyChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public bool AllowRemoteImages
    {
        get => (bool)GetValue(AllowRemoteImagesProperty);
        set => SetValue(AllowRemoteImagesProperty, value);
    }

    /// <summary>Raised after the user edited, split, or toggled the document.</summary>
    public event EventHandler? EditorTextChanged;

    /// <summary>Activate the first block for editing (open-in-edit entry point).</summary>
    public void FocusEditor()
    {
        if (IsReadOnly || _model.Blocks.Count == 0)
        {
            return;
        }

        ActivateBlock(0);
    }

    private static void OnTextPropertyChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args)
    {
        var editor = (MarkdownLiveEditor)sender;
        string value = args.NewValue as string ?? string.Empty;
        if (string.Equals(value, editor._model.Serialize(), StringComparison.Ordinal))
        {
            return;
        }

        editor._isSynchronizing = true;
        try
        {
            editor._model.Load(value);
            editor.RebuildBlocks();
        }
        finally
        {
            editor._isSynchronizing = false;
        }

        editor.ApplyPlaceholder();
    }

    private static void OnPlaceholderPropertyChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args) =>
        ((MarkdownLiveEditor)sender).ApplyPlaceholder();

    private static void OnReadOnlyPropertyChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args) =>
        ((MarkdownLiveEditor)sender).RebuildBlocks();

    private static void OnDocumentPropertyChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args) =>
        ((MarkdownLiveEditor)sender).RebuildBlocks();

    private void Model_DocumentChanged(object? sender, EventArgs args)
    {
        if (_isSynchronizing)
        {
            return;
        }

        // Text and model are already in sync here; the property change only
        // notifies bindings, guarded against reloading by the equality check.
        Text = _model.Serialize();
        EditorTextChanged?.Invoke(this, EventArgs.Empty);
        ApplyPlaceholder();
    }

    private void RebuildBlocks()
    {
        _blockList.Children.Clear();
        for (int index = 0; index < _model.Blocks.Count; index++)
        {
            _blockList.Children.Add(
                index == _model.ActiveIndex
                    ? CreateActiveEditor(index)
                    : CreateRenderedBlock(index));
        }
    }

    private MarkdownDocumentView CreateRenderedBlock(int index)
    {
        var view = new MarkdownDocumentView
        {
            Markdown = _model.Blocks[index],
            ContentFormat = TextContentFormat.Markdown,
            FontSize = double.IsFinite(FontSize) && FontSize > 0 ? FontSize : 14,
            UseInternalScrollViewer = false,
            AllowRemoteImages = AllowRemoteImages,
            AreTaskListsInteractive = !IsReadOnly
        };
        if (!IsReadOnly)
        {
            view.Tapped += (_, _) => ActivateBlock(index);
        }

        view.TaskToggleRequested += (_, args) =>
        {
            if (!IsReadOnly && _model.TryToggleTask(index, args.TaskIndex))
            {
                RefreshRenderedBlock(index);
            }
        };
        return view;
    }

    private TextBox CreateActiveEditor(int index)
    {
        var editor = new TextBox
        {
            Text = _model.Blocks[index],
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = double.IsFinite(FontSize) && FontSize > 0 ? FontSize : 14,
            PlaceholderText = PlaceholderText,
            Margin = new Thickness(0, 2, 0, 2),
            IsReadOnly = IsReadOnly
        };
        editor.TextChanged += (_, _) =>
        {
            if (_isSynchronizing)
            {
                return;
            }

            // Splitting on a blank line may grow the block list; the visual
            // list is rebuilt when this block deactivates.
            _model.UpdateActiveSource(editor.Text);
        };
        editor.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Escape)
            {
                args.Handled = true;
                Deactivate();
            }
        };
        editor.LostFocus += (_, _) =>
        {
            if (_model.ActiveIndex == index)
            {
                Deactivate();
            }
        };
        return editor;
    }

    private void ActivateBlock(int index)
    {
        if (IsReadOnly || index < 0 || index >= _model.Blocks.Count)
        {
            return;
        }

        if (_model.ActiveIndex == index)
        {
            return;
        }

        _model.SetActive(index);
        RebuildBlocks();
        if (_blockList.Children[index] is TextBox editor)
        {
            editor.Focus(FocusState.Programmatic);
            editor.Select(editor.Text.Length, 0);
        }

        ApplyPlaceholder();
    }

    private void Deactivate()
    {
        if (_model.ActiveIndex < 0)
        {
            return;
        }

        _model.Deactivate();
        RebuildBlocks();
        ApplyPlaceholder();
    }

    private void RefreshRenderedBlock(int index)
    {
        if (index >= 0 && index < _blockList.Children.Count &&
            _blockList.Children[index] is MarkdownDocumentView view)
        {
            view.Markdown = _model.Blocks[index];
        }
    }

    private void ApplyPlaceholder()
    {
        _placeholder.Text = PlaceholderText;
        bool showPlaceholder = _model.Blocks.Count == 1 &&
                               _model.Blocks[0].Length == 0 &&
                               _model.ActiveIndex < 0;
        if (showPlaceholder && ReferenceEquals(_placeholder.Parent, null))
        {
            _blockList.Children.Insert(0, _placeholder);
        }
        else if (!showPlaceholder && !ReferenceEquals(_placeholder.Parent, null))
        {
            _blockList.Children.Remove(_placeholder);
        }
    }
}
