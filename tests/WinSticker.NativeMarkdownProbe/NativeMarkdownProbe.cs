#if DEBUG
using System.Text.Json;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinSticker.Services;
using WinSticker.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

namespace WinSticker;

public partial class App
{
    private Window? _nativeMarkdownProbeWindow;

    private void StartNativeMarkdownProbeIfRequested()
    {
        if (!WinStickerDataPathService.Current.IsDevelopmentRoot ||
            Environment.GetEnvironmentVariable("WINSTICKER_NATIVE_MARKDOWN_PROBE") != "1")
        {
            return;
        }

        var editor = new RichEditBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 340 };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        var root = new Grid
        {
            Padding = new Thickness(24), RowSpacing = 16,
            RequestedTheme = ThemeService?.EffectiveTheme ?? ElementTheme.Default,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                ThemeService?.EffectiveTheme == ElementTheme.Dark ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White)
        };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(editor);
        Grid.SetRow(status, 1);
        root.Children.Add(status);
        _nativeMarkdownProbeWindow = new Window { Title = "WinSticker Native Markdown Probe", Content = root };
        _nativeMarkdownProbeWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(820, 620));
        editor.Loaded += async (_, _) =>
        {
            try
            {
                await Task.Delay(200);
                var results = new Dictionary<string, object?>();
                var doc = editor.Document;
                string Read() { doc.GetText(TextGetOptions.None, out string value); return value; }
                doc.SetText(TextSetOptions.None, "# Title\r\rText **bold**\r");
                string initial = Read();
                results["initial"] = initial;
                doc.ClearUndoRedoHistory();
                doc.GetRange(0, 2).CharacterFormat.Hidden = FormatEffect.On;
                results["hiddenPreservesSource"] = Read() == initial;
                doc.GetText(TextGetOptions.NoHidden, out string visible);
                results["visible"] = visible;
                results["formatCreatesUndo"] = doc.CanUndo();
                doc.ClearUndoRedoHistory();
                doc.Selection.SetRange(initial.Length - 1, initial.Length - 1);
                doc.Selection.SetText(TextSetOptions.None, "X");
                string typed = Read();
                doc.GetRange(2, 7).CharacterFormat.Bold = FormatEffect.On;
                doc.Undo();
                results["undoAfterFormat"] = Read();
                results["undoRemovedTyping"] = Read() == initial;
                results["typed"] = typed;
                var before = doc.GetRange(0, 2);
                var after = doc.GetRange(7, initial.Length);
                int oldStart = after.StartPosition;
                doc.Selection.SetRange(4, 4);
                doc.Selection.SetText(TextSetOptions.None, "ABC");
                results["rangeTracking"] = new { prefixEnd = before.EndPosition, oldStart, suffixStart = after.StartPosition };
                doc.SetText(TextSetOptions.None, "# Heading\r\rA **bold** and *italic* paragraph.\r\r- [ ] Task\r");
                doc.GetRange(0, 2).CharacterFormat.Hidden = FormatEffect.On;
                doc.GetRange(2, 9).CharacterFormat.Size = 24;
                doc.GetRange(2, 9).CharacterFormat.Bold = FormatEffect.On;
                doc.ClearUndoRedoHistory();
                doc.Selection.SetRange(15, 15);
                status.Text = JsonSerializer.Serialize(results);
                string path = Path.Combine(WinStickerDataPathService.Current.RootPath, "native-markdown-probe.json");
                void Stage(string stage)
                {
                    results["stage"] = stage;
                    File.WriteAllText(path, JsonSerializer.Serialize(results));
                }
                async Task CaptureEditorAsync(string name)
                {
                    var bitmap = new RenderTargetBitmap();
                    await bitmap.RenderAsync(root);
                    var pixels = await bitmap.GetPixelsAsync();
                    using var output = File.Create(Path.Combine(WinStickerDataPathService.Current.RootPath, name + ".png"));
                    using var stream = output.AsRandomAccessStream();
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                        (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
                    await encoder.FlushAsync();
                }
                async Task<(int Count, double Fps, double? P95)> MeasureScrollAsync(ScrollViewer scroller)
                {
                    var frameTimes = new List<double>();
                    long previousFrame = 0;
                    void OnFrame(object? sender, object args)
                    {
                        long now = System.Diagnostics.Stopwatch.GetTimestamp();
                        if (previousFrame != 0) frameTimes.Add(System.Diagnostics.Stopwatch.GetElapsedTime(previousFrame, now).TotalMilliseconds);
                        previousFrame = now;
                    }
                    _nativeMarkdownProbeWindow.Activate();
                    Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnFrame;
                    long started = System.Diagnostics.Stopwatch.GetTimestamp();
                    try
                    {
                        for (int frame = 0; frame < 120; frame++)
                        {
                            scroller.ChangeView(null, Math.Min(scroller.ScrollableHeight, frame * 12), null, disableAnimation: true);
                            await Task.Delay(1);
                        }
                    }
                    finally { Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnFrame; }
                    double duration = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds;
                    return (frameTimes.Count, frameTimes.Count / duration,
                        frameTimes.Count == 0 ? null : frameTimes.Order().ElementAt((int)((frameTimes.Count - 1) * 0.95)));
                }
                ScrollViewer? FindScroll(DependencyObject parent)
                {
                    for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                    {
                        var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                        if (child is ScrollViewer scroll) return scroll;
                        if (FindScroll(child) is { } nested) return nested;
                    }
                    return null;
                }
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(results));
                var live = new MarkdownLiveEditor { FontSize = 16 };
                string sample = "# 原生实时预览\n\n包含 **加粗**、*斜体* 和 `code`。\n\n- [ ] 检查任务\n- [x] 已完成任务\n\n最后一行。\n";
                root.Children.Remove(editor);
                root.Children.Add(live);
                live.Text = sample;
                await live.PresentationForProbe;
                await Task.Delay(200);
                string linkProbePath = Path.Combine(WinStickerDataPathService.Current.RootPath, "link-hint-probe.txt");
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    await File.WriteAllTextAsync(linkProbePath, $"opening {attempt}");
                    live.ShowLinkHintForProbe();
                    if (!live.IsLinkHintOpenForProbe) throw new InvalidOperationException("Link hint failed to open.");
                    await Task.Delay(200);
                    await File.WriteAllTextAsync(linkProbePath, $"closing {attempt}");
                    live.ClearLinkHintForProbe();
                    if (live.IsLinkHintOpenForProbe) throw new InvalidOperationException("Link hint failed to close.");
                    await Task.Delay(100);
                }
                await File.WriteAllTextAsync(linkProbePath, "completed");
                live.FocusEditor();
                var native = live.NativeEditorForProbe;
                native.Document.Selection.SetRange(sample.Length - 2, sample.Length - 2);
                await Task.Delay(200);
                results["liveRoundTrip"] = live.FlushAndGetText() == sample;
                results["liveHeadingMarkerHidden"] = native.Document.GetRange(0, 2).CharacterFormat.Hidden.ToString();
                results["liveHeadingBold"] = native.Document.GetRange(2, 8).CharacterFormat.Bold.ToString();
                int captures = live.SourceCaptureCount;
                results["focusSucceeded"] = native.Focus(FocusState.Programmatic);
                native.Document.Selection.SetRange(0, 0);
                await Task.Delay(100);
                native.Document.GetRange(0, 0).SetText(TextSetOptions.None, "新增 ");
                native.Document.GetText(TextGetOptions.None, out string nativeAfterInsert);
                results["nativeAfterInsert"] = nativeAfterInsert;
                results["keystrokeCaptures"] = live.SourceCaptureCount - captures;
                results["liveInsertedText"] = live.FlushAndGetText();
                results["captureMilliseconds"] = live.LastCaptureMilliseconds;
                await live.PresentationForProbe;
                await Task.Delay(200);
                live.UndoForProbe();
                results["liveUndoRestoresSource"] = live.FlushAndGetText() == sample;
                live.RedoForProbe();
                results["liveRedoRestoresSource"] = live.FlushAndGetText() == "新增 " + sample;
                live.UndoForProbe();
                native.Document.Selection.SetRange(2, 40);
                await Task.Delay(80);
                int selectedStart = native.Document.Selection.StartPosition;
                int selectedEnd = native.Document.Selection.EndPosition;
                results["crossParagraphOriginalSelection"] = new { start = selectedStart, end = selectedEnd };
                native.Document.GetRange(2, 40).SetText(TextSetOptions.None, string.Empty);
                live.FlushAndGetText();
                live.UndoForProbe();
                results["crossParagraphUndoRestoresSource"] = live.FlushAndGetText() == sample;
                results["crossParagraphRestoredSelection"] = new { start = native.Document.Selection.StartPosition, length = native.Document.Selection.Length };
                results["crossParagraphSelectionExact"] = native.Document.Selection.StartPosition == selectedStart && native.Document.Selection.EndPosition == selectedEnd;
                results["nativeFormatUndoDisabled"] = !native.Document.CanUndo();
                await live.ToggleFirstTaskAfterPrefixForProbeAsync("prefix ");
                results["taskToggleAfterPrefixExact"] = live.FlushAndGetText() == "prefix " + sample.Replace("- [ ] 检查任务", "- [x] 检查任务", StringComparison.Ordinal);
                live.UndoForProbe();
                results["taskUndoPreservesPrefix"] = live.FlushAndGetText() == "prefix " + sample;
                live.UndoForProbe();
                results["taskAndPrefixUndoRestoreSource"] = live.FlushAndGetText() == sample;
                const string complex = "# 表格和图片验收\n\n| 名称 | 状态 |\n| --- | --- |\n| 连续编辑 | **可用** |\n| 撤销重做 | 保留源码 |\n\n![附件图片](attachment:probe-image)\n\n末尾正文：在表格上下移动和选择，点击表格可展开源码。\n";
                live.AttachmentResolver = id => id == "probe-image" ? Path.Combine(AppContext.BaseDirectory, "Assets", "Store", "StoreLogo.png") : null;
                live.Text = complex;
                await live.PresentationForProbe;
                native.Document.Selection.SetRange(0, 0);
                await Task.Delay(300);
                live.RenderForProbe();
                await Task.Delay(300);
                live.RenderForProbe();
                results["complexPreviewCount"] = live.ComplexViewsForProbe.Length;
                var initialTableView = live.ComplexViewsForProbe.FirstOrDefault();
                results["complexPreviewPreservesSource"] = live.FlushAndGetText() == complex;
                int tableStart = complex.IndexOf('|');
                int imageStart = complex.IndexOf("![", StringComparison.Ordinal);
                native.Document.GetRange(imageStart, imageStart).GetRect(PointOptions.ClientCoordinates, out var imageRect, out _);
                results["complexPreviewGeometry"] = live.ComplexViewsForProbe.Select(view => new
                {
                    top = Canvas.GetTop(view), height = view.ActualHeight, width = view.ActualWidth
                }).ToArray();
                results["imageSourceTop"] = imageRect.Y;
                int followingStart = complex.IndexOf("末尾正文", StringComparison.Ordinal);
                native.Document.GetRange(followingStart, followingStart).GetRect(PointOptions.ClientCoordinates, out var followingRect, out _);
                results["followingTextTop"] = followingRect.Y;
                results["imageParagraphSpaceAfter"] = native.Document.GetRange(imageStart, imageStart + 2).ParagraphFormat.SpaceAfter;
                results["inactiveTableHidden"] = native.Document.GetRange(tableStart, tableStart + 2).CharacterFormat.Hidden.ToString();
                await CaptureEditorAsync("native-markdown-complex-preview");
                if (live.ScrollerForProbe is { } complexScroller)
                {
                    complexScroller.ChangeView(null, Math.Min(160, complexScroller.ScrollableHeight), null, disableAnimation: true);
                    await Task.Delay(200);
                    live.RenderForProbe();
                    await CaptureEditorAsync("native-markdown-complex-scrolled");
                    native.Document.GetRange(followingStart, followingStart).GetRect(PointOptions.ClientCoordinates, out var scrolledTextRect, out _);
                    results["complexScrolledTextTop"] = scrolledTextRect.Y - complexScroller.VerticalOffset;
                    results["complexScrollOffset"] = complexScroller.VerticalOffset;
                    results["complexScrolledViews"] = live.ComplexViewsForProbe.Select(view => new
                    {
                        contentTop = Canvas.GetTop(view), height = view.ActualHeight
                    }).ToArray();
                    complexScroller.ChangeView(null, 0, null, disableAnimation: true);
                    await Task.Delay(100);
                }
                long activationStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                live.ActivateComplexForProbe();
                results["tableSourceActivationMs"] = System.Diagnostics.Stopwatch.GetElapsedTime(activationStarted).TotalMilliseconds;
                results["activeTableSourceVisible"] = native.Document.GetRange(tableStart, tableStart + 2).CharacterFormat.Hidden == FormatEffect.Off;
                results["activeTableNaturalLineSpacing"] = native.Document.GetRange(tableStart, tableStart + 2).ParagraphFormat.LineSpacingRule.ToString();
                await CaptureEditorAsync("native-markdown-table-source");
                native.Document.Selection.SetRange(0, 0);
                live.RenderForProbe();
                results["tableViewReusedAfterActivation"] = ReferenceEquals(initialTableView, live.ComplexViewsForProbe.FirstOrDefault());
                live.ActivateComplexForProbe(imageStart);
                native.Document.Selection.SetRange(followingStart, followingStart);
                live.RenderForProbe();
                native.Document.GetRange(followingStart, followingStart).GetRect(PointOptions.ClientCoordinates, out var caretAfterImage, out _);
                double caretViewportY = caretAfterImage.Y - (live.ScrollerForProbe?.VerticalOffset ?? 0);
                results["caretRemainsVisibleAfterImagePreview"] = caretViewportY >= 0 && caretViewportY < native.ActualHeight;
                await CaptureEditorAsync("native-markdown-image-caret");
                native.Document.Selection.SetRange(2, imageStart + 2);
                live.RenderForProbe();
                native.Document.GetRange(2, imageStart + 2).SetText(TextSetOptions.None, string.Empty);
                live.FlushAndGetText();
                live.UndoForProbe();
                results["crossTableDeleteUndoPreservesSource"] = live.FlushAndGetText() == complex;
                native.Document.Selection.SetRange(0, 0);
                await live.PresentationForProbe;
                live.RenderForProbe();
                await Task.Delay(300);
                live.RenderForProbe();
                results["complexPreviewReappearsAfterUndo"] = live.ComplexViewsForProbe.Length;
                live.Text = sample;
                await live.PresentationForProbe;
                if (Environment.GetEnvironmentVariable("WINSTICKER_NATIVE_MARKDOWN_PERF") == "1")
                {
                    string paragraph = new string('a', 820) + " **bold** and `code`.\n\n";
                    string large = string.Concat(Enumerable.Repeat(paragraph, 300));
                    large = large[..^2].PadRight(MarkdownDocumentService.MaxCharacters, 'a');
                    results["largeInitialLength"] = large.Length;
                    long loadStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                    live.Text = large;
                    await live.PresentationForProbe;
                    results["largeLoadAndParseMs"] = System.Diagnostics.Stopwatch.GetElapsedTime(loadStarted).TotalMilliseconds;
                    await Task.Delay(200);
                    int capturesBeforeLarge = live.SourceCaptureCount;
                    double inputCallbacksBeforeLarge = live.InputCallbackMillisecondsForProbe;
                    long inputStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                    for (int i = 0; i < 1000; i++)
                        native.Document.GetRange(1000 + i, 1000 + i).SetText(TextSetOptions.None, "x");
                    results["nativeInsertion1000Ms"] = System.Diagnostics.Stopwatch.GetElapsedTime(inputStarted).TotalMilliseconds;
                    results["nativeInsertion1000SynchronousCaptures"] = live.SourceCaptureCount - capturesBeforeLarge;
                    results["inputCallbacks1000Ms"] = live.InputCallbackMillisecondsForProbe - inputCallbacksBeforeLarge;
                    live.FlushAndGetText();
                    results["largeCaptureMs"] = live.LastCaptureMilliseconds;
                    results["largeSourceLength"] = live.Text.Length;
                    results["largeExactSource"] = live.Text == large.Insert(1000, new string('x', 1000));
                    await live.PresentationForProbe;
                    await Task.Delay(200);
                    if (live.ScrollerForProbe is { } scroller)
                    {
                        var measured = await MeasureScrollAsync(scroller);
                        results["programmaticScrollFrameCount"] = measured.Count;
                        results["programmaticScrollFramesPerSecond"] = measured.Fps;
                        results["programmaticScrollP95Ms"] = measured.P95;
                    }
                    var baseline = new RichEditBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 16 };
                    baseline.Document.SetText(TextSetOptions.None, MarkdownNativeBuffer.ToNative(large));
                    root.Children.Remove(live);
                    root.Children.Add(baseline);
                    await Task.Delay(200);
                    if (FindScroll(baseline) is { } baselineScroller)
                    {
                        var measured = await MeasureScrollAsync(baselineScroller);
                        results["bareNativeScrollFramesPerSecond"] = measured.Fps;
                        results["bareNativeScrollP95Ms"] = measured.P95;
                        results["bareNativeScrollFinalOffset"] = baselineScroller.VerticalOffset;
                    }
                    root.Children.Remove(baseline);
                    root.Children.Add(live);
                    live.Text = sample;
                    await live.PresentationForProbe;
                    await Task.Delay(200);
                }
                using (var saveTimer = new DispatcherQueueSessionTimer(live.DispatcherQueue, TimeSpan.FromMilliseconds(600)))
                {
                    var session = new MarkdownEditingSession(saveTimer);
                    string savedPath = Path.Combine(WinStickerDataPathService.Current.RootPath, $"native-autosave-{Guid.NewGuid():N}.md");
                    int savedCount = 0;
                    session.SaveCore = async () =>
                    {
                        await File.WriteAllTextAsync(savedPath, live.FlushAndGetText());
                        savedCount++;
                        return true;
                    };
                    void Changed(object? sender, EventArgs args) => session.NotifyChanged();
                    live.EditorTextChanged += Changed;
                    native.Document.GetRange(0, 0).SetText(TextSetOptions.None, "saved ");
                    await Task.Delay(1200);
                    results["autosaveWritesLatestSource"] = savedCount > 0 && await File.ReadAllTextAsync(savedPath) == "saved " + sample;
                    session.Reset();
                    live.Text = sample;
                    native.Document.GetRange(0, 0).SetText(TextSetOptions.None, "instant ");
                    bool saved = await session.SaveAsync(completeEditing: true);
                    results["immediateSaveFlushesPendingSource"] = saved && await File.ReadAllTextAsync(savedPath) == "instant " + sample;
                    live.EditorTextChanged -= Changed;
                    session.Reset();
                    live.Text = sample;
                    await live.PresentationForProbe;
                }
                string entryRoot = Path.Combine(WinStickerDataPathService.Current.RootPath, "native-entry-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(entryRoot);
                var entrySettings = new SettingsService(entryRoot);
                entrySettings.Settings.QuickCaptureClipboardEnabled = false;
                var entryLocalization = new LocalizationService(entrySettings);
                var entryStore = new QuickCaptureService(new QuickCaptureStore(Path.Combine(entryRoot, "quick-capture")));
                using (var panelVm = new WinSticker.ViewModels.QuickPanelViewModel(entryStore, entrySettings, entryLocalization, dispatcherQueue: live.DispatcherQueue))
                {
                    await panelVm.LoadNotesAsync();
                    var withAttachment = await entryStore.AddAttachmentsAsync(panelVm.SelectedNote!.Id,
                        [Path.Combine(AppContext.BaseDirectory, "Assets", "Store", "Square310x310Logo.png")], copyToManagedStorage: true);
                    string entrySample = complex.Replace("attachment:probe-image", "attachment:" + withAttachment!.Attachments[0].Id, StringComparison.Ordinal);
                    await entryStore.UpdateItemDetailsAsync(panelVm.SelectedNote.Id, panelVm.NoteTitle, entrySample);
                    await panelVm.LoadNotesAsync();
                    var panelView = new QuickPanelNotesView { DataContext = panelVm };
                    panelView.DisableTabTransitionsForProbe();
                    root.Children.Remove(live);
                    root.Children.Add(panelView);
                    await Task.Delay(300);
                    var panelCore = panelView.NoteEditorForProbe;
                    await panelCore.PresentationForProbe;
                    panelCore.RenderForProbe();
                    await Task.Delay(200);
                    panelCore.RenderForProbe();
                    results["quickPanelUsesNativeEditor"] = panelCore.FlushAndGetText() == entrySample;
                    await CaptureEditorAsync("native-markdown-quick-panel");
                    string formerId = panelVm.SelectedNote!.Id;
                    panelCore.NativeEditor.Document.GetRange(0, 0).SetText(TextSetOptions.None, "pending ");
                    await panelVm.CreateNewNoteAsync();
                    await panelVm.SaveCurrentNoteAsync();
                    var entryData = await entryStore.GetDataAsync();
                    results["quickPanelSwitchFlushesFormerNote"] = entryData.Items.Single(item => item.Id == formerId).Body == "pending " + entrySample;
                    results["quickPanelNewNoteDoesNotInheritPendingText"] = panelVm.NoteBody.Length == 0 && panelCore.FlushAndGetText().Length == 0;
                    results["quickPanelNewNoteState"] = new { selected = panelVm.SelectedNote?.Id, formerId, body = panelVm.NoteBody, native = panelCore.FlushAndGetText() };
                    root.Children.Remove(panelView);
                    var config = new WinSticker.Models.WidgetConfig { WidgetKind = WinSticker.Models.WidgetKind.QuickCapture };
                    using var desktopVm = new WinSticker.ViewModels.QuickCaptureWidgetViewModel(config, entryStore, entrySettings, entryLocalization, live.DispatcherQueue);
                    using var desktopView = new WinSticker.Controls.WidgetContents.QuickCaptureSurfaceContent(desktopVm, entrySettings, entryLocalization, live.DispatcherQueue);
                    root.Children.Add(desktopView);
                    await desktopView.InitializeContentAsync();
                    await desktopView.RevealItemAsync(formerId);
                    var desktopEditor = desktopView.BeginNativeNoteEditorProbe();
                    await Task.Delay(300);
                    await desktopEditor.LiveEditorForProbe.PresentationForProbe;
                    desktopEditor.LiveEditorForProbe.RenderForProbe();
                    await Task.Delay(200);
                    desktopEditor.LiveEditorForProbe.RenderForProbe();
                    results["desktopNoteUsesNativeEditor"] = desktopEditor.FlushAndGetText() == "pending " + entrySample;
                    await CaptureEditorAsync("native-markdown-desktop-note");
                    desktopEditor.LiveEditorForProbe.NativeEditor.Document.GetRange(0, 0).SetText(TextSetOptions.None, "desktop ");
                    bool desktopSaved = await desktopView.SaveNativeNoteEditorProbeAsync();
                    entryData = await entryStore.GetDataAsync();
                    results["desktopImmediateSaveFlushesNativeInput"] = desktopSaved && entryData.Items.Single(item => item.Id == formerId).Body == "desktop pending " + entrySample;
                    desktopEditor = desktopView.BeginNativeNoteEditorProbe();
                    desktopEditor.LiveEditorForProbe.NativeEditor.Document.GetRange(0, 0).SetText(TextSetOptions.None, "second ");
                    bool injectedDuringSave = false;
                    void InjectDuringSave()
                    {
                        if (injectedDuringSave) return;
                        injectedDuringSave = true;
                        desktopEditor.LiveEditorForProbe.NativeEditor.Document.GetRange(0, 0).SetText(TextSetOptions.None, "during ");
                    }
                    entryStore.Changed += InjectDuringSave;
                    Stage("desktop-save-with-new-input");
                    try { await desktopView.SaveNativeNoteEditorProbeAsync(); }
                    finally { entryStore.Changed -= InjectDuringSave; }
                    entryData = await entryStore.GetDataAsync();
                    results["desktopInputDuringSaveIsPreserved"] = injectedDuringSave && entryData.Items.Single(item => item.Id == formerId).Body == "during second desktop pending " + entrySample;
                    Stage("select-shared-note");
                    // Keep both actual host controls loaded against the same store and Id.
                    await panelVm.SelectNoteAsync(panelVm.AllNotes.Single(note => note.Id == formerId));
                    Stage("attach-both-hosts");
                    root.Children.Remove(desktopView);
                    var pair = new Grid { ColumnSpacing = 8 };
                    pair.ColumnDefinitions.Add(new ColumnDefinition());
                    pair.ColumnDefinitions.Add(new ColumnDefinition());
                    pair.Children.Add(panelView);
                    Grid.SetColumn(desktopView, 1);
                    pair.Children.Add(desktopView);
                    root.Children.Add(pair);
                    Stage("resize-both-hosts");
                    _nativeMarkdownProbeWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(1240, 760));
                    await Task.Delay(300);
                    desktopView.OnHostViewportSizeChanged((root.ActualWidth - root.Padding.Left - root.Padding.Right - 8) / 2, root.ActualHeight - 100);
                    var quickPane = panelView.NotePaneForProbe;
                    Stage("shared-metadata");
                    var desktopPane = desktopView.NotePaneForProbe;
                    results["entryHostsShareSession"] = ReferenceEquals(quickPane.Session, desktopPane.Session);
                    results["selectedTabMatchesEditor"] = panelView.TabSelectionMatchesEditorForProbe;
                    quickPane.SetTitleForProbe("统一便签 · 项目记录");
                    quickPane.SetColorForProbe(Models.QuickCaptureAppearancePreset.Mint);
                    await quickPane.FlushAsync();
                    results["titleAndColorPropagateToDesktop"] = desktopPane.Session?.Title == "统一便签 · 项目记录" && desktopPane.Session?.Appearance == Models.QuickCaptureAppearancePreset.Mint;
                    desktopPane.SetTitleForProbe("统一便签 · 双向同步");
                    desktopPane.FocusBody();
                    desktopPane.MarkdownEditor.LiveEditorForProbe.NativeEditor.Document.GetRange(0, 0).SetText(TextSetOptions.None, "双向 ");
                    await desktopPane.FlushAsync();
                    results["desktopBodyAndTitlePropagateToQuickPanel"] = quickPane.Session?.Title == "统一便签 · 双向同步" && panelView.NoteEditorForProbe.FlushAndGetText().StartsWith("双向 ", StringComparison.Ordinal);
                    var sharedSession = desktopPane.Session!;
                    var workspace = NoteWorkspace.For(entryStore);
                    await sharedSession.TogglePinnedAsync();
                    results["favoriteSharedByBothHosts"] = quickPane.Session!.IsPinned && desktopPane.Session!.IsPinned;
                    string fileFixture = Path.Combine(entryRoot, "共享附件.txt");
                    await File.WriteAllTextAsync(fileFixture, "owned note attachment fixture");
                    await sharedSession.AddAttachmentsAsync([fileFixture], true);
                    results["desktopAttachmentAppearsInQuickPanel"] = quickPane.Session!.Attachments.Count == 2;
                    var addedFile = sharedSession.Attachments.Single(file => file.DisplayName == "共享附件.txt");
                    await quickPane.Session.RemoveAttachmentAsync(addedFile.Id);
                    results["quickPanelAttachmentRemovalAppearsInDesktop"] = desktopPane.Session!.Attachments.Count == 1;
                    await panelVm.CloseNoteAsync(panelVm.SelectedNote);
                    results["closeTabLeavesDesktopNoteVisible"] = desktopPane.Session?.Id == formerId && !desktopPane.Session.IsDeleted && desktopPane.MarkdownEditor.FlushAndGetText().StartsWith("双向 ", StringComparison.Ordinal);
                    await panelVm.SelectNoteAsync(panelVm.AllNotes.Single(note => note.Id == formerId));
                    results["reopenedTabRetainsTitleAndColor"] = quickPane.Session?.Title == "统一便签 · 双向同步" && quickPane.Session.Appearance == Models.QuickCaptureAppearancePreset.Mint;
                    foreach (var theme in new[] { ElementTheme.Dark, ElementTheme.Light })
                    {
                        root.RequestedTheme = theme;
                        root.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(theme == ElementTheme.Dark ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White);
                        await Task.Delay(300);
                        panelView.NoteEditorForProbe.RenderForProbe();
                        desktopPane.MarkdownEditor.LiveEditorForProbe.RenderForProbe();
                        await Task.Delay(200);
                        results["paperColor" + theme] = new { quick = quickPane.SurfaceColorForProbe, desktop = desktopPane.SurfaceColorForProbe };
                        results["foregrounds" + theme] = new { quick = panelView.NoteEditorForProbe.ForegroundsForProbe, desktop = desktopPane.MarkdownEditor.LiveEditorForProbe.ForegroundsForProbe };
                        await CaptureEditorAsync("notes-unified-pair-" + theme.ToString().ToLowerInvariant());
                    }
                    root.Children.Remove(pair);
                    pair.Children.Remove(panelView);
                    pair.Children.Remove(desktopView);
                    root.Children.Add(panelView);
                    _nativeMarkdownProbeWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(400, 700));
                    foreach (var theme in new[] { ElementTheme.Dark, ElementTheme.Light })
                    {
                        root.RequestedTheme = theme;
                        root.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(theme == ElementTheme.Dark ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White);
                        await Task.Delay(300);
                        panelView.NoteEditorForProbe.RenderForProbe();
                        await Task.Delay(200);
                        await CaptureEditorAsync("notes-unified-quick-narrow-" + theme.ToString().ToLowerInvariant());
                    }
                    root.Children.Remove(panelView);
                    root.Children.Add(desktopView);
                    desktopView.OnHostViewportSizeChanged(root.ActualWidth - root.Padding.Left - root.Padding.Right, root.ActualHeight - 100);
                    await desktopView.RevealItemAsync(formerId);
                    await Task.Delay(300);
                    desktopPane.MarkdownEditor.LiveEditorForProbe.RenderForProbe();
                    await CaptureEditorAsync("notes-unified-desktop-narrow-light");
                    bool deleted = await workspace.DeleteAsync(sharedSession);
                    results["deleteOffersSharedUndo"] = deleted && desktopPane.UndoVisibleForProbe;
                    bool restored = await workspace.UndoDeleteAsync();
                    await panelVm.LoadNotesAsync();
                    await desktopView.RevealItemAsync(formerId);
                    results["deleteUndoRestoresTitleBodyColorAndAttachment"] = restored && panelVm.AllNotes.Single(note => note.Id == formerId) is { Title: "统一便签 · 双向同步", Appearance: Models.QuickCaptureAppearancePreset.Mint } restoredNote && restoredNote.Body.StartsWith("双向 ", StringComparison.Ordinal) && restoredNote.Attachments.Count == 1;
                    Stage("unified-host-checks-finished");
                    if (Environment.GetEnvironmentVariable("WINSTICKER_NOTE_INTERACTION_PROBE") == "1")
                    {
                        root.Children.Remove(desktopView);
                        await panelVm.SelectNoteAsync(panelVm.AllNotes.Single(note => note.Id == formerId));
                        root.Children.Add(panelView);
                        _nativeMarkdownProbeWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(820, 700));
                        await Task.Delay(300);
                        status.Text = "共享便签实际按键验收：F6 保存截图及输入状态，F9 返回内核验收。";
                        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        async void InteractiveKey(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
                        {
                            if (args.Key == Windows.System.VirtualKey.F9) { args.Handled = true; finish.TrySetResult(); }
                            if (args.Key != Windows.System.VirtualKey.F6) return;
                            args.Handled = true;
                            await panelView.NotePaneForProbe.FlushAsync();
                            var source = panelView.NoteEditorForProbe;
                            results["interaction"] = new { source = source.FlushAndGetText(), title = panelView.NotePaneForProbe.Session?.Title,
                                focusedType = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root.XamlRoot)?.GetType().Name,
                                start = source.NativeEditor.Document.Selection.StartPosition, end = source.NativeEditor.Document.Selection.EndPosition };
                            Stage("interactive-note");
                            await CaptureEditorAsync("notes-unified-keyboard");
                        }
                        root.AddHandler(UIElement.KeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(InteractiveKey), true);
                        panelView.NotePaneForProbe.FocusBody();
                        Stage("interactive-ready");
                        await finish.Task;
                        root.RemoveHandler(UIElement.KeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(InteractiveKey));
                        root.Children.Remove(panelView);
                        root.Children.Add(desktopView);
                    }
                    root.Children.Remove(desktopView);
                    root.Children.Add(live);
                    root.RequestedTheme = ThemeService?.EffectiveTheme ?? ElementTheme.Default;
                    _nativeMarkdownProbeWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(820, 620));
                }
                live.Text = complex;
                await live.PresentationForProbe;
                native.Document.Selection.SetRange(0, 0);
                await Task.Delay(300);
                live.RenderForProbe();
                status.Text = "表格/图片验收：点击表格展开源码，移出后恢复预览；Ctrl+Z/Y 撤销/重做；F6 记录状态。";
                async Task Snapshot()
                {
                    results["source"] = live.FlushAndGetText();
                    results["captureCount"] = live.SourceCaptureCount;
                    results["captureMilliseconds"] = live.LastCaptureMilliseconds;
                    native.Document.GetText(TextGetOptions.NoHidden, out string shown);
                    results["visibleText"] = shown;
                    native.Document.GetText(TextGetOptions.None, out string raw);
                    results["nativeText"] = raw;
                    results["nativeFocusState"] = native.FocusState.ToString();
                    results["nativeReadOnly"] = native.IsReadOnly;
                    results["focusedType"] = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(live.XamlRoot)?.GetType().Name;
                    results["nativeSelectionStart"] = native.Document.Selection.StartPosition;
                    results["complexPreviewCountNow"] = live.ComplexViewsForProbe.Length;
                    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(results));
                    await CaptureEditorAsync("native-markdown-keyboard");
                }
                root.KeyDown += async (_, args) =>
                {
                    if (args.Key == Windows.System.VirtualKey.F6) { args.Handled = true; await Snapshot(); }
                };
                await Snapshot();
                await Task.Delay(1500);
                _nativeMarkdownProbeWindow.AppWindow.Show();
                _nativeMarkdownProbeWindow.Activate();
                native.Focus(FocusState.Programmatic);
            }
            catch (Exception error)
            {
                status.Text = error.ToString();
                await File.WriteAllTextAsync(Path.Combine(WinStickerDataPathService.Current.RootPath, "native-markdown-probe-error.txt"), error.ToString());
            }
        };
        _nativeMarkdownProbeWindow.Activate();
    }
}
#endif
