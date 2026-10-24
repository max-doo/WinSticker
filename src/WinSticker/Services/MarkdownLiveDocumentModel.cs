using System.Text;

namespace WinSticker.Services;

/// <summary>
/// Pure document model behind the live Markdown editor. The document is a
/// ordered list of Markdown blocks separated by blank lines (code fences
/// keep their interior blank lines). Exactly one block at a time is active
/// and is shown as editable source; every other block is rendered. Ported
/// from the WorkCat MarkdownEditor paragraph model, adapted from per-line
/// WPF FlowDocument paragraphs to markdown blocks.
/// </summary>
public sealed class MarkdownLiveDocumentModel
{
    private readonly MarkdownDocumentService _markdownService = new();
    private List<string> _blocks = [string.Empty];

    public IReadOnlyList<string> Blocks => _blocks;

    /// <summary>Index of the block currently edited as source; -1 when none.</summary>
    public int ActiveIndex { get; private set; } = -1;

    public event EventHandler? DocumentChanged;

    public void Load(string markdown)
    {
        _blocks = SplitIntoBlocks(markdown);
        if (_blocks.Count == 0)
        {
            _blocks = [string.Empty];
        }

        ActiveIndex = -1;
    }

    public string Serialize() => string.Join("\n\n", _blocks);

    public void SetActive(int index)
    {
        ActiveIndex = index >= 0 && index < _blocks.Count ? index : -1;
    }

    public void Deactivate() => ActiveIndex = -1;

    /// <summary>
    /// Replace the active block's source. A blank line inside the new source
    /// splits it into several blocks (the caret stays on the block returned),
    /// and removing the separating blank line merges blocks back together.
    /// </summary>
    public int UpdateActiveSource(string source)
    {
        int index = ActiveIndex;
        if (index < 0 || index >= _blocks.Count)
        {
            return -1;
        }

        List<string> parts = SplitIntoBlocks(source);
        if (parts.Count == 0)
        {
            parts = [string.Empty];
        }

        _blocks.RemoveAt(index);
        _blocks.InsertRange(index, parts);
        ActiveIndex = index + parts.Count - 1;
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        return ActiveIndex;
    }

    /// <summary>
    /// Toggle the task-list checkbox at <paramref name="taskIndex"/> inside
    /// block <paramref name="blockIndex"/> directly from its rendered state.
    /// </summary>
    public bool TryToggleTask(int blockIndex, int taskIndex)
    {
        if (blockIndex < 0 || blockIndex >= _blocks.Count)
        {
            return false;
        }

        if (!_markdownService.TryToggleTask(_blocks[blockIndex], taskIndex, out string updated))
        {
            return false;
        }

        _blocks[blockIndex] = updated;
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Split on blank lines, but never inside a fenced code block: a blank
    /// line between ``` fences belongs to the code block, not the separator.
    /// </summary>
    internal static List<string> SplitIntoBlocks(string markdown)
    {
        var blocks = new List<string>();
        StringBuilder current = new();
        bool inFence = false;
        bool sawContent = false;
        foreach (string rawLine in (markdown ?? string.Empty)
                     .Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Replace('\r', '\n')
                     .Split('\n'))
        {
            string line = rawLine.TrimEnd();
            bool fenceLine = line.Length >= 3 &&
                             line.TrimStart().StartsWith("```", StringComparison.Ordinal);
            bool isBlank = line.Length == 0;
            if (!inFence && isBlank)
            {
                if (sawContent)
                {
                    blocks.Add(current.ToString());
                    current.Clear();
                    sawContent = false;
                }

                continue;
            }

            if (current.Length > 0)
            {
                current.Append('\n');
            }

            current.Append(line);
            if (line.Length > 0)
            {
                sawContent = true;
            }

            if (fenceLine)
            {
                inFence = !inFence;
            }
        }

        if (sawContent)
        {
            blocks.Add(current.ToString());
        }

        return blocks;
    }
}
