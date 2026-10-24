using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using WinSticker.Models;
using WinSticker.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;

namespace WinSticker.Controls;

/// <summary>
/// Hybrid live Markdown editor: the block holding the caret is shown as
/// editable source while every other block is rendered live. Keystrokes
/// only update the model string — serialization is debounced through
/// <see cref="MarkdownSerializationScheduler"/> and block-list updates go
/// through <see cref="MarkdownBlockReconciler"/> so only blocks whose
/// source changed are re-rendered. Blocks are presented in an
/// <see cref="ItemsRepeater"/> so only the viewport is realized; recycled
/// views re-receive the same Markdown string and hit the
/// <see cref="MarkdownDocumentView"/> render-state gate for free. Task
/// checkboxes toggle the underlying Markdown source directly.
/// </summary>
public sealed partial class MarkdownLiveEditor : UserControl
{
    private const int SerializationIdleMilliseconds = 150;

    private const string RenderedTemplateXaml =
        "<DataTemplate xmlns=\"http://schema.microsoft.com/winfx/2006/xaml/presentation\"" +
        " xmlns:controls=\"using:WinSticker.Controls\">" +
        "<controls:MarkdownDocumentView/></DataTemplate>";
    private const string ActiveTemplateXaml =
        "<DataTemplate xmlns=\"http://schema.microsoft.com/winfx/2006/xaml/presentation\">" +
        "<TextBox/></DataTemplate>";

