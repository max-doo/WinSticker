using WinSticker.Services;

namespace WinSticker.Tests;

/// <summary>
/// The optional max-pending-delay timer on <see cref="MarkdownEditingSession"/>:
/// continuous input without an idle window still saves within the deadline,
/// typing never resets it, and it re-arms only for revisions that arrived
/// during a save. Sessions without the timer keep the old behavior.
/// </summary>
public sealed class NoteSaveDeadlineTests
{
    private sealed class FakeTimer : IMarkdownSessionTimer
    {
        public event EventHandler? Tick;
        public bool IsEnabled { get; private set; }
        public int StartCount { get; private set; }
        public void Start()
        {
            IsEnabled = true;
            StartCount++;
        }

        public void Stop() => IsEnabled = false;
        public void Fire() => Tick?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }

    private sealed class Harness
    {
        public FakeTimer Autosave = new();
        public FakeTimer Deadline = new();
        public MarkdownEditingSession Session = null!;
        public List<string> SavedBodies = [];

        public static Harness Create(bool failingSave = false)
        {
            var harness = new Harness
            {
                Autosave = new FakeTimer(),
                Deadline = new FakeTimer()
            };
            harness.Session = new MarkdownEditingSession(harness.Autosave, harness.Deadline)
            {
                SaveCore = () =>
                {
                    harness.SavedBodies.Add($"save-{harness.SavedBodies.Count}");
                    return Task.FromResult(!failingSave);
                }
            };
            return harness;
        }
    }

    [Fact]
    public async Task ContinuousInputWithoutIdle_StillSavesAtDeadline()
    {
        var harness = Harness.Create();
        var (session, deadline) = (harness.Session, harness.Deadline);

        session.NotifyChanged();
        session.NotifyChanged();
        session.NotifyChanged();

        // No idle window ever fires the 600 ms autosave timer.
        deadline.Fire();
        await session.FlushAsync();

        Assert.Single(harness.SavedBodies);
        Assert.False(session.HasUnsavedChanges);
        Assert.False(deadline.IsEnabled);
    }

    [Fact]
    public void TypingDoesNotResetTheDeadline()
    {
        var harness = Harness.Create();
        var (session, deadline) = (harness.Session, harness.Deadline);

        session.NotifyChanged();
        int startsAfterFirst = deadline.StartCount;
        Assert.Equal(1, startsAfterFirst);

        session.NotifyChanged();
        session.NotifyChanged();

        // Armed once from the first unsaved revision; later typing must not
        // postpone the save deadline.
        Assert.Equal(startsAfterFirst, deadline.StartCount);
    }

    [Fact]
    public async Task RevisionsArrivingDuringSave_GetAFreshDeadline()
    {
        var harness = new Harness
        {
            Autosave = new FakeTimer(),
            Deadline = new FakeTimer()
        };
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Session = new MarkdownEditingSession(harness.Autosave, harness.Deadline)
        {
            SaveCore = async () =>
            {
                harness.SavedBodies.Add($"save-{harness.SavedBodies.Count}");
                saveStarted.TrySetResult();
                await saveReleased.Task;
                return true;
            }
        };
        var (session, deadline) = (harness.Session, harness.Deadline);

        session.NotifyChanged();
        _ = Task.Run(() => deadline.Fire());
        await saveStarted.Task;

        // The save is provably in flight; the user keeps typing.
        session.NotifyChanged();
        saveReleased.TrySetResult();
        await WaitUntilAsync(() => harness.SavedBodies.Count == 1 && !session.IsSaving);

        Assert.True(session.HasUnsavedChanges);
        // A fresh deadline covers the revision typed during the save.
        Assert.True(deadline.IsEnabled);
        int rearmStarts = deadline.StartCount;

        deadline.Fire();
        await session.FlushAsync();

        Assert.Equal(2, harness.SavedBodies.Count);
        Assert.False(session.HasUnsavedChanges);
        Assert.Equal(rearmStarts, deadline.StartCount);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 500 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task FailedSave_KeepsDeadlineArmedForRetry()
    {
        var harness = Harness.Create(failingSave: true);
        var (session, deadline) = (harness.Session, harness.Deadline);

        session.NotifyChanged();
        deadline.Fire();
        await session.FlushAsync();

        Assert.True(session.HasUnsavedChanges);
        // The deadline stays armed: the retry rides the next window instead
        // of an immediate hot loop.
        Assert.True(deadline.IsEnabled);
    }

    [Fact]
    public async Task IdleAutosave_DisarmsDeadlineWhenClean()
    {
        var harness = Harness.Create();
        var (session, autosave, deadline) = (harness.Session, harness.Autosave, harness.Deadline);

        session.NotifyChanged();
        Assert.True(deadline.IsEnabled);

        autosave.Fire();
        await session.FlushAsync();

        Assert.Single(harness.SavedBodies);
        Assert.False(session.HasUnsavedChanges);
        Assert.False(deadline.IsEnabled);
    }

    [Fact]
    public async Task SessionWithoutDeadlineTimer_KeepsLegacyBehavior()
    {
        var autosave = new FakeTimer();
        int saves = 0;
        var session = new MarkdownEditingSession(autosave)
        {
            SaveCore = () => { saves++; return Task.FromResult(true); }
        };

        session.NotifyChanged();
        autosave.Fire();
        await session.FlushAsync();

        Assert.Equal(1, saves);
        Assert.False(session.HasUnsavedChanges);
    }
}
