using WinSticker.Services;
using WinSticker.Helpers;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace WinSticker.Controls;

/// <summary>Formatting and save shortcuts over the continuous native Markdown editor.</summary>
public sealed partial class MarkdownSourceEditor : UserControl
{
    private const double FormattingButtonWidth = 28;
    private const double FormattingButtonSpacing = 1;
    private bool _isSynchronizingText;
    private Func<string, string>? _textResolver;
    private (int Start, int End)? _pendingFormatSelection;

    public MarkdownSourceEditor()
    {
        InitializeComponent();
        FormattingToolbar.AddHandler(PointerPressedEvent, new PointerEventHandler(FormattingToolbar_PointerPressed), true);
        FormattingMenuPanel.AddHandler(PointerPressedEvent, new PointerEventHandler(FormattingToolbar_PointerPressed), true);
        AddHandler(PreviewKeyDownEvent, new KeyEventHandler(EditorSurface_PreviewKeyDown), true);
        EditorSurface.EditorTextChanged += EditorSurface_EditorTextChanged;
        ApplyToolbarVisibility();
        ApplyLocalizedText();
    }
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(MarkdownSourceEditor),
        new PropertyMetadata(string.Empty, OnTextPropertyChanged));

    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText),
        typeof(string),
        typeof(MarkdownSourceEditor),
        new PropertyMetadata("Write with Markdown…"));

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly),
        typeof(bool),
        typeof(MarkdownSourceEditor),
        new PropertyMetadata(false));

    public static readonly DependencyProperty EditorFontSizeProperty = DependencyProperty.Register(
        nameof(EditorFontSize),
        typeof(double),
        typeof(MarkdownSourceEditor),
        new PropertyMetadata(14.0));

    public static readonly DependencyProperty ShowFormattingToolbarProperty = DependencyProperty.Register(
        nameof(ShowFormattingToolbar),
        typeof(bool),
        typeof(MarkdownSourceEditor),
        new PropertyMetadata(true, OnToolbarVisibilityChanged));

    public static readonly DependencyProperty EditorEnterBehaviorProperty = DependencyProperty.Register(
        nameof(EditorEnterBehavior),
        typeof(string),
        typeof(MarkdownSourceEditor),
        new PropertyMetadata(SettingsService.EditorEnterBehaviorCtrlEnterSaves));

    public static readonly DependencyProperty EnableCtrlSSaveShortcutProperty = DependencyProperty.Register(
        nameof(EnableCtrlSSaveShortcut),
        typeof(bool),
        typeof(MarkdownSourceEditor),
        new PropertyMetadata(false));

    public string Text
    {
        get => FlushAndGetText();
        set { if ((string)GetValue(TextProperty) == value) EditorSurface.Text = value; else SetValue(TextProperty, value); }
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

    public double EditorFontSize
    {
        get => (double)GetValue(EditorFontSizeProperty);
        set => SetValue(EditorFontSizeProperty, value);
    }

    public bool ShowFormattingToolbar
    {
        get => (bool)GetValue(ShowFormattingToolbarProperty);
        set => SetValue(ShowFormattingToolbarProperty, value);
    }

    public string EditorEnterBehavior
    {
        get => (string)GetValue(EditorEnterBehaviorProperty);
        set => SetValue(EditorEnterBehaviorProperty, value);
    }

    public bool EnableCtrlSSaveShortcut
    {
        get => (bool)GetValue(EnableCtrlSSaveShortcutProperty);
        set => SetValue(EnableCtrlSSaveShortcutProperty, value);
    }

    public Func<string, string>? TextResolver
    {
        get => _textResolver;
        set
        {
            _textResolver = value;
            ApplyLocalizedText();
        }
    }

    /// <summary>Resolve attachments with the same policy as the note reader.</summary>
    public Func<string, string?>? AttachmentResolver { get => EditorSurface.AttachmentResolver; set => EditorSurface.AttachmentResolver = value; }
    /// <summary>Whether remote image previews are allowed for this note.</summary>
    public bool AllowRemoteImages { get => EditorSurface.AllowRemoteImages; set => EditorSurface.AllowRemoteImages = value; }
    public event EventHandler? EditorTextChanged;
    public event EventHandler? TextTruncated;
    public event EventHandler? CommitRequested;
    public event EventHandler? CancelRequested;
    internal MarkdownLiveEditor LiveEditorForProbe => EditorSurface;
    internal MarkdownLiveEditor LiveEditor => EditorSurface;

    /// <summary>Capture pending input before saving, switching notes or closing the surface.</summary>
    public string FlushAndGetText() => EditorSurface is null ? (string)GetValue(TextProperty) : EditorSurface.FlushAndGetText();

    public void FocusEditor(bool selectAll = false, bool moveCaretToEnd = false)
    {
        FlushAndGetText();
        EditorSurface.FocusEditor();
        if (selectAll) EditorSurface.NativeEditor.Document.Selection.SetRange(0, EditorSurface.NativeSource.Length);
        else if (moveCaretToEnd) EditorSurface.NativeEditor.Document.Selection.SetRange(EditorSurface.NativeSource.Length, EditorSurface.NativeSource.Length);
    }

    public bool FindNext(string? value, StringComparison comparison = StringComparison.CurrentCultureIgnoreCase)
    {
        if (string.IsNullOrEmpty(value)) return false;
        FlushAndGetText();
        string source = EditorSurface.NativeSource;
        string search = MarkdownNativeBuffer.ToNative(value);
        int start = Math.Min(source.Length, EditorSurface.NativeEditor.Document.Selection.EndPosition);
        int index = source.IndexOf(search, start, comparison);
        if (index < 0) index = source.IndexOf(search, comparison);
        if (index < 0) return false;
        EditorSurface.FocusEditor();
        EditorSurface.NativeEditor.Document.Selection.SetRange(index, index + search.Length);
        EditorSurface.NativeEditor.Document.Selection.ScrollIntoView(PointOptions.None);
        return true;
    }

    public void ApplyFormat(string action)
    {
        if (IsReadOnly || !TryResolveCommand(action, out var command)) return;
        if (_pendingFormatSelection is { } selection)
            EditorSurface.NativeEditor.Document.Selection.SetRange(selection.Start, selection.End);
        _pendingFormatSelection = null;
        EditorSurface.ApplyFormattingCommand(command);
        EditorSurface.FocusEditor();
    }

    private static void OnTextPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var host = (MarkdownSourceEditor)sender;
        if (host._isSynchronizingText) return;
        host._pendingFormatSelection = null;
        host.EditorSurface.Text = args.NewValue as string ?? string.Empty;
    }

    private void EditorSurface_EditorTextChanged(object? sender, EventArgs args)
    {
        if (_isSynchronizingText) return;
        string source = EditorSurface.Text;
        bool truncated = source.Length > MarkdownDocumentService.MaxCharacters && !ContainsInlineDataImage(source);
        if (truncated) { source = TruncateText(source); EditorSurface.Text = source; }
        _isSynchronizingText = true;
        try { SetValue(TextProperty, source); }
        finally { _isSynchronizingText = false; }
        EditorTextChanged?.Invoke(this, EventArgs.Empty);
        if (truncated) TextTruncated?.Invoke(this, EventArgs.Empty);
    }

    private void FormattingToolbar_PointerPressed(object sender, PointerRoutedEventArgs args)
    {
        var selection = EditorSurface.NativeEditor.Document.Selection;
        _pendingFormatSelection = (selection.StartPosition, selection.EndPosition);
    }

    private void EditorSurface_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (EditorSurface.IsComposing || EditorSurface.NativeEditor.FocusState == FocusState.Unfocused) return;
        bool control = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        bool shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
        if ((EnableCtrlSSaveShortcut && TextBoxEditorShortcutHelper.IsCtrlSaveShortcut(e.Key, control, shift)) ||
            (e.Key == VirtualKey.Enter && SettingsService.ShouldSubmitEditorOnEnter(EditorEnterBehavior, control)))
        {
            e.Handled = true;
            FlushAndGetText();
            CommitRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            FlushAndGetText();
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }
    private static bool TryResolveCommand(string action, out MarkdownEditCommand command)
    {
        command = action switch
        {
            "Bold" => MarkdownEditCommand.Bold,
            "Italic" => MarkdownEditCommand.Italic,
            "Strike" => MarkdownEditCommand.Strikethrough,
            "Code" => MarkdownEditCommand.Code,
            "Link" => MarkdownEditCommand.Link,
            "Heading" => MarkdownEditCommand.Heading,
            "List" => MarkdownEditCommand.List,
            "Task" => MarkdownEditCommand.Task,
            "Quote" => MarkdownEditCommand.Quote,
            "Table" => MarkdownEditCommand.Table,
            _ => default
        };
        return action is "Bold" or "Italic" or "Strike" or "Code" or "Link" or
            "Heading" or "List" or "Task" or "Quote" or "Table";
    }

    private static void OnToolbarVisibilityChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args) =>
        ((MarkdownSourceEditor)sender).ApplyToolbarVisibility();

    private void ApplyToolbarVisibility()
    {
        SetToolbarVisibility();
        if (EditorSurface is not null) EditorSurface.IsLivePreviewEnabled = ShowFormattingToolbar;
    }

    private void SetToolbarVisibility()
    {
        FormattingToolbar.Visibility = ShowFormattingToolbar
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (ShowFormattingToolbar)
        {
            UpdateFormattingToolbar(EditorLayoutRoot.ActualWidth);
        }
    }

    private void EditorLayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ShowFormattingToolbar)
        {
            UpdateFormattingToolbar(e.NewSize.Width);
        }
    }

    private void UpdateFormattingToolbar(double availableWidth)
    {
        Button[] toolbarButtons =
        [
            BoldButton,
            ItalicButton,
            StrikeButton,
            HeadingButton,
            ListButton,
            TaskButton,
            LinkButton,
            QuoteButton,
            CodeButton,
            TableButton
        ];
        Button[] menuButtons =
        [
            BoldMenuButton,
            ItalicMenuButton,
            StrikeMenuButton,
            HeadingMenuButton,
            ListMenuButton,
            TaskMenuButton,
            LinkMenuButton,
            QuoteMenuButton,
            CodeMenuButton,
            TableMenuButton
        ];

        int visibleCount = CalculateVisibleToolbarCommandCount(
            availableWidth,
            toolbarButtons.Length);
        for (int index = 0; index < toolbarButtons.Length; index++)
        {
            bool isInline = index < visibleCount;
            toolbarButtons[index].Visibility = isInline
                ? Visibility.Visible
                : Visibility.Collapsed;
            menuButtons[index].Visibility = isInline
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        bool hasOverflow = visibleCount < toolbarButtons.Length;
        FormattingMoreButton.Visibility = hasOverflow
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (!hasOverflow)
        {
            FormattingMoreFlyout.Hide();
        }
    }

    internal static int CalculateVisibleToolbarCommandCount(
        double availableWidth,
        int commandCount)
    {
        if (availableWidth <= 0 || commandCount <= 0)
        {
            return 0;
        }

        double fullWidth = commandCount * FormattingButtonWidth +
                           (commandCount - 1) * FormattingButtonSpacing;
        if (availableWidth >= fullWidth)
        {
            return commandCount;
        }

        double inlineWidth = availableWidth - FormattingButtonWidth;
        int visibleCount = (int)Math.Floor(
            inlineWidth / (FormattingButtonWidth + FormattingButtonSpacing));
        return Math.Clamp(visibleCount, 0, commandCount - 1);
    }

    private void FormatButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string action })
        {
            FormattingMoreFlyout.Hide();
            ApplyFormat(action);
        }
    }

    private static string TruncateText(string text)
    {
        int length = MarkdownDocumentService.MaxCharacters;
        if (char.IsHighSurrogate(text[length - 1]) && char.IsLowSurrogate(text[length]))
        {
            length--;
        }

        return text[..length];
    }

    private static bool ContainsInlineDataImage(string text) =>
        text.Contains(
            "data:image/",
            StringComparison.OrdinalIgnoreCase);

    private void ApplyLocalizedText()
    {
        SetButtonText(BoldButton, "Markdown.Editor.Bold", "Bold");
        SetButtonText(ItalicButton, "Markdown.Editor.Italic", "Italic");
        SetButtonText(HeadingButton, "Markdown.Editor.Heading", "Heading");
        SetButtonText(StrikeButton, "Markdown.Editor.Strikethrough", "Strikethrough");
        SetButtonText(ListButton, "Markdown.Editor.List", "List");
        SetButtonText(TaskButton, "Markdown.Editor.Task", "Task list");
        SetButtonText(LinkButton, "Markdown.Editor.Link", "Link");
        SetButtonText(QuoteButton, "Markdown.Editor.Quote", "Quote");
        SetButtonText(CodeButton, "Markdown.Editor.Code", "Code");
        SetButtonText(TableButton, "Markdown.Editor.Table", "Table");
        SetButtonText(FormattingMoreButton, "Common.More", "More");
        SetButtonText(BoldMenuButton, BoldMenuButtonText, "Markdown.Editor.Bold", "Bold");
        SetButtonText(ItalicMenuButton, ItalicMenuButtonText, "Markdown.Editor.Italic", "Italic");
        SetButtonText(StrikeMenuButton, StrikeMenuButtonText, "Markdown.Editor.Strikethrough", "Strikethrough");
        SetButtonText(HeadingMenuButton, HeadingMenuButtonText, "Markdown.Editor.Heading", "Heading");
        SetButtonText(ListMenuButton, ListMenuButtonText, "Markdown.Editor.List", "List");
        SetButtonText(TaskMenuButton, TaskMenuButtonText, "Markdown.Editor.Task", "Task list");
        SetButtonText(LinkMenuButton, LinkMenuButtonText, "Markdown.Editor.Link", "Link");
        SetButtonText(QuoteMenuButton, QuoteMenuButtonText, "Markdown.Editor.Quote", "Quote");
        SetButtonText(CodeMenuButton, CodeMenuButtonText, "Markdown.Editor.Code", "Code");
        SetButtonText(TableMenuButton, TableMenuButtonText, "Markdown.Editor.Table", "Table");

        AutomationProperties.SetName(
            FormattingToolbar,
            T("Markdown.Editor.Toolbar", "Formatting"));
        AutomationProperties.SetName(
            EditorSurface,
            T("Markdown.Editor.Source", "Markdown source editor"));
    }

    private void SetButtonText(Button button, string key, string fallback)
    {
        string text = T(key, fallback);
        ToolTipService.SetToolTip(button, text);
        AutomationProperties.SetName(button, text);
    }

    private void SetButtonText(
        Button button,
        TextBlock label,
        string key,
        string fallback)
    {
        string text = T(key, fallback);
        label.Text = text;
        AutomationProperties.SetName(button, text);
    }

    private string T(string key, string fallback)
    {
        if (_textResolver is null)
        {
            return fallback;
        }

        string value = _textResolver(key);
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal)
            ? fallback
            : value;
    }

}
