using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using WinSticker.Models;

namespace WinSticker.Services;

/// <summary>Shares open-note sessions within one store while keeping host tab state separate.</summary>
public sealed class NoteWorkspace
{
    private static readonly ConditionalWeakTable<QuickCaptureService, NoteWorkspace> Workspaces = new();
    private readonly QuickCaptureService _service;
    private readonly Dictionary<string, NoteEditingSession> _sessions = new(StringComparer.Ordinal);
    private NoteWorkspace(QuickCaptureService service) => _service = service;
    public static NoteWorkspace For(QuickCaptureService service) => Workspaces.GetValue(service, value => new(value));
    public event EventHandler<NoteEditingSession>? NoteChanged;
    public QuickCaptureDeletedItemSnapshot? LastDeletion { get; private set; }
    public event EventHandler? ActionChanged;

    public async Task<bool> DeleteAsync(NoteEditingSession session)
    {
        var snapshot = await session.DeleteAsync();
        if (snapshot is null) return false;
        LastDeletion = snapshot;
        ActionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task<bool> UndoDeleteAsync()
    {
        if (LastDeletion is null || !await _service.RestoreDeletedItemAsync(LastDeletion)) return false;
        LastDeletion = null;
        ActionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public NoteEditingSession Acquire(
        QuickCaptureItem item,
        Func<IMarkdownSessionTimer> timerFactory,
        DispatcherQueue? dispatcher = null,
        Func<IMarkdownSessionTimer>? maxDelayTimerFactory = null)
    {
        lock (_sessions)
        {
            if (!_sessions.TryGetValue(item.Id, out var session))
            {
                session = new(_service, item, timerFactory(), dispatcher, maxDelayTimerFactory?.Invoke());
                session.Changed += SessionChanged;
                _sessions.Add(item.Id, session);
            }
            session.ViewCount++;
            return session;
        }
    }

    public void Release(NoteEditingSession session)
    {
        lock (_sessions)
        {
            session.ViewCount = Math.Max(0, session.ViewCount - 1);
            if (session.ViewCount > 0 || session.HasUnsavedChanges) return;
            _sessions.Remove(session.Id);
            session.Changed -= SessionChanged;
            session.Dispose();
        }
    }

    private void SessionChanged(object? sender, EventArgs args)
    {
        if (sender is not NoteEditingSession session) return;
        NoteChanged?.Invoke(this, session);
        lock (_sessions)
        {
            if (session.ViewCount != 0 || session.HasUnsavedChanges) return;
            _sessions.Remove(session.Id);
            session.Changed -= SessionChanged;
            session.Dispose();
        }
    }

    public static bool IsNote(QuickCaptureItem item) => !item.IsDeleted && !item.IsRecent &&
        (item.IsNote || item.Type is QuickCaptureItemType.Text or QuickCaptureItemType.Link ||
         (item.ContentFormat == TextContentFormat.Markdown && !string.IsNullOrWhiteSpace(item.Body)));

    public static string DisplayTitle(string? title, string body, string unnamed)
    {
        if (!string.IsNullOrWhiteSpace(title)) return title.Trim();
        var remaining = body.AsSpan();
        while (!remaining.IsEmpty)
        {
            int end = remaining.IndexOfAny('\r', '\n');
            var line = end < 0 ? remaining : remaining[..end];
            remaining = end < 0 ? [] : remaining[(end + 1)..];
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            string plain = new MarkdownDocumentService().ToPlainText(trimmed[..Math.Min(trimmed.Length, 160)].ToString());
            if (plain.Length > 0) return plain;
        }
        return unnamed;
    }
}
