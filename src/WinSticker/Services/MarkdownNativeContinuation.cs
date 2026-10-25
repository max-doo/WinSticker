using System.Text.RegularExpressions;

namespace WinSticker.Services;

/// <summary>Native Enter continuation as a source edit, independent of UI layout.</summary>
public static partial class MarkdownNativeContinuation
{
    public static bool TryCreate(string source, int caret, out MarkdownTextEdit edit)
    {
        edit = default;
        caret = Math.Clamp(caret, 0, source.Length);
        int start = caret == 0 ? 0 : source.LastIndexOf('\r', caret - 1) + 1;
        int end = source.IndexOf('\r', caret);
        if (end < 0) end = source.Length;
        string line = source[start..end];
        if (InsideFence(source.AsSpan(0, start)) || line.TrimStart().StartsWith("```") || line.TrimStart().StartsWith("~~~")) return false;
        Match match = ListLine().Match(line);
        string prefix;
        int contentStart;
        if (match.Success)
        {
            string marker = match.Groups["marker"].Value;
            if (int.TryParse(match.Groups["number"].Value, out int number))
                marker = Math.Min(999999999, number + 1) + match.Groups["punct"].Value;
            prefix = match.Groups["indent"].Value + marker + " " + (match.Groups["task"].Success ? "[ ] " : string.Empty);
            contentStart = match.Groups["content"].Index;
        }
        else
        {
            match = QuoteLine().Match(line);
            if (!match.Success) return false;
            prefix = match.Groups["quote"].Value;
            contentStart = match.Groups["content"].Index;
        }
        if (caret - start < contentStart) return false;
        if (string.IsNullOrWhiteSpace(match.Groups["content"].Value))
            edit = new(start, line.Length, string.Empty, start, 0);
        else
            edit = new(caret, 0, "\r" + prefix, caret + 1 + prefix.Length, 0);
        return true;
    }

    private static bool InsideFence(ReadOnlySpan<char> source)
    {
        char fence = '\0';
        int length = 0;
        while (!source.IsEmpty)
        {
            int end = source.IndexOf('\r');
            ReadOnlySpan<char> line = end < 0 ? source : source[..end];
            source = end < 0 ? [] : source[(end + 1)..];
            ReadOnlySpan<char> trimmed = line.TrimStart(' ');
            if (line.Length - trimmed.Length > 3 || trimmed.Length < 3 || trimmed[0] is not ('`' or '~')) continue;
            int count = 1;
            while (count < trimmed.Length && trimmed[count] == trimmed[0]) count++;
            if (count < 3) continue;
            if (fence == '\0') { fence = trimmed[0]; length = count; }
            else if (fence == trimmed[0] && count >= length && trimmed[count..].Trim().IsEmpty) fence = '\0';
        }
        return fence != '\0';
    }

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<marker>[-+*]|(?<number>[0-9]{1,9})(?<punct>[.)]))[ \t]+(?<task>\[[ xX]\][ \t]+)?(?<content>.*)$")]
    private static partial Regex ListLine();
    [GeneratedRegex(@"^(?<quote>(?:[ \t]*>[ \t]?)+)(?<content>.*)$")]
    private static partial Regex QuoteLine();
}
