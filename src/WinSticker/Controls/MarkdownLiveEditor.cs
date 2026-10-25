using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using WinSticker.Services;

namespace WinSticker.Controls;

/// <summary>
/// Native Markdown live preview over one continuous source buffer. RichEdit
/// owns input, selection and viewport layout; source-only history keeps visual
/// formatting out of undo. Source capture and parsing run after input is idle.
/// </summary>
public sealed partial class MarkdownLiveEditor : UserControl
{
    private readonly RichEditBox _editor = new()
    {
        AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
        IsSpellCheckEnabled = false, IsTextPredictionEnabled = false,
        BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 16, 8),
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
    };
    private readonly Canvas _tasks = new();
    private readonly Grid _overlayViewport = new();
    private readonly MarkdownNativeBuffer _buffer = new();
    private readonly MarkdownSerializationScheduler _scheduler;
    private readonly Dictionary<int, CheckBox> _taskMarkers = [];
    private sealed record NativeTaskMarker(ITextRange Range, int DocumentGeneration);
    private MarkdownNativeSyntax _syntax = new([], []);
    private Task _presentationTask = Task.CompletedTask;
#if DEBUG
    internal RichEditBox NativeEditorForProbe => _editor;
    internal Task PresentationForProbe => _presentationTask;
    internal void RenderForProbe() => RenderViewport();
    internal FrameworkElement[] ComplexViewsForProbe => _embedded.Where(state => state.View is not null && _tasks.Children.Contains(state.View)).Select(state => (FrameworkElement)state.View!).ToArray();
    internal ScrollViewer? ScrollerForProbe => _nativeScroller;
    internal double InputCallbackMillisecondsForProbe { get; private set; }
    internal string ForegroundsForProbe => $"control={(Foreground as SolidColorBrush)?.Color};native={(_editor.Foreground as SolidColorBrush)?.Color};readers={string.Join(',', _embedded.Where(item => item.View is not null).Select(item => (item.View!.Foreground as SolidColorBrush)?.Color.ToString()))};cells={string.Join(';', _embedded.Where(item => item.View is not null).Select(item => item.View!.TableColorsForProbe))}";
    internal void ActivateComplexForProbe(int? start = null) => ActivateEmbeddedSource(_embedded.First(state => state.View is not null && (start is null || state.Range.StartPosition == start)), 2);
    internal int SourceCaptureCount { get; private set; }
    internal double LastCaptureMilliseconds { get; private set; }
    internal void UndoForProbe() => ApplyHistory(false);
    internal void RedoForProbe() => ApplyHistory(true);
    internal async Task ToggleFirstTaskAfterPrefixForProbeAsync(string prefix)
    {
        await _presentationTask;
        var task = _syntax.Tasks[0];
        var marker = new NativeTaskMarker(_editor.Document.GetRange(task.Start, task.Start + 3), _documentGeneration);
        RememberSelection();
        _editor.Document.GetRange(0, 0).SetText(TextSetOptions.None, prefix);
        await ToggleTaskAsync(marker);
    }
