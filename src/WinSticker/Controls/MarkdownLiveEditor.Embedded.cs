using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using WinSticker.Models;
using WinSticker.Services;

namespace WinSticker.Controls;

public sealed partial class MarkdownLiveEditor
{
    // Text remains in RichEdit. Only visible complex blocks have XAML readers;
    // tracked native ranges retain their measured space while off screen.
    private sealed class EmbeddedPresentation(ITextRange range, string source, int generation)
    {
        public ITextRange Range { get; } = range;
        public string Source { get; } = source;
        public int Generation { get; } = generation;
        public MarkdownDocumentView? View { get; set; }
        public double Height { get; set; } = 100;
        public bool Reserved { get; set; }
        public double ReservedHeight { get; set; }
    }

    private readonly List<EmbeddedPresentation> _embedded = [];
    private bool _embeddedLayoutDirty;
    private Func<string, string?>? _attachmentResolver;
    private (ElementTheme Theme, double Size, Windows.UI.Color Foreground)? _embeddedAppearance;

    private void RefreshEmbeddedAppearance()
    {
        var current = (_editor.ActualTheme, FontSize, (Foreground as Microsoft.UI.Xaml.Media.SolidColorBrush)?.Color ?? default);
        if (_embeddedAppearance is { } previous && previous.Equals(current)) return;
        _embeddedAppearance = current;
        // Detached readers keep inline UI with concrete brushes. Their cache
        // belongs to this appearance, just as it belongs to the source block.
        foreach (var state in _embedded)
        {
            if (state.View is not { } view) continue;
            _tasks.Children.Remove(view);
            state.View = null;
        }
        _viewStart = -1;
        _embeddedLayoutDirty = true;
    }

    /// <summary>Resolve note attachments using the same policy as its reader.</summary>
    public Func<string, string?>? AttachmentResolver
    {
        get => _attachmentResolver;
        set
        {
            _attachmentResolver = value;
            foreach (var state in _embedded)
                if (state.View is { } view) view.AttachmentResolver = value;
        }
    }

    private static void ResetEmbeddedLayout(ITextRange range)
    {
        range.ParagraphFormat.SetLineSpacing(LineSpacingRule.Single, 0);
        range.ParagraphFormat.SpaceBefore = range.ParagraphFormat.SpaceAfter = 0;
    }

    private void UpdateEmbeddedViews(int viewStart, int viewEnd, int rawStart, int rawEnd)
    {
        if (!IsLivePreviewEnabled) return;
        var blocks = _syntax.EmbeddedBlocks.ToDictionary(block => block.Start);
        foreach (var state in _embedded.ToArray())
        {
            if (!blocks.TryGetValue(state.Range.StartPosition, out var block) || block.Source != state.Source)
            {
                ResetEmbeddedLayout(state.Range);
                state.Range.CharacterFormat.Hidden = FormatEffect.Off;
                if (state.View is not null) _tasks.Children.Remove(state.View);
                _embedded.Remove(state);
            }
        }
        foreach (var block in _syntax.EmbeddedBlocks)
        {
            int end = block.Start + block.Length;
            bool active = rawStart >= 0 && block.Start <= rawEnd && end >= rawStart;
            bool visible = block.Start <= viewEnd && end >= viewStart;
            var state = _embedded.FirstOrDefault(item => item.Range.StartPosition == block.Start);
            if (!visible && !active)
            {
                if (state?.View is { } oldView)
                {
                    _tasks.Children.Remove(oldView);
                }
                continue;
            }
            if (active)
            {
                if (state is not null)
                {
                    if (state.Reserved) ResetEmbeddedLayout(state.Range);
                    state.Reserved = false;
                    if (state.View is not null) _tasks.Children.Remove(state.View);
                }
                if (visible)
                {
                    FormatRange(block.Start, end);
                    _editor.Document.GetRange(block.Start, end).CharacterFormat.Hidden = FormatEffect.Off;
                }
                continue;
            }
            if (state is null)
            {
                state = new(_editor.Document.GetRange(block.Start, end), block.Source, _documentGeneration);
                _embedded.Add(state);
            }
            if (state.View is null)
            {
                var view = new MarkdownDocumentView
                {
                    UseInternalScrollViewer = false, AreTaskListsInteractive = false,
                    ContentFormat = TextContentFormat.Markdown, FontSize = FontSize,
                    AllowRemoteImages = AllowRemoteImages,
                    AttachmentResolver = AttachmentResolver, Markdown = block.Source,
                    Tag = state, IsTabStop = !IsReadOnly
                };
                state.View = view;
                view.SetBinding(Control.ForegroundProperty, new Microsoft.UI.Xaml.Data.Binding
                {
                    Source = this, Path = new PropertyPath(nameof(Foreground)), Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay
                });
                view.AddHandler(PointerPressedEvent, new PointerEventHandler(EmbeddedViewPointerPressed), handledEventsToo: true);
                view.AddHandler(TappedEvent, new TappedEventHandler(EmbeddedViewTapped), handledEventsToo: true);
                view.AddHandler(KeyDownEvent, new KeyEventHandler(EmbeddedViewKeyDown), handledEventsToo: true);
                view.SizeChanged += (_, args) =>
                {
                    if (Math.Abs(state.Height - args.NewSize.Height) < 1 || args.NewSize.Height < 1) return;
                    state.Height = args.NewSize.Height;
                    _embeddedLayoutDirty = true;
                    QueuePresentation();
                };
            }
            if (!_tasks.Children.Contains(state.View)) _tasks.Children.Insert(0, state.View);
            state.View.Width = Math.Max(1, _editor.ActualWidth - 32);
            state.View.FontSize = FontSize;
            state.View.Foreground = Foreground;
            state.View.AllowRemoteImages = AllowRemoteImages;
            state.View.IsTabStop = !IsReadOnly;
            state.View.TabIndex = block.Start + 1;
            ReserveEmbeddedSpace(state, block);
        }
        // Keep a small detached cache for activation and short scroll reversals.
        // UI outside the viewport stays bounded independently of document size.
        var detached = _embedded.Where(state => state.View is not null && !_tasks.Children.Contains(state.View)).ToArray();
        foreach (var state in detached.Take(Math.Max(0, detached.Length - 8))) state.View = null;
    }