    private readonly MarkdownLiveDocumentModel _model = new();
    private readonly ScrollViewer _scroller = new();
    private readonly TextBlock _placeholder = new();
    private readonly MarkdownSerializationScheduler _serializationScheduler;
    private readonly ObservableCollection<MarkdownBlockItem> _itemSource = [];
    // Realized element -> block index, maintained in ElementPrepared/Clearing
    // so recycled views resolve their block at event time.
    private readonly Dictionary<UIElement, int> _elementBlockIndices = new();
    private ItemsRepeater _blockRepeater = null!;
    private List<string> _committedBlocks = [];
    private int _lastCommittedActiveIndex = -2;
    private TextBox? _activeEditor;
    private int? _pendingActiveCaretOffset;
    private bool _isSynchronizing;
    private bool _isSynchronizingItems;

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
        _blockRepeater = new ItemsRepeater
        {
            Layout = new StackLayout { Orientation = Orientation.Vertical },
            ItemTemplate = new MarkdownBlockTemplateSelector
            {
                RenderedTemplate = (DataTemplate)XamlReader.Load(RenderedTemplateXaml),
                ActiveTemplate = (DataTemplate)XamlReader.Load(ActiveTemplateXaml)
            },
            ItemsSource = _itemSource
        };
        _blockRepeater.ElementPrepared += (_, args) => PrepareBlockElement(args.Index, args.Element);
        _blockRepeater.ElementClearing += (_, args) => ClearBlockElement(args.Element);
        _scroller.Content = _blockRepeater;
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
        SynchronizeItemsToModel();
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
            editor.SynchronizeItemsToModel();
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
        _itemSource.Clear();
        // An empty snapshot forces a full-replace plan so every view is
        // recreated with the new appearance options.
        _committedBlocks = [];
        _lastCommittedActiveIndex = -2;
        SynchronizeItemsToModel();
    }

    /// <summary>
    /// Reconcile the item source with the model: blocks whose source (and
    /// active state) are unchanged keep their item object, so the repeater
    /// never touches their realized elements; changed positions get fresh
    /// items and are the only elements recycled and re-rendered.
    /// </summary>
    private void SynchronizeItemsToModel()
    {
        MarkdownBlockPlan plan = MarkdownBlockReconciler.Plan(
            _committedBlocks,
            _model.Blocks);
        int activeIndex = _model.ActiveIndex;
        if (plan.Unchanged &&
            activeIndex == _lastCommittedActiveIndex &&
            _itemSource.Count == _model.Blocks.Count)
        {
            return;
        }

        _isSynchronizingItems = true;
        try
        {
            int count = _model.Blocks.Count;
            var desired = new MarkdownBlockItem?[count];
            if (!plan.Unchanged)
            {
                foreach (MarkdownBlockReuse reuse in plan.Reuses)
                {
                    int newIndex = reuse.NewIndex;
                    if (newIndex < _itemSource.Count &&
                        _itemSource[newIndex].IsActive == (newIndex == activeIndex) &&
                        string.Equals(
                            _itemSource[newIndex].Source,
                            _model.Blocks[newIndex],
                            StringComparison.Ordinal))
                    {
                        desired[newIndex] = _itemSource[newIndex];
                    }
                }
            }

            for (int index = 0; index < count; index++)
            {
                MarkdownBlockItem item = desired[index] ?? new MarkdownBlockItem
                {
                    Source = _model.Blocks[index],
                    IsActive = index == activeIndex
                };
                if (index < _itemSource.Count)
                {
                    if (!ReferenceEquals(_itemSource[index], item))
                    {
                        _itemSource[index] = item;
                    }
                }
                else
                {
                    _itemSource.Add(item);
                }
            }

            while (_itemSource.Count > count)
            {
                _itemSource.RemoveAt(_itemSource.Count - 1);
            }

            _committedBlocks = _model.Blocks.ToList();
            _lastCommittedActiveIndex = activeIndex;
        }
        finally
        {
            _isSynchronizingItems = false;
        }

        ApplyPlaceholder();
    }

    private void PrepareBlockElement(int index, UIElement element)
    {
        if (index < 0 || index >= _itemSource.Count)
        {
            return;
        }

        bool wasSynchronizing = _isSynchronizingItems;
        _isSynchronizingItems = true;
        try
        {
            _elementBlockIndices[element] = index;
            double fontSize = double.IsFinite(FontSize) && FontSize > 0 ? FontSize : 14;
            if (element is MarkdownDocumentView view)
            {
                view.ContentFormat = TextContentFormat.Markdown;
                view.FontSize = fontSize;
                view.UseInternalScrollViewer = false;
                view.AllowRemoteImages = AllowRemoteImages;
                view.AreTaskListsInteractive = !IsReadOnly;
                // Same-string assignments hit the view's render-state gate,
                // so recycling without a content change costs no re-parse.
                view.Markdown = _itemSource[index].Source;
                view.Tapped += OnRenderedBlockTapped;
                view.TaskToggleRequested += OnRenderedBlockTaskToggleRequested;
            }
            else if (element is TextBox editor)
            {
                editor.Text = _itemSource[index].Source;
                editor.AcceptsReturn = true;
                editor.TextWrapping = TextWrapping.Wrap;
                editor.FontSize = fontSize;
                editor.PlaceholderText = PlaceholderText;
                editor.Margin = new Thickness(0, 2, 0, 2);
                editor.IsReadOnly = IsReadOnly;
                editor.TextChanged += OnActiveEditorTextChanged;
                editor.KeyDown += OnActiveEditorKeyDown;
                editor.LostFocus += OnActiveEditorLostFocus;
                _activeEditor = editor;
                editor.Focus(FocusState.Programmatic);
                int offset = _pendingActiveCaretOffset ?? editor.Text.Length;
                _pendingActiveCaretOffset = null;
                editor.Select(Math.Clamp(offset, 0, editor.Text.Length), 0);
            }
        }
        finally
        {
            _isSynchronizingItems = wasSynchronizing;
        }
    }

    private void ClearBlockElement(UIElement element)
    {
        _elementBlockIndices.Remove(element);
        if (element is MarkdownDocumentView view)
        {
            view.Tapped -= OnRenderedBlockTapped;
            view.TaskToggleRequested -= OnRenderedBlockTaskToggleRequested;
        }
        else if (element is TextBox editor)
        {
            editor.TextChanged -= OnActiveEditorTextChanged;
            editor.KeyDown -= OnActiveEditorKeyDown;
            editor.LostFocus -= OnActiveEditorLostFocus;
            if (ReferenceEquals(_activeEditor, editor))
            {
                // The active editor is being recycled (scroll-out or
                // deactivation); remember the caret for when it comes back.
                _pendingActiveCaretOffset = editor.SelectionStart;
                _activeEditor = null;
            }
        }
    }

    private void OnRenderedBlockTapped(object sender, TappedRoutedEventArgs args)
    {
        if (IsReadOnly ||
            sender is not MarkdownDocumentView view ||
            !_elementBlockIndices.TryGetValue(view, out int index))
        {
            return;
        }

        ActivateBlock(index);
    }

    private void OnRenderedBlockTaskToggleRequested(
        object sender,
        MarkdownTaskToggleRequestedEventArgs args)
    {
        if (IsReadOnly ||
            sender is not MarkdownDocumentView view ||
            !_elementBlockIndices.TryGetValue(view, out int index))
        {
            return;
        }

        if (_model.TryToggleTask(index, args.TaskIndex))
        {
            _serializationScheduler.Flush();
            SynchronizeItemsToModel();
        }
    }

    private void OnActiveEditorTextChanged(object sender, TextChangedEventArgs args)
    {
        if (_isSynchronizing ||
            _isSynchronizingItems ||
            sender is not TextBox editor ||
            !ReferenceEquals(_activeEditor, editor))
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
        // A sync-displaced editor losing focus must not deactivate the new
        // active block; only a genuine user focus move commits.
        if (_isSynchronizingItems ||
            sender is not TextBox editor ||
            !ReferenceEquals(_activeEditor, editor))
        {
            return;
        }

        Deactivate();
    }

    private void ActivateBlock(int index)
    {
        if (IsReadOnly || index < 0 || index >= _model.Blocks.Count)
        {
            return;
        }

        if (_model.ActiveIndex == index && _activeEditor is not null)
        {
            return;
        }

        _serializationScheduler.Flush();
        // Fresh activations place the caret at the end; only recycling
        // restores a remembered offset.
        _pendingActiveCaretOffset = null;
        _model.SetActive(index);
        SynchronizeItemsToModel();
        ApplyPlaceholder();
    }

    private void Deactivate()
    {
        if (_model.ActiveIndex < 0)
        {
            return;
        }

        _serializationScheduler.Flush();
        _model.Deactivate();
        SynchronizeItemsToModel();
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

    /// <summary>Immutable block presentation record fed to the repeater.</summary>
    private sealed class MarkdownBlockItem
    {
        public required string Source { get; init; }

        public required bool IsActive { get; init; }
    }

    private sealed class MarkdownBlockTemplateSelector : DataTemplateSelector
    {
        public required DataTemplate RenderedTemplate { get; init; }

        public required DataTemplate ActiveTemplate { get; init; }

        protected override DataTemplate SelectTemplateCore(object item) =>
            item is MarkdownBlockItem { IsActive: true } ? ActiveTemplate : RenderedTemplate;
    }
}
