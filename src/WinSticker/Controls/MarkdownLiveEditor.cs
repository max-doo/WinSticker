using Microsoft.UI.Dispatching;
using WinSticker.Models;
using WinSticker.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace WinSticker.Controls;

/// <summary>
/// Hybrid live Markdown editor: the block holding the caret is shown as
/// editable source while every other block is rendered live. Keystrokes
/// only update the model string — serialization is debounced through
/// <see cref="MarkdownSerializationScheduler"/> and visual updates go
/// through <see cref="MarkdownBlockReconciler"/> so blocks whose source
/// did not change keep their rendered view instances. Task checkboxes
/// toggle the underlying Markdown source directly.
/// </summary>
public sealed partial class MarkdownLiveEditor : UserControl
{
    private const int SerializationIdleMilliseconds = 150;

    private readonly MarkdownLiveDocumentModel _model = new();
    private readonly ScrollViewer _scroller = new();
    private readonly StackPanel _blockList = new();
    private readonly TextBlock _placeholder = new();
    private readonly MarkdownSerializationScheduler _serializationScheduler;
    // Rendered view per committed block (null slot = active/never built);
    // indices stay aligned with _committedBlocks.
    private readonly List<MarkdownDocumentView?> _renderedViews = [];
    private List<string> _committedBlocks = [];
    private bool _isSynchronizing;

    public MarkdownLiveEditor()
        : this(new DispatcherQueueSessionTimer(
            DispatcherQueue.GetForCurrentThread(),
            TimeSpan.FromMilliseconds(SerializationIdleMilliseconds)))
    {
    }

    internal MarkdownLiveEditor(IMarkdownSessionTimer idleTimer)
    {
        _serializationScheduler = new MarkdownSerializationScheduler(
            idleTimer,
            () => _model.Serialize(),
            PublishSerializedText);
        Initialize();
    }

    private void Initialize()
    {
        _scroller.Content = _blockList;
        var root = new Grid();
        root.Children.Add(_scroller);
        _placeholder.IsHitTestVisible = false;
        _placeholder.Visibility = Visibility.Collapsed;
        _placeholder.Opacity = 0.55;
        _placeholder.VerticalAlignment = VerticalAlignment.Top;
        _placeholder.Margin = new Thickness(10, 5, 0, 0);
        root.Children.Add(_placeholder);
        Content = root;
        _model.DocumentChanged += Model_DocumentChanged;
        SynchronizeBlocksToModel();
        Loaded += (_, _) => ApplyPlaceholder();
        // Unload can arrive without a host flush (surface teardown); publish
        // any pending typing so the autosave session still sees it.
        Unloaded += (_, _) => _serializationScheduler.Flush();
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

    /// <summary>
    /// Publish any debounced typing immediately and return the up-to-date
    /// document text. Autosave reads this instead of <see cref="Text"/> so a
    /// save never races the idle debounce.
    /// </summary>
    public string FlushAndGetText()
    {
        _serializationScheduler.Flush();
        return Text;
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
            // The pending change belongs to the outgoing document; the host
            // flushes before switching, so anything left here is superseded.
            editor._serializationScheduler.DiscardPending();
            editor._model.Load(value);
            editor.SynchronizeBlocksToModel();
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
        ((MarkdownLiveEditor)sender).RebuildAllViews();

    private static void OnDocumentPropertyChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args) =>
        ((MarkdownLiveEditor)sender).RebuildAllViews();

    private void PublishSerializedText(string text)
    {
        Text = text;
        EditorTextChanged?.Invoke(this, EventArgs.Empty);
        ApplyPlaceholder();
    }

    private void Model_DocumentChanged(object? sender, EventArgs args)
    {
        if (_isSynchronizing)
        {
            return;
        }

        // Keystroke path ends here: no serialization, no visual update.
        _serializationScheduler.NotifyChanged();
    }

    private void RebuildAllViews()
    {
        _renderedViews.Clear();
        // An empty snapshot forces a full-replace plan so every view is
        // recreated with the new appearance options.
        _committedBlocks = [];
        SynchronizeBlocksToModel();
    }

    /// <summary>
    /// Reconcile the visual block list with the model: reuse rendered views
    /// whose source is unchanged (by instance), rebuild only changed blocks,
    /// and swap the active block in or out as a source TextBox.
    /// </summary>
    private void SynchronizeBlocksToModel()
    {
        MarkdownBlockPlan plan = MarkdownBlockReconciler.Plan(
            _committedBlocks,
            _model.Blocks);
        if (!plan.Unchanged)
        {
            var newViews = new MarkdownDocumentView?[_model.Blocks.Count];
            foreach (MarkdownBlockReuse reuse in plan.Reuses)
            {
                newViews[reuse.NewIndex] = _renderedViews[reuse.OldIndex];
            }

            _renderedViews.Clear();
            _renderedViews.AddRange(newViews);
            _committedBlocks = _model.Blocks.ToList();
        }

        RefreshBlockListChildren();
        ApplyPlaceholder();
    }

    private void RefreshBlockListChildren()
    {
        int blockCount = _model.Blocks.Count;
        while (_blockList.Children.Count > blockCount)
        {
            _blockList.Children.RemoveAt(_blockList.Children.Count - 1);
        }

        for (int index = 0; index < blockCount; index++)
        {
            FrameworkElement desired = BuildChildForBlock(index);
            if (index < _blockList.Children.Count)
            {
                if (!ReferenceEquals(_blockList.Children[index], desired))
                {
                    _blockList.Children[index] = desired;
                }
            }
            else
            {
                _blockList.Children.Add(desired);
            }
        }
    }

    private FrameworkElement BuildChildForBlock(int index) =>
        index == _model.ActiveIndex
            ? CreateActiveEditor(index)
            : _renderedViews[index] ??= CreateRenderedBlock(index);

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
        // Index is resolved at event time from _renderedViews so a view that
        // moves after a reconcile never fires a stale block index.
        if (!IsReadOnly)
        {
            view.Tapped += OnRenderedBlockTapped;
        }

        view.TaskToggleRequested += OnRenderedBlockTaskToggleRequested;
        return view;
    }

