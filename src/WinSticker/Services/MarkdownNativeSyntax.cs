using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Tables;
using System.Text.RegularExpressions;

namespace WinSticker.Services;

/// <summary>Presentation attributes applied without changing Markdown text.</summary>
[Flags]
public enum MarkdownNativeStyle { None = 0, Bold = 1, Italic = 2, Strike = 4, Code = 8, Link = 16, Hidden = 32, Quote = 64 }

/// <summary>A presentation range in native CR-based character positions.</summary>
public sealed record MarkdownNativeSpan(int Start, int Length, MarkdownNativeStyle Style, int HeadingLevel = 0);

/// <summary>A task's source marker and paragraph, used for native checkbox overlays.</summary>
public sealed record MarkdownNativeTask(int Start, int LineStart, int LineEnd, bool IsChecked);

/// <summary>A complex block displayed by the existing native Markdown reader.</summary>
public sealed record MarkdownNativeEmbeddedBlock(int Start, int Length, string Source);
public sealed record MarkdownNativeLink(int Start, int Length, string Url);

/// <summary>Immutable presentation snapshot, safe to compute away from the UI thread.</summary>
public sealed record MarkdownNativeSyntax(IReadOnlyList<MarkdownNativeSpan> Spans, IReadOnlyList<MarkdownNativeTask> Tasks)
{
    private static readonly Regex WebLink = new(@"https?://[^\s<>\u3000-\u303f\uff00-\uff65]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public IReadOnlyList<MarkdownNativeEmbeddedBlock> EmbeddedBlocks { get; init; } = [];
    public IReadOnlyList<MarkdownNativeLink> Links { get; init; } = [];

    public static MarkdownNativeSyntax Parse(string nativeText)
    {
        string source = nativeText.Replace('\r', '\n');
        var parsed = new MarkdownDocumentService().Parse(source);
        var spans = new List<MarkdownNativeSpan>();
        var tasks = new List<MarkdownNativeTask>();
        var embedded = new List<MarkdownNativeEmbeddedBlock>();
        var links = new List<MarkdownNativeLink>();
        void Add(int start, int length, MarkdownNativeStyle style, int heading = 0)
        {
            if (start >= 0 && length > 0 && start + length <= source.Length)
                spans.Add(new(start, length, style, heading));
        }
        void Hide(int start, int length) => Add(start, length, MarkdownNativeStyle.Hidden);
        void AddWebLinks(int start, int length)
        {
            if (start < 0 || length <= 0 || start + length > source.Length) return;
            foreach (Match match in WebLink.Matches(source.Substring(start, length)))
            {
                string url = match.Value.TrimEnd('.', ',', '!', '?', ';', ':');
                if (!MarkdownDocumentService.IsRemoteImage(url)) continue;
                links.Add(new(start + match.Index, url.Length, url));
                Add(start + match.Index, url.Length, MarkdownNativeStyle.Link);
            }
        }
        foreach (Block block in parsed.Document.Descendants<Block>())
        {
            bool standaloneImage = block is ParagraphBlock { Inline: { FirstChild: LinkInline { IsImage: true } image } } && image.NextSibling is null;
            bool insideTable = false;
            for (var parent = block.Parent; parent is not null; parent = parent.Parent)
                if (parent is Table) { insideTable = true; break; }
            if (block is Table || (standaloneImage && !insideTable))
            {
                int start = block.Span.Start == 0 ? 0 : source.LastIndexOf('\n', block.Span.Start - 1) + 1;
                int end = source.IndexOf('\n', Math.Min(source.Length, block.Span.End));
                if (end < 0) end = source.Length;
                embedded.Add(new(start, end - start, source[start..end]));
            }
            if (block is HeadingBlock heading)
            {
                Add(block.Span.Start, block.Span.Length, MarkdownNativeStyle.Bold, heading.Level);
                int cursor = block.Span.Start;
                while (cursor < source.Length && source[cursor] == '#') cursor++;
                if (cursor > block.Span.Start && cursor < source.Length && source[cursor] == ' ')
                    Hide(block.Span.Start, cursor + 1 - block.Span.Start);
                if (heading.IsSetext)
                {
                    int marker = source.IndexOf('\n', heading.Span.Start) + 1;
                    int markerEnd = source.IndexOf('\n', marker);
                    if (markerEnd < 0) markerEnd = source.Length;
                    Hide(marker, markerEnd - marker);
                }
                else
                {
                    int lineEnd = source.IndexOf('\n', heading.Span.Start);
                    if (lineEnd < 0) lineEnd = source.Length;
                    int tail = lineEnd - 1;
                    while (tail >= cursor && char.IsWhiteSpace(source[tail])) tail--;
                    int hashes = tail;
                    while (hashes >= cursor && source[hashes] == '#') hashes--;
                    if (hashes < tail && hashes >= cursor && char.IsWhiteSpace(source[hashes]))
                        Hide(hashes, lineEnd - hashes);
                }
            }
            if (block is QuoteBlock)
            {
                int line = block.Span.Start == 0 ? 0 : source.LastIndexOf('\n', block.Span.Start - 1) + 1;
                while (line <= block.Span.End)
                {
                    int end = source.IndexOf('\n', line);
                    if (end < 0 || end > block.Span.End) end = block.Span.End + 1;
                    int marker = line;
                    while (marker < end && source[marker] == ' ') marker++;
                    if (marker < end && source[marker] == '>')
                    {
                        int after = marker + 1;
                        if (after < end && source[after] == ' ') after++;
                        Hide(marker, after - marker);
                    }
                    Add(line, end - line, MarkdownNativeStyle.Quote);
                    line = end + 1;
                }
            }
            if (block is CodeBlock or Table)
                Add(block.Span.Start, block.Span.Length, MarkdownNativeStyle.Code);
            if (block is FencedCodeBlock fence)
            {
                int start = block.Span.Start;
                int firstEnd = source.IndexOf('\n', start);
                if (firstEnd > start) Hide(start, firstEnd - start);
                int lastStart = source.LastIndexOf('\n', Math.Min(source.Length - 1, block.Span.End)) + 1;
                if (lastStart > start && fence.ClosingFencedCharCount > 0)
                {
                    int marker = lastStart;
                    while (marker <= block.Span.End && source[marker] != fence.FencedChar) marker++;
                    Hide(marker, block.Span.End + 1 - marker);
                }
            }
        }
        foreach (Inline inline in parsed.Document.Descendants<Inline>())
        {
            if (inline is EmphasisInline emphasis)
            {
                MarkdownNativeStyle style = emphasis.DelimiterChar == '~' ? MarkdownNativeStyle.Strike :
                    emphasis.DelimiterCount >= 2 ? MarkdownNativeStyle.Bold : MarkdownNativeStyle.Italic;
                Add(inline.Span.Start, inline.Span.Length, style);
                Hide(inline.Span.Start, emphasis.DelimiterCount);
                Hide(inline.Span.End + 1 - emphasis.DelimiterCount, emphasis.DelimiterCount);
            }
            else if (inline is CodeInline code)
            {
                Add(inline.Span.Start, inline.Span.Length, MarkdownNativeStyle.Code);
                Hide(inline.Span.Start, code.DelimiterCount);
                Hide(inline.Span.End + 1 - code.DelimiterCount, code.DelimiterCount);
            }
            else if (inline is LinkInline { IsAutoLink: true, IsImage: false } autoLink && MarkdownDocumentService.IsRemoteImage(autoLink.Url))
            {
                AddWebLinks(inline.Span.Start, inline.Span.Length);
            }
            else if (inline is AutolinkInline auto && MarkdownDocumentService.IsRemoteImage(auto.Url))
            {
                int start = inline.Span.Start + 1;
                int length = inline.Span.Length - 2;
                links.Add(new(start, length, auto.Url));
                Add(start, length, MarkdownNativeStyle.Link);
            }
            else if (inline is LinkInline link && !link.IsAutoLink && link.FirstChild is { } first && link.LastChild is { } last)
            {
                Add(first.Span.Start, last.Span.End + 1 - first.Span.Start, MarkdownNativeStyle.Link);
                Hide(inline.Span.Start, first.Span.Start - inline.Span.Start);
                Hide(last.Span.End + 1, inline.Span.End - last.Span.End);
            }
            else if (inline is LiteralInline && inline.Parent is not LinkInline)
            {
                AddWebLinks(inline.Span.Start, inline.Span.Length);
            }
            else if (inline is TaskList task)
            {
                int start = inline.Span.Start;
                int lineStart = start == 0 ? 0 : source.LastIndexOf('\n', start - 1) + 1;
                int lineEnd = source.IndexOf('\n', start);
                if (lineEnd < 0) lineEnd = source.Length;
                tasks.Add(new(start, lineStart, lineEnd, task.Checked));
                Hide(lineStart, start + 3 - lineStart);
                if (task.Checked) Add(start + 3, lineEnd - start - 3, MarkdownNativeStyle.Strike);
            }
        }
        return new(spans.OrderBy(s => s.Start).ThenByDescending(s => s.Length).ToArray(), tasks) { EmbeddedBlocks = embedded, Links = links };
    }
}
