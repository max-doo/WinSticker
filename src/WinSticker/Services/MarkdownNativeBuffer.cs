namespace WinSticker.Services;

/// <summary>A source-only edit to apply to the native text buffer.</summary>
public sealed record MarkdownNativeEdit(int Start, int Length, string Replacement, int Caret, int SelectionLength = 0, int SelectionOptions = 0);

/// <summary>
/// Maps RichEdit's CR paragraph positions to lossless Markdown source. History
/// contains text changes only; presentation formats never become undo steps.
/// Capture happens at idle/flush, never on the synchronous keystroke path.
/// </summary>
public sealed class MarkdownNativeBuffer
{
    private sealed record Change(int NativeStart, string Removed, string Inserted,
        int SourceStart, string RemovedSource, string InsertedSource, int BeforeCaret, int AfterCaret, int BeforeLength, int AfterLength, int BeforeOptions, int AfterOptions);
    private readonly List<Change> _undo = [];
    private readonly List<Change> _redo = [];
    private string _newline = "\n";
    public string Source { get; private set; } = string.Empty;
    public string NativeText { get; private set; } = string.Empty;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Load(string source)
    {
        Source = source;
        NativeText = ToNative(source);
        int lf = source.IndexOf('\n');
        _newline = lf > 0 && source[lf - 1] == '\r' ? "\r\n" : lf >= 0 ? "\n" : source.Contains('\r') ? "\r" : "\n";
        _undo.Clear();
        _redo.Clear();
    }

    public static string ToNative(string source) => source.Replace("\r\n", "\r", StringComparison.Ordinal).Replace('\n', '\r');

    public bool Capture(string text, int beforeCaret, int afterCaret, int beforeLength = 0, int afterLength = 0, int beforeOptions = 0, int afterOptions = 0)
    {
        if (text == NativeText) return false;
        int prefix = 0;
        int limit = Math.Min(text.Length, NativeText.Length);
        while (prefix < limit && text[prefix] == NativeText[prefix]) prefix++;
        int suffix = 0;
        while (suffix < limit - prefix && text[^(suffix + 1)] == NativeText[^(suffix + 1)]) suffix++;
        // Keep surrogate pairs intact in the stored replacement.
        if (prefix > 0 && prefix < NativeText.Length && char.IsLowSurrogate(NativeText[prefix])) prefix--;
        if (suffix > 0 && suffix < NativeText.Length - prefix && char.IsLowSurrogate(NativeText[^suffix])) suffix--;
        int sourceStart = SourceOffset(prefix);
        int sourceEnd = SourceOffset(NativeText.Length - suffix);
        string inserted = text.Substring(prefix, text.Length - prefix - suffix);
        var change = new Change(prefix,
            NativeText.Substring(prefix, NativeText.Length - prefix - suffix), inserted,
            sourceStart, Source[sourceStart..sourceEnd], inserted.Replace("\r", _newline, StringComparison.Ordinal),
            beforeCaret, afterCaret, beforeLength, afterLength, beforeOptions, afterOptions);
        Source = Source[..sourceStart] + change.InsertedSource + Source[sourceEnd..];
        NativeText = text;
        _undo.Add(change);
        _redo.Clear();
        while (_undo.Count > 128 || (_undo.Count > 1 && _undo.Sum(x => x.Removed.Length + x.Inserted.Length) > 2 * 1024 * 1024))
            _undo.RemoveAt(0);
        return true;
    }

    public MarkdownNativeEdit? Undo()
    {
        if (_undo.Count == 0) return null;
        Change change = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Source = Source[..change.SourceStart] + change.RemovedSource + Source[(change.SourceStart + change.InsertedSource.Length)..];
        NativeText = NativeText[..change.NativeStart] + change.Removed + NativeText[(change.NativeStart + change.Inserted.Length)..];
        _redo.Add(change);
        return new(change.NativeStart, change.Inserted.Length, change.Removed, change.BeforeCaret, change.BeforeLength, change.BeforeOptions);
    }

    public MarkdownNativeEdit? Redo()
    {
        if (_redo.Count == 0) return null;
        Change change = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        Source = Source[..change.SourceStart] + change.InsertedSource + Source[(change.SourceStart + change.RemovedSource.Length)..];
        NativeText = NativeText[..change.NativeStart] + change.Inserted + NativeText[(change.NativeStart + change.Removed.Length)..];
        _undo.Add(change);
        return new(change.NativeStart, change.Removed.Length, change.Inserted, change.AfterCaret, change.AfterLength, change.AfterOptions);
    }

    private int SourceOffset(int nativeOffset)
    {
        int source = 0;
        for (int native = 0; native < nativeOffset && source < Source.Length; native++, source++)
            if (Source[source] == '\r' && source + 1 < Source.Length && Source[source + 1] == '\n') source++;
        return source;
    }
}