    private void ResetPreviewMode()
    {
        _decorating = true;
        _editor.Document.BatchDisplayUpdates();
        try
        {
            foreach (var state in _embedded)
            {
                ResetEmbeddedLayout(state.Range);
                if (state.View is not null) _tasks.Children.Remove(state.View);
            }
            _embedded.Clear();
            ResetFormat(_editor.Document.GetRange(0, int.MaxValue));
        }
        finally { _editor.Document.ApplyDisplayUpdates(); _decorating = false; }
        QueuePresentation(force: true);
    }

    private void ReserveEmbeddedSpace(EmbeddedPresentation state, MarkdownNativeEmbeddedBlock block)
    {
        // Keep paragraph marks visible: hiding them would merge paragraphs and
        // invalidate selection geometry. Each source line occupies one point.
        int line = block.Start;
        int end = block.Start + block.Length;
        int lines = 0;
        while (line <= end)
        {
            int next = LineEnd(_buffer.NativeText, line);
            next = Math.Min(next, end);
            _editor.Document.GetRange(line, next).CharacterFormat.Hidden = FormatEffect.On;
            lines++;
            if (next == end) break;
            line = next + 1;
        }
        if (!state.Reserved || Math.Abs(state.ReservedHeight - state.Height) >= 1)
        {
            var paragraph = state.Range.ParagraphFormat;
            if (!state.Reserved)
            {
                paragraph.SetIndents(0, 0, 0);
                paragraph.SetLineSpacing(LineSpacingRule.Exactly, 1);
                paragraph.SpaceBefore = paragraph.SpaceAfter = 0;
            }
            int last = LineStart(_buffer.NativeText, end);
            _editor.Document.GetRange(last, end).ParagraphFormat.SpaceAfter = (float)Math.Max(0, state.Height * 0.75 - lines);
            state.ReservedHeight = state.Height;
        }
        state.Reserved = true;
    }

    private void PositionEmbeddedViews()
    {
        _overlayViewport.Clip = new RectangleGeometry { Rect = new Rect(0, 0, Math.Max(0, ActualWidth), Math.Max(0, ActualHeight)) };
        foreach (var state in _embedded)
        {
            if (state.View is not { } view || !state.Reserved) continue;
            _editor.Document.GetRange(state.Range.StartPosition, state.Range.StartPosition)
                .GetRect(PointOptions.ClientCoordinates, out Rect rect, out _);
            Canvas.SetLeft(view, 8);
            Canvas.SetTop(view, rect.Y);
        }
    }

    private static int EmbeddedSourceOffset(object sender, object originalSource)
    {
        for (var element = originalSource as DependencyObject; element is UIElement && !ReferenceEquals(element, sender);
            element = VisualTreeHelper.GetParent(element))
            if (element is RichTextBlock { Tag: int offset }) return offset;
        return 0;
    }

    private void EmbeddedViewPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (IsReadOnly || _dirty || _composing || _syntaxRevision != _revision ||
            sender is not MarkdownDocumentView { Tag: EmbeddedPresentation state } || !state.Reserved ||
            !args.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed) return;
        args.Handled = true;
        ActivateEmbeddedSource(state, EmbeddedSourceOffset(sender, args.OriginalSource));
    }

    private async void EmbeddedViewTapped(object sender, TappedRoutedEventArgs args)
    {
        if (IsReadOnly || sender is not MarkdownDocumentView { Tag: EmbeddedPresentation state } || !state.Reserved) return;
        int relative = EmbeddedSourceOffset(sender, args.OriginalSource);
        args.Handled = true;
        FlushAndGetText();
        await _presentationTask;
        if (state.Generation != _documentGeneration || _syntaxRevision != _revision) return;
        ActivateEmbeddedSource(state, relative);
    }

    private async void EmbeddedViewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != Windows.System.VirtualKey.Enter || IsReadOnly ||
            sender is not MarkdownDocumentView { Tag: EmbeddedPresentation state } || !state.Reserved) return;
        args.Handled = true;
        FlushAndGetText();
        await _presentationTask;
        if (state.Generation != _documentGeneration || _syntaxRevision != _revision) return;
        ActivateEmbeddedSource(state, 0);
    }

    private void ActivateEmbeddedSource(EmbeddedPresentation state, int relative)
    {
        int start = state.Range.StartPosition;
        ResetEmbeddedLayout(state.Range);
        state.Range.CharacterFormat.Hidden = FormatEffect.Off;
        if (state.View is not null) _tasks.Children.Remove(state.View);
        state.Reserved = false;
        _editor.Focus(FocusState.Programmatic);
        int caret = start + Math.Clamp(relative, 0, state.Source.Length);
        _editor.Document.Selection.SetRange(caret, caret);
        PositionEmbeddedViews();
        QueuePresentation(force: true);
    }
}
