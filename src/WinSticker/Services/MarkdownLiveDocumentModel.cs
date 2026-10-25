using System.Text;

namespace WinSticker.Services;

/// <summary>
/// Lossless Markdown fragments with one stable editing transaction. Typing
/// replaces only the active fragment; parsing is deferred until it commits.
/// Separators belong to the document and are never reconstructed or trimmed.
/// </summary>
public sealed class MarkdownLiveDocumentModel
{
    private readonly MarkdownDocumentService _markdownService = new();
    private List<string> _blocks = [string.Empty];
    private List<string> _gaps = [string.Empty, string.Empty];

    public IReadOnlyList<string> Blocks => _blocks;

    /// <summary>Index of the source fragment being edited, or -1.</summary>
    public int ActiveIndex { get; private set; } = -1;

    public event EventHandler? DocumentChanged;

    public void Load(string markdown)
    {
        (_blocks, _gaps) = ParseFragments(markdown ?? string.Empty);
        ActiveIndex = -1;
    }

    public string Serialize()
    {
        var result = new StringBuilder();
        for (int index = 0; index < _blocks.Count; index++)
        {
            result.Append(_gaps[index]);
            result.Append(_blocks[index]);
        }

        return result.Append(_gaps[^1]).ToString();
    }

    public void SetActive(int index)
    {
        int previous = ActiveIndex;
        int delta = CommitActiveSource();
        if (previous >= 0 && index > previous)
        {
            index += delta;
        }

        ActiveIndex = index >= 0 && index < _blocks.Count ? index : -1;
    }

    public void Deactivate()
    {
        CommitActiveSource();
        ActiveIndex = -1;
    }

    /// <summary>
    /// Update the stable active fragment without parsing or changing its index.
    /// The native editor still owns that entire range, including new blank lines.
    /// Splitting here would duplicate it on the next keystroke.
    /// </summary>
    public int UpdateActiveSource(string source)
    {
        int index = ActiveIndex;
        if (index < 0 || index >= _blocks.Count)
        {
            return -1;
        }

        if (!string.Equals(_blocks[index], source, StringComparison.Ordinal))
        {
            _blocks[index] = source;
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        return index;
    }

    // Commit only the active range; return its fragment-count delta.
    private int CommitActiveSource()
    {
        int index = ActiveIndex;
        if (index < 0 || index >= _blocks.Count)
        {
            return 0;
        }

        (List<string> parts, List<string> gaps) = ParseFragments(_blocks[index]);
        gaps[0] = _gaps[index] + gaps[0];
        gaps[^1] += _gaps[index + 1];
        _blocks.RemoveAt(index);
        _blocks.InsertRange(index, parts);
        _gaps.RemoveRange(index, 2);
        _gaps.InsertRange(index, gaps);
        return parts.Count - 1;
    }

    /// <summary>Toggle a rendered task directly in its source fragment.</summary>
    public bool TryToggleTask(int blockIndex, int taskIndex)
    {
        if (blockIndex < 0 || blockIndex >= _blocks.Count ||
            !_markdownService.TryToggleTask(_blocks[blockIndex], taskIndex, out string updated))
        {
            return false;
        }

        _blocks[blockIndex] = updated;
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private (List<string> Blocks, List<string> Gaps) ParseFragments(string source)
    {
        // Syntax spans keep tilde fences, nested lists and tables intact. Keep
        // the unparsed tail beyond the renderer's size limit rather than lose it.
        var parsed = _markdownService.Parse(source);
        var blocks = new List<string>();
        var gaps = new List<string>();
        int end = 0;
        foreach (var block in parsed.Document)
        {
            int start = Math.Clamp(block.Span.Start, end, source.Length);
            int next = Math.Clamp(block.Span.End + 1, start, source.Length);
            if (next == start)
            {
                continue;
            }

            gaps.Add(source[end..start]);
            blocks.Add(source[start..next]);
            end = next;
        }

        if (blocks.Count == 0)
        {
            return ([source], [string.Empty, string.Empty]);
        }

        if (parsed.WasTruncated)
        {
            blocks[^1] += source[end..];
            gaps.Add(string.Empty);
        }
        else
        {
            gaps.Add(source[end..]);
        }

        return (blocks, gaps);
    }
}
