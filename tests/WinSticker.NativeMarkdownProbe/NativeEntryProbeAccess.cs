#if DEBUG
namespace WinSticker.Controls
{
    public sealed partial class MarkdownDocumentView
    {
        internal string TableColorsForProbe
        {
            get
            {
                var colors = new List<string>();
                var pending = new Stack<Microsoft.UI.Xaml.DependencyObject>();
                pending.Push(_contentRoot);
                while (pending.TryPop(out var item))
                {
                    if (item is Microsoft.UI.Xaml.Controls.RichTextBlock text && !ReferenceEquals(text, _documentText))
                        colors.Add((text.Foreground as Microsoft.UI.Xaml.Media.SolidColorBrush)?.Color.ToString() ?? "none");
                    for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(item); i++)
                        pending.Push(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(item, i));
                }
                return string.Join(',', colors);
            }
        }
    }
    public sealed partial class QuickPanelNotesView
    {
        internal MarkdownLiveEditor NoteEditorForProbe => NotePane.MarkdownEditor.LiveEditorForProbe;
        internal NoteEditorPane NotePaneForProbe => NotePane;
        internal bool TabSelectionMatchesEditorForProbe => ReferenceEquals(NoteTabs.SelectedItem, ViewModel?.SelectedNote);
        internal void DisableTabTransitionsForProbe() => NoteTabs.ItemContainerTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection();
    }

    public sealed partial class NoteEditorPane
    {
        internal void SetTitleForProbe(string title) { TitleEditor.Focus(Microsoft.UI.Xaml.FocusState.Programmatic); TitleEditor.Text = title; FlushNative(); }
        internal void SetColorForProbe(Models.QuickCaptureAppearancePreset color) { FlushNative(); Session?.SetAppearance(color); }
        internal bool UndoVisibleForProbe => ActionNotice.IsOpen && UndoDeleteButton.Visibility == Microsoft.UI.Xaml.Visibility.Visible;
        internal string SurfaceColorForProbe => NoteSurface.Background is Microsoft.UI.Xaml.Media.SolidColorBrush brush ? brush.Color.ToString() : "none";
        internal void ExtractForProbe() => ExtractTasksItem_Click(this, new Microsoft.UI.Xaml.RoutedEventArgs());
    }
}

namespace WinSticker.Controls.WidgetContents
{
    public sealed partial class QuickCaptureSurfaceContent
    {
        internal NoteEditorPane NotePaneForProbe => UnifiedNoteEditor;
        internal MarkdownSourceEditor BeginNativeNoteEditorProbe()
        {
            BeginDetailEditing();
            return UsesUnifiedNote ? UnifiedNoteEditor.MarkdownEditor : DetailMarkdownEditor;
        }

        internal Task<bool> SaveNativeNoteEditorProbeAsync() => SaveDetailAsync(completeEditing: true);
    }
}
#endif