    private void OnRenderedBlockTapped(object sender, TappedRoutedEventArgs args)
    {
        if (IsReadOnly || sender is not MarkdownDocumentView view)
        {
            return;
        }

        int index = _renderedViews.IndexOf(view);
        if (index >= 0)
        {
            ActivateBlock(index);
        }
    }

    private void OnRenderedBlockTaskToggleRequested(
        object sender,
        MarkdownTaskToggleRequestedEventArgs args)
    {
        if (IsReadOnly || sender is not MarkdownDocumentView view)
        {
            return;
        }

        int index = _renderedViews.IndexOf(view);
        if (index >= 0 && _model.TryToggleTask(index, args.TaskIndex))
        {
            _serializationScheduler.Flush();
            SynchronizeBlocksToModel();
        }
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
        editor.TextChanged += OnActiveEditorTextChanged;
        editor.KeyDown += OnActiveEditorKeyDown;
        editor.LostFocus += OnActiveEditorLostFocus;
        return editor;
    }

    private void OnActiveEditorTextChanged(object sender, TextChangedEventArgs args)
    {
        if (_isSynchronizing || sender is not TextBox editor)
        {
            return;
        }

        // Splitting on a blank line may grow the block list; the visual
        // list is reconciled when this block deactivates.
        _model.UpdateActiveSource(editor.Text);
    }

    private void OnActiveEditorKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            args.Handled = true;
            Deactivate();
        }
    }

    private void OnActiveEditorLostFocus(object sender, RoutedEventArgs args)
    {
        // Only the editor currently occupying the active slot may commit;
        // a stale TextBox displaced by activation must not deactivate the
        // new active block.
        if (_model.ActiveIndex >= 0 &&
            _model.ActiveIndex < _blockList.Children.Count &&
            ReferenceEquals(_blockList.Children[_model.ActiveIndex], sender))
        {
            Deactivate();
        }
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

        _serializationScheduler.Flush();
        _model.SetActive(index);
        SynchronizeBlocksToModel();
        if (_blockList.Children[index] is TextBox editor)
        {
            editor.Focus(FocusState.Programmatic);
            editor.Select(editor.Text.Length, 0);
        }
    }

    private void Deactivate()
    {
        if (_model.ActiveIndex < 0)
        {
            return;
        }

        _serializationScheduler.Flush();
        _model.Deactivate();
        SynchronizeBlocksToModel();
    }

    private void ApplyPlaceholder()
    {
        _placeholder.Text = PlaceholderText;
        bool showPlaceholder = _model.Blocks.Count == 1 &&
                               _model.Blocks[0].Length == 0 &&
                               _model.ActiveIndex < 0;
        _placeholder.Visibility = showPlaceholder
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
