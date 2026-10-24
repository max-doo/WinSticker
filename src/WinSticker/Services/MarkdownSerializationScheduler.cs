namespace WinSticker.Services;

/// <summary>
/// Debounces Markdown serialization off the keystroke path. Typing calls
/// <see cref="NotifyChanged"/> which only (re)arms a one-shot idle timer;
/// the expensive <c>Serialize()</c> + publish runs when the timer fires, or
/// immediately through <see cref="Flush"/> at commit/sync points (block
/// deactivation, external text assignment, autosave) so consumers never
/// read a stale document.
/// </summary>
public sealed class MarkdownSerializationScheduler : IDisposable
{
    private readonly IMarkdownSessionTimer _idleTimer;
    private readonly Func<string> _serialize;
    private readonly Action<string> _publish;
    private bool _hasPending;

    public MarkdownSerializationScheduler(
        IMarkdownSessionTimer idleTimer,
        Func<string> serialize,
        Action<string> publish)
    {
        _idleTimer = idleTimer;
        _serialize = serialize;
        _publish = publish;
        _idleTimer.Tick += IdleTimer_Tick;
    }

    /// <summary>A change arrived; serialize after the idle window.</summary>
    public bool HasPending => _hasPending;

    public void NotifyChanged()
    {
        _hasPending = true;
        _idleTimer.Stop();
        _idleTimer.Start();
    }

    /// <summary>
    /// Serialize and publish immediately if a change is pending, cancelling
    /// the idle window. Safe to call when nothing is pending.
    /// </summary>
    public void Flush()
    {
        _idleTimer.Stop();
        if (!_hasPending)
        {
            return;
        }

        _hasPending = false;
        _publish(_serialize());
    }

    private void IdleTimer_Tick(object? sender, EventArgs args)
    {
        _idleTimer.Stop();
        if (!_hasPending)
        {
            return;
        }

        _hasPending = false;
        _publish(_serialize());
    }

    public void Dispose()
    {
        _idleTimer.Stop();
        _idleTimer.Tick -= IdleTimer_Tick;
        _idleTimer.Dispose();
    }
}
