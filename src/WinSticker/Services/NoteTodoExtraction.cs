using System.Text.RegularExpressions;
using WinSticker.Models;

namespace WinSticker.Services;

/// <summary>Shared task preview and duplicate-safe import for note hosts.</summary>
public static partial class NoteTodoExtraction
{
    public static string[] Candidates(string body)
    {
        var titles = new List<string>();
        foreach (string line in body.Replace('\r', '\n').Split('\n'))
        {
            var match = TaskLine().Match(line);
            if (!match.Success || match.Groups["done"].Success) continue;
            string title = match.Groups["title"].Value.Trim();
            if (title.Length > 0) titles.Add(title[..Math.Min(200, title.Length)]);
        }
        return titles.Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public static async Task<int> AddAsync(TodoRepository repository, IEnumerable<string> titles)
    {
        string[] chosen = titles.Where(title => !string.IsNullOrWhiteSpace(title)).Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
        int added = 0;
        await repository.MutateAsync(data =>
        {
            var existing = data.Items.Select(item => item.Text.Trim()).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            foreach (string title in chosen)
            {
                if (!existing.Add(title.Trim())) continue;
                data.Items.Insert(0, new TodoItem { Text = title, DueDate = DateTimeOffset.Now,
                    SortOrder = data.Items.Count, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
                added++;
            }
        });
        return added;
    }

    [GeneratedRegex(@"^\s*(?:[-*+]\s+(?:\[(?:(?<done>[xX])| )\]\s+)?|\d+[.)]\s+)(?<title>.+)$")]
    private static partial Regex TaskLine();
}
