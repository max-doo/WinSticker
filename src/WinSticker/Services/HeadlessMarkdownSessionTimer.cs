namespace WinSticker.Services;

/// <summary>Explicit-save timer used by view models created without a UI dispatcher.</summary>
internal sealed class HeadlessMarkdownSessionTimer : IMarkdownSessionTimer
{
    public event EventHandler? Tick { add { } remove { } }
    public bool IsEnabled { get; private set; }
    public void Start() => IsEnabled = true;
    public void Stop() => IsEnabled = false;
    public void Dispose() => Stop();
}