#endif
    private bool _internalChange;
    private bool _decorating;
    private bool _composing;
    private bool _dirty;
    private bool _renderQueued;
    private bool _focusRequested;
    private int _revision;
    private int _documentGeneration;
    private int _syntaxRevision = -1;
    private int _beforeCaret;
    private int _beforeLength;
    private int _lastCaret;
    private int _lastLength;
    private int _lastSelectionOptions;
    private int _beforeSelectionOptions;
    private int _rawStart = -1;
    private int _rawEnd = -1;
    private int _viewStart = -1;
    private int _viewEnd = -1;
    private ScrollViewer? _nativeScroller;
    internal RichEditBox NativeEditor => _editor;
    internal bool IsComposing => _composing;
    internal bool HasPendingSource => _dirty || _composing;
    internal string NativeSource => _buffer.NativeText;
    internal void ApplyFormattingCommand(MarkdownEditCommand command) => ApplyCommand(command);

    public MarkdownLiveEditor() : this(new DispatcherQueueSessionTimer(
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread(), TimeSpan.FromMilliseconds(150))) { }

    internal MarkdownLiveEditor(IMarkdownSessionTimer timer)
    {
        _scheduler = new(timer, CaptureSource, PublishSource);
        var root = new Grid();
        root.Children.Add(_editor);
        _overlayViewport.Children.Add(_tasks);
        root.Children.Add(_overlayViewport);
        _tasks.PointerWheelChanged += (_, args) =>
        {
            var properties = args.GetCurrentPoint(_tasks).Properties;
            if (properties.IsHorizontalMouseWheel || _nativeScroller is not { } scroller) return;
            scroller.ChangeView(null, Math.Clamp(scroller.VerticalOffset - properties.MouseWheelDelta * 0.5,
                0, scroller.ScrollableHeight), null, disableAnimation: true);
            args.Handled = true;
        };
        Content = root;
        foreach (string key in new[] { "TextControlBackground", "TextControlBackgroundFocused", "TextControlBackgroundPointerOver", "TextControlBorderBrushFocused" })
            _editor.Resources[key] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        AutomationProperties.SetName(_editor, "Markdown");
        ScrollViewer.SetHorizontalScrollBarVisibility(_editor, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(_editor, ScrollBarVisibility.Auto);
        _editor.Document.UndoLimit = 0;
        _editor.TextChanging += EditorTextChanging;
        _editor.SelectionChanged += (_, _) =>
        {
            if (_internalChange || _decorating || _composing) return;
            if (!_dirty)
            {
                _lastCaret = _editor.Document.Selection.StartPosition;
                _lastLength = _editor.Document.Selection.Length;
                _lastSelectionOptions = (int)_editor.Document.Selection.Options;
                QueuePresentation();
            }
        };
        _editor.TextCompositionStarted += (_, _) => { _composing = true; _scheduler.Suspend(); RememberSelection(); };
        _editor.TextCompositionEnded += (_, _) => { _composing = false; _scheduler.NotifyChanged(); };
        _editor.PreviewKeyDown += EditorKeyDown;
        _editor.Paste += async (_, args) => { args.Handled = true; await PasteTextAsync(); };
        _editor.GotFocus += (_, _) => QueuePresentation();
        _editor.LostFocus += (_, _) => { FlushAndGetText(); QueuePresentation(); };
        _editor.Loaded += (_, _) =>
        {
            ApplyOptions();
            _nativeScroller = FindScroller(_editor);
            if (_nativeScroller is not null)
            {
                _nativeScroller.ViewChanged += NativeViewChanged;
                // Keep preview geometry on the same compositor scroll clock as
                // native text; UI-thread ViewChanged callbacks can arrive later.
                ElementCompositionPreview.SetIsTranslationEnabled(_tasks, true);
                var visual = ElementCompositionPreview.GetElementVisual(_tasks);
                var animation = visual.Compositor.CreateExpressionAnimation("scroll.Translation.Y");
                animation.SetReferenceParameter("scroll", ElementCompositionPreview.GetScrollViewerManipulationPropertySet(_nativeScroller));
                visual.StartAnimation("Translation.Y", animation);
            }
            ConfigureContextMenu();
            if (_focusRequested) { _focusRequested = false; _editor.Focus(FocusState.Programmatic); }
            QueuePresentation(force: true);
        };
        _editor.Unloaded += (_, _) =>
        {
            FlushAndGetText();
            if (_nativeScroller is not null) _nativeScroller.ViewChanged -= NativeViewChanged;
            var visual = ElementCompositionPreview.GetElementVisual(_tasks);
            visual.StopAnimation("Translation.Y");
            visual.Properties.InsertVector3("Translation", System.Numerics.Vector3.Zero);
            _nativeScroller = null;
        };
        SizeChanged += (_, _) => QueuePresentation(force: true);
        ActualThemeChanged += (_, _) => DispatcherQueue.TryEnqueue(ApplyOptions);
        _editor.ActualThemeChanged += (_, _) => DispatcherQueue.TryEnqueue(ApplyOptions);
        RegisterPropertyChangedCallback(FontSizeProperty, (_, _) => ApplyOptions());
        RegisterPropertyChangedCallback(ForegroundProperty, (_, _) => ApplyOptions());
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string),
        typeof(MarkdownLiveEditor), new PropertyMetadata(string.Empty, OnTextChanged));
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(nameof(PlaceholderText), typeof(string),
        typeof(MarkdownLiveEditor), new PropertyMetadata(string.Empty, (d, _) => ((MarkdownLiveEditor)d).ApplyOptions()));
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool),
        typeof(MarkdownLiveEditor), new PropertyMetadata(false, (d, _) => ((MarkdownLiveEditor)d).ApplyOptions()));
    public static readonly DependencyProperty AllowRemoteImagesProperty = DependencyProperty.Register(nameof(AllowRemoteImages), typeof(bool),
        typeof(MarkdownLiveEditor), new PropertyMetadata(false, (d, _) => ((MarkdownLiveEditor)d).QueuePresentation(force: true)));
    public static readonly DependencyProperty IsLivePreviewEnabledProperty = DependencyProperty.Register(nameof(IsLivePreviewEnabled), typeof(bool),
        typeof(MarkdownLiveEditor), new PropertyMetadata(true, (d, _) => ((MarkdownLiveEditor)d).ResetPreviewMode()));
    /// <summary>Disable Markdown presentation when the host edits plain text.</summary>
    public bool IsLivePreviewEnabled { get => (bool)GetValue(IsLivePreviewEnabledProperty); set => SetValue(IsLivePreviewEnabledProperty, value); }
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set
        {
            string source = value ?? string.Empty;
            // An explicit session load can have identical text but a different
            // document identity. It must not inherit the previous note's undo.
            if (!_internalChange && source == Text) { LoadSource(source); return; }
            SetValue(TextProperty, source);
        }
    }
    public string PlaceholderText { get => (string)GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public bool AllowRemoteImages { get => (bool)GetValue(AllowRemoteImagesProperty); set => SetValue(AllowRemoteImagesProperty, value); }
    public event EventHandler? EditorTextChanged;

    /// <summary>Focus the continuous native editor without replacing its text.</summary>
    public void FocusEditor()
    {
        if (IsReadOnly) return;
        _focusRequested = !IsLoaded;
        _editor.Focus(FocusState.Programmatic);
        QueuePresentation();
    }

    /// <summary>Flush pending native edits before autosave or surface teardown.</summary>
    public string FlushAndGetText()
    {
        if (_composing) return _buffer.Source;
        if (_dirty && !_scheduler.HasPending) _scheduler.NotifyChanged();
        _scheduler.Flush();
        return _buffer.Source;
    }

    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (MarkdownLiveEditor)sender;
        if (!control._internalChange) control.LoadSource(args.NewValue as string ?? string.Empty);
    }

    private void LoadSource(string text)
    {
        _scheduler.DiscardPending();
        _documentGeneration++;
        _tasks.Children.Clear();
        _taskMarkers.Clear();
        _embedded.Clear();
        _buffer.Load(text);
        _internalChange = true;
        _editor.Document.BatchDisplayUpdates();
        try
        {
            // Reset the empty insertion format once. Applying every base
            // attribute to an entire large story needlessly repeats native
            // formatting work before viewport decoration even starts.
            _editor.Document.SetText(TextSetOptions.None, string.Empty);
            ResetFormat(_editor.Document.GetRange(0, int.MaxValue));
            ResetEmbeddedLayout(_editor.Document.GetRange(0, int.MaxValue));
            _editor.Document.SetText(TextSetOptions.None, _buffer.NativeText);
            _editor.Document.UndoLimit = 0;
            _editor.Document.Selection.SetRange(0, 0);
        }
        finally { _editor.Document.ApplyDisplayUpdates(); _internalChange = false; }
        _dirty = false;
        _lastCaret = _lastLength = 0;
        _lastSelectionOptions = 0;
        _revision++;
        ParsePresentation();
    }

    private void ApplyOptions()
    {
        _editor.PlaceholderText = PlaceholderText;
        _editor.IsReadOnly = IsReadOnly;
        _editor.FontSize = FontSize;
        _editor.FontFamily = FontFamily;
        _editor.Foreground = Foreground;
        foreach (var state in _embedded)
            if (state.View is { } view)
            {
                view.FontSize = FontSize;
            }
        QueuePresentation(force: true);
    }

    private void RememberSelection()
    {
        if (_dirty) return;
        _lastCaret = _editor.Document.Selection.StartPosition;
        _lastLength = _editor.Document.Selection.Length;
        _lastSelectionOptions = (int)_editor.Document.Selection.Options;
    }

    private void EditorTextChanging(RichEditBox sender, RichEditBoxTextChangingEventArgs args)
    {
        if (_internalChange || _decorating || !args.IsContentChanging) return;
#if DEBUG
        long inputStarted = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        if (!_dirty) { _beforeCaret = _lastCaret; _beforeLength = _lastLength; _beforeSelectionOptions = _lastSelectionOptions; }
        _dirty = true;
        _revision++;
        // No source extraction, parsing, DP assignment or formatting on a keystroke.
        if (!_composing) _scheduler.NotifyChanged();
#if DEBUG
        InputCallbackMillisecondsForProbe += System.Diagnostics.Stopwatch.GetElapsedTime(inputStarted).TotalMilliseconds;
#endif
    }

    private string CaptureSource()
    {
        if (_dirty)
        {
#if DEBUG
            long captureStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            SourceCaptureCount++;
#endif
            _editor.Document.GetText(TextGetOptions.None, out string text);
            // RichEdit includes its mandatory final EOP, distinct from a user's
            // trailing blank lines. Remove exactly that final paragraph marker.
            if (text.EndsWith('\r')) text = text[..^1];
            var selection = _editor.Document.Selection;
            _buffer.Capture(text, _beforeCaret, selection.StartPosition, _beforeLength, selection.Length, _beforeSelectionOptions, (int)selection.Options);
            _lastCaret = selection.StartPosition;
            _lastLength = selection.Length;
            _lastSelectionOptions = (int)selection.Options;
            _dirty = false;
#if DEBUG
            LastCaptureMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(captureStarted).TotalMilliseconds;
#endif
            ParsePresentation();
        }
        return _buffer.Source;
    }

    private void PublishSource(string source)
    {
        if (Text == source) return;
        _internalChange = true;
        try { Text = source; }
        finally { _internalChange = false; }
        EditorTextChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ParsePresentation() => _presentationTask = ParsePresentationAsync();

    private async Task ParsePresentationAsync()
    {
        int revision = _revision;
        string source = _buffer.NativeText;
        MarkdownNativeSyntax syntax;
        try { syntax = await Task.Run(() => MarkdownNativeSyntax.Parse(source)); }
        catch (Exception error)
        {
            App.Log($"[Markdown] Presentation parsing failed: {error}");
            return;
        }
        if (_revision != revision) return;
        _syntax = syntax;
        _syntaxRevision = revision;
        QueuePresentation(force: true);
    }

    private void NativeViewChanged(object? sender, ScrollViewerViewChangedEventArgs args) => QueuePresentation();

    private void QueuePresentation(bool force = false)
    {
        if (force) _viewStart = -1;
        if (_renderQueued || _composing || _dirty || !IsLoaded) return;
        _renderQueued = true;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            _renderQueued = false;
            if (IsLoaded && !_composing && !_dirty && _syntaxRevision == _revision) RenderViewport();
        });
    }

    private void RenderViewport()
    {
        RefreshEmbeddedAppearance();
        string source = _buffer.NativeText;
        var selection = _editor.Document.Selection;
        int originalStart = selection.StartPosition;
        int originalEnd = selection.EndPosition;
        SelectionOptions originalOptions = selection.Options;
        bool focused = _editor.FocusState != FocusState.Unfocused && !IsReadOnly;
        int caret = Math.Clamp(selection.StartPosition, 0, source.Length);
        int end = Math.Clamp(selection.EndPosition, caret, source.Length);
        int rawStart = focused ? LineStart(source, caret) : -1;
        int rawEnd = focused ? LineEnd(source, end) : -1;
        // RichEdit's client is the ScrollViewer content, so its geometry is
        // unscrolled. Convert viewport points to that coordinate system.
        double scrollOffset = _nativeScroller?.VerticalOffset ?? 0;
        int first = _editor.Document.GetRangeFromPoint(new Point(8, 8 + scrollOffset), PointOptions.ClientCoordinates).StartPosition;
        int last = _editor.Document.GetRangeFromPoint(new Point(Math.Max(8, _editor.ActualWidth - 20), Math.Max(8, _editor.ActualHeight - 8) + scrollOffset), PointOptions.ClientCoordinates).StartPosition;
        int viewStart = LineStart(source, Math.Clamp(Math.Min(first, last), 0, source.Length));
        int viewEnd = LineEnd(source, Math.Clamp(Math.Max(first, last), 0, source.Length));
        // Adjacent lines cover a small amount of scroll overscan.
        if (viewStart > 0) viewStart = LineStart(source, viewStart - 1);
        if (viewEnd < source.Length) viewEnd = LineEnd(source, viewEnd + 1);
        bool viewportChanged = viewStart != _viewStart || viewEnd != _viewEnd;
        bool activeChanged = rawStart != _rawStart || rawEnd != _rawEnd;
        if (!viewportChanged && rawStart == _rawStart && rawEnd == _rawEnd && !_embeddedLayoutDirty)
        {
            UpdateTaskMarkers();
            PositionEmbeddedViews();
            return;
        }
        _embeddedLayoutDirty = false;
        _decorating = true;
        _editor.Document.BatchDisplayUpdates();
        try
        {
            if (viewportChanged)
            {
                if (_viewStart >= 0 && viewStart < _viewEnd && viewEnd > _viewStart)
                {
                    if (viewStart < _viewStart) FormatRange(viewStart, Math.Min(viewEnd, _viewStart));
                    if (viewEnd > _viewEnd) FormatRange(Math.Max(viewStart, _viewEnd), viewEnd);
                }
                else FormatRange(viewStart, viewEnd);
            }
            if (_rawStart >= 0 && activeChanged && _rawStart <= viewEnd && _rawEnd >= viewStart) FormatRange(_rawStart, _rawEnd);
            if (rawStart >= 0 && rawEnd > rawStart && (activeChanged || (viewportChanged && rawStart <= viewEnd && rawEnd >= viewStart)))
            {
                var activeRange = _editor.Document.GetRange(rawStart, rawEnd);
                activeRange.CharacterFormat.Hidden = FormatEffect.Off;
                activeRange.ParagraphFormat.SetIndents(0, 0, 0);
            }
            UpdateEmbeddedViews(viewStart, viewEnd, rawStart, rawEnd);
            _rawStart = rawStart;
            _rawEnd = rawEnd;
            _viewStart = viewStart;
            _viewEnd = viewEnd;
        }
        finally
        {
            _editor.Document.ApplyDisplayUpdates();
            if (selection.StartPosition != originalStart || selection.EndPosition != originalEnd)
                selection.SetRange(originalStart, originalEnd);
            const SelectionOptions preserved = SelectionOptions.StartActive | SelectionOptions.AtEndOfLine;
            SelectionOptions restoredOptions = (selection.Options & ~preserved) | (originalOptions & preserved);
            if (selection.Options != restoredOptions) selection.Options = restoredOptions;
            // A formatting transaction must not bring an off-screen caret back
            // into view while the user is scrolling elsewhere in the document.
            if (!activeChanged && _nativeScroller is { } scroller && Math.Abs(scroller.VerticalOffset - scrollOffset) > 0.5)
                scroller.ChangeView(null, scrollOffset, null, disableAnimation: true);
            else if (activeChanged && focused && _nativeScroller is { } activeScroller)
            {
                // Expanding a preview above the new caret changes its geometry
                // after native selection navigation has already scrolled it.
                int activeCaret = originalOptions.HasFlag(SelectionOptions.StartActive) ? originalStart : originalEnd;
                _editor.Document.GetRange(activeCaret, activeCaret).GetRect(PointOptions.ClientCoordinates, out Rect caretRect, out _);
                double top = caretRect.Y - activeScroller.VerticalOffset;
                double lineHeight = Math.Max(caretRect.Height, FontSize * 1.4);
                if (top < 6 || top + lineHeight > _editor.ActualHeight - 8)
                {
                    double target = top < 6 ? caretRect.Y - 6 : caretRect.Y + lineHeight - _editor.ActualHeight + 8;
                    activeScroller.ChangeView(null, Math.Clamp(target, 0, activeScroller.ScrollableHeight), null, disableAnimation: true);
                }
            }
            _decorating = false;
        }
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, UpdateTaskMarkers);
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, PositionEmbeddedViews);
    }

    private void FormatRange(int start, int end)
    {
        if (end <= start) return;
        var range = _editor.Document.GetRange(start, end);
        ResetFormat(range);
        if (!IsLivePreviewEnabled) return;
        foreach (MarkdownNativeSpan span in _syntax.Spans)
        {
            int from = Math.Max(start, span.Start);
            int to = Math.Min(end, span.Start + span.Length);
            if (to <= from) continue;
            var style = _editor.Document.GetRange(from, to).CharacterFormat;
            if (span.Style.HasFlag(MarkdownNativeStyle.Hidden)) style.Hidden = FormatEffect.On;
            if (span.Style.HasFlag(MarkdownNativeStyle.Bold)) style.Bold = FormatEffect.On;
            if (span.Style.HasFlag(MarkdownNativeStyle.Italic)) style.Italic = FormatEffect.On;
            if (span.Style.HasFlag(MarkdownNativeStyle.Strike)) style.Strikethrough = FormatEffect.On;
            if (span.Style.HasFlag(MarkdownNativeStyle.Code)) style.Name = "Consolas";
            if (span.Style.HasFlag(MarkdownNativeStyle.Quote))
                _editor.Document.GetRange(from, to).ParagraphFormat.SetIndents(0, 12, 0);
            if (span.Style.HasFlag(MarkdownNativeStyle.Link)) style.Underline = UnderlineType.Single;
            if (span.HeadingLevel > 0) style.Size = (float)(FontSize * 0.75 * (span.HeadingLevel switch { 1 => 1.62, 2 => 1.38, 3 => 1.20, _ => 1.08 }));
        }
        foreach (var task in _syntax.Tasks)
            if (task.LineStart < end && task.LineEnd > start)
                _editor.Document.GetRange(task.LineStart, task.LineEnd).ParagraphFormat.SetIndents(0, 18, 0);
    }

    private void ResetFormat(ITextRange range)
    {
        var format = range.CharacterFormat;
        format.Hidden = FormatEffect.Off;
        format.Bold = format.Italic = format.Strikethrough = FormatEffect.Off;
        format.Underline = UnderlineType.None;
        format.Size = (float)(FontSize * 0.75);
        format.Name = FontFamily.Source;
        format.ForegroundColor = Foreground is SolidColorBrush brush ? brush.Color : ActualTheme == ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
        format.BackgroundColor = Microsoft.UI.Colors.Transparent;
        range.ParagraphFormat.SetIndents(0, 0, 0);
    }

    private void UpdateTaskMarkers()
    {
        if (_dirty || _composing || _syntaxRevision != _revision) return;
        var visible = new HashSet<int>();
        foreach (var task in IsLivePreviewEnabled ? _syntax.Tasks : [])
        {
            if (task.LineStart >= _viewEnd || task.LineEnd < _viewStart ||
                (task.LineStart < _rawEnd && task.LineEnd >= _rawStart && _rawStart >= 0)) continue;
            _editor.Document.GetRange(task.Start + 3, task.Start + 3).GetRect(PointOptions.ClientCoordinates, out Rect rect, out _);
            double viewportY = rect.Y - (_nativeScroller?.VerticalOffset ?? 0);
            if (viewportY < 0 || viewportY + 18 > ActualHeight) continue;
            visible.Add(task.Start);
            if (!_taskMarkers.TryGetValue(task.Start, out CheckBox? marker))
            {
                marker = new CheckBox { MinWidth = 0, MinHeight = 0, Padding = new Thickness(0) };
                marker.Click += TaskMarkerClick;
                _taskMarkers.Add(task.Start, marker);
                _tasks.Children.Add(marker);
            }
            marker.Tag = new NativeTaskMarker(_editor.Document.GetRange(task.Start, task.Start + 3), _documentGeneration);
            marker.IsChecked = task.IsChecked;
            marker.IsEnabled = !IsReadOnly;
            marker.TabIndex = task.Start + 1;
            AutomationProperties.SetName(marker, _buffer.NativeText.Substring(task.Start + 3, task.LineEnd - task.Start - 3).Trim());
            Canvas.SetLeft(marker, Math.Max(0, rect.X - 24));
            Canvas.SetTop(marker, rect.Y);
        }
        foreach (int key in _taskMarkers.Keys.Where(key => !visible.Contains(key)).ToArray())
        {
            _tasks.Children.Remove(_taskMarkers[key]);
            _taskMarkers.Remove(key);
        }
    }

    private async void TaskMarkerClick(object sender, RoutedEventArgs args)
    {
        if (IsReadOnly || sender is not CheckBox { Tag: NativeTaskMarker marker }) return;
        await ToggleTaskAsync(marker);
    }

    private async Task ToggleTaskAsync(NativeTaskMarker marker)
    {
        FlushAndGetText();
        await _presentationTask;
        if (IsReadOnly || marker.DocumentGeneration != _documentGeneration || _syntaxRevision != _revision) return;
        int start = marker.Range.StartPosition;
        MarkdownNativeTask? task = _syntax.Tasks.FirstOrDefault(task => task.Start == start);
        if (task is null) return;
        RememberSelection();
        _editor.Document.GetRange(start + 1, start + 2).SetText(TextSetOptions.None, task.IsChecked ? " " : "x");
        FlushAndGetText();
    }

    private void EditorKeyDown(object sender, KeyRoutedEventArgs args)
    {
        RememberSelection();
        if (_composing) return;
        bool control = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        bool shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
        if (control && args.Key is VirtualKey.Z or VirtualKey.Y)
        {
            args.Handled = true;
            if (!IsReadOnly) ApplyHistory(args.Key == VirtualKey.Y || shift);
        }
        else if (control && args.Key is VirtualKey.C or VirtualKey.X)
        {
            args.Handled = true;
            CopySource(args.Key == VirtualKey.X);
        }
        else if (control && args.Key is VirtualKey.B or VirtualKey.I or VirtualKey.K)
        {
            args.Handled = true;
            ApplyCommand(args.Key == VirtualKey.B ? MarkdownEditCommand.Bold : args.Key == VirtualKey.I ? MarkdownEditCommand.Italic : MarkdownEditCommand.Link);
        }
        else if (args.Key == VirtualKey.Enter && !control && !shift && !IsReadOnly && IsLivePreviewEnabled)
        {
            _scheduler.Flush();
            var selection = _editor.Document.Selection;
            if (selection.Length == 0 && MarkdownNativeContinuation.TryCreate(_buffer.NativeText, selection.StartPosition, out var edit))
            {
                args.Handled = true;
                RememberSelection();
                _editor.Document.GetRange(edit.Start, edit.Start + edit.Length).SetText(TextSetOptions.None, edit.Replacement);
                selection.SetRange(edit.SelectionStart, edit.SelectionStart);
            }
        }
        else if (args.Key == VirtualKey.Tab && control)
        {
            args.Handled = true;
            MoveFocusOutsideEditor(shift);
        }
        else if (args.Key == VirtualKey.Tab)
        {
            args.Handled = true;
            if (IsReadOnly || !IsLivePreviewEnabled) MoveFocusOutsideEditor(shift);
            else ApplyCommand(shift ? MarkdownEditCommand.Outdent : MarkdownEditCommand.Indent);
        }
        else if (args.Key == VirtualKey.Escape)
        {
            FlushAndGetText();
            MoveFocusOutsideEditor(previous: false);
            args.Handled = true;
        }
    }

    private void MoveFocusOutsideEditor(bool previous)
    {
        if (XamlRoot?.Content is not { } root) return;
        FocusManager.TryMoveFocus(previous ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next,
            new FindNextElementOptions { SearchRoot = root });
    }

    private void ApplyHistory(bool redo)
    {
        _scheduler.Flush();
        MarkdownNativeEdit? edit = redo ? _buffer.Redo() : _buffer.Undo();
        if (edit is null) return;
        _internalChange = true;
        try
        {
            _editor.Document.GetRange(edit.Start, edit.Start + edit.Length).SetText(TextSetOptions.None, edit.Replacement);
            _editor.Document.Selection.SetRange(edit.Caret, edit.Caret + edit.SelectionLength);
            const SelectionOptions preserved = SelectionOptions.StartActive | SelectionOptions.AtEndOfLine;
            var selection = _editor.Document.Selection;
            selection.Options = (selection.Options & ~preserved) | ((SelectionOptions)edit.SelectionOptions & preserved);
        }
        finally { _internalChange = false; }
        _revision++;
        PublishSource(_buffer.Source);
        ParsePresentation();
    }

    private void ApplyCommand(MarkdownEditCommand command)
    {
        if (IsReadOnly) return;
        _scheduler.Flush();
        var selection = _editor.Document.Selection;
        if (!MarkdownEditCommandEngine.TryCreateEdit(_buffer.NativeText.Replace('\r', '\n'), selection.StartPosition, selection.Length, command, out var edit)) return;
        RememberSelection();
        _editor.Document.GetRange(edit.Start, edit.Start + edit.Length).SetText(TextSetOptions.None, MarkdownNativeBuffer.ToNative(edit.Replacement));
        selection.SetRange(edit.SelectionStart, edit.SelectionStart + edit.SelectionLength);
        _scheduler.Flush();
    }

    private async Task PasteTextAsync()
    {
        if (IsReadOnly) return;
        int document = _documentGeneration;
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text)) return;
            string text = await content.GetTextAsync();
            if (IsReadOnly || document != _documentGeneration) return;
            RememberSelection();
            var selection = _editor.Document.Selection;
            _editor.Document.GetRange(selection.StartPosition, selection.EndPosition)
                .SetText(TextSetOptions.None, MarkdownNativeBuffer.ToNative(text));
        }
        catch (Exception error) { App.Log($"[Markdown] Clipboard paste failed: {error.Message}"); }
    }

    private void CopySource(bool cut)
    {
        if (cut && IsReadOnly) return;
        FlushAndGetText();
        var selection = _editor.Document.Selection;
        if (selection.Length == 0) return;
        int end = Math.Min(selection.EndPosition, _buffer.NativeText.Length);
        var range = _editor.Document.GetRange(selection.StartPosition, end);
        range.GetText(TextGetOptions.UseCrlf, out string text);
        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            if (cut) { RememberSelection(); range.SetText(TextSetOptions.None, string.Empty); }
        }
        catch (Exception error) { App.Log($"[Markdown] Clipboard copy failed: {error.Message}"); }
    }

    private void ConfigureContextMenu()
    {
        var menu = new MenuFlyout();
        void Add(string key, Action action)
        {
            var item = new MenuFlyoutItem { Text = App.Current.LocalizationService.T(key) };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        Add("Common.Undo", () => { if (!IsReadOnly) ApplyHistory(false); });
        Add("Common.Redo", () => { if (!IsReadOnly) ApplyHistory(true); });
        Add("Common.Copy", () => CopySource(false));
        Add("Common.Cut", () => CopySource(true));
        Add("Common.Paste", () => _ = PasteTextAsync());
        Add("Common.SelectAll", () => { FlushAndGetText(); _editor.Document.Selection.SetRange(0, _buffer.NativeText.Length); });
        _editor.ContextFlyout = menu;
    }

    private static int LineStart(string source, int offset) => offset == 0 ? 0 : source.LastIndexOf('\r', offset - 1) + 1;
    private static int LineEnd(string source, int offset) { int end = source.IndexOf('\r', offset); return end < 0 ? source.Length : end; }
    private static ScrollViewer? FindScroller(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer scroller) return scroller;
            if (FindScroller(child) is { } nested) return nested;
        }
        return null;
    }
}
