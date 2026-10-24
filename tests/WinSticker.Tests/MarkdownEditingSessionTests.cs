using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class MarkdownEditingSessionTests
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
        public void Fire() => Tick?.Invoke(null, EventArgs.Empty);
        public void Dispose() { }
    }

    private static (MarkdownEditingSession Session, FakeTimer Timer) Create(
        Func<Task<bool>>? saveCore = null)
    {
        var timer = new FakeTimer();
        var session = new MarkdownEditingSession(timer)
        {
            SaveCore = saveCore
        };
        return (session, timer);
    }

    [Fact]
    public void NotifyChanged_MarksDirtyAndSchedulesAutoSave()
    {
        var (session, timer) = Create(() => Task.FromResult(true));

        session.NotifyChanged();

        Assert.True(session.HasUnsavedChanges);
        Assert.True(session.IsAutoSaveScheduled);
    }

    [Fact]
    public void NotifyChanged_RestartsAlreadyScheduledTimer()
    {
        var (session, timer) = Create(() => Task.FromResult(true));

        session.NotifyChanged();
        session.NotifyChanged();

        Assert.Equal(2, timer.StartCount);
    }

    [Fact]
    public async Task AutoSaveTick_SavesAndClearsDirtyOnSuccess()
    {
        int saves = 0;
        var (session, timer) = Create(() =>
        {
            saves++;
            return Task.FromResult(true);
        });
        int completed = 0;
        session.SaveCompleted += (_, _) => completed++;

        session.NotifyChanged();
        timer.Fire();
        await session.FlushAsync();

        Assert.Equal(1, saves);
        Assert.False(session.HasUnsavedChanges);
        Assert.False(session.IsAutoSaveScheduled);
        Assert.Equal(1, completed);
    }

    [Fact]
    public async Task AutoSaveTick_KeepsDirtyWhenSaveFailsUntilNextSaveSucceeds()
    {
        int failed = 0;
        int calls = 0;
        var (session, timer) = Create(() =>
        {
            calls++;
            return Task.FromResult(calls > 1);
        });
        session.SaveFailed += (_, _) => failed++;

        session.NotifyChanged();
        Assert.False(await session.SaveAsync(completeEditing: false));
        Assert.True(session.HasUnsavedChanges);
        Assert.Equal(1, failed);
        Assert.Equal(1, calls);

        timer.Fire();
        await session.FlushAsync();
        Assert.Equal(2, calls);
        Assert.False(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task CommitAsync_ResavesChangesMadeDuringSave()
    {
        int calls = 0;
        var timer = new FakeTimer();
        var session = new MarkdownEditingSession(timer);
        session.SaveCore = () =>
        {
            calls++;
            if (calls == 1)
            {
                // 用户在保存期间继续输入
                session.NotifyChanged();
            }

            return Task.FromResult(true);
        };

        session.NotifyChanged();
        bool committed = await session.SaveAsync(completeEditing: true);

        Assert.True(committed);
        Assert.Equal(2, calls);
        Assert.False(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task CommitAsync_StopsLoopAndStaysDirtyWhenSaveFails()
    {
        int calls = 0;
        var (session, timer) = Create(() =>
        {
            calls++;
            return Task.FromResult(false);
        });

        session.NotifyChanged();
        bool committed = await session.SaveAsync(completeEditing: true);

        Assert.False(committed);
        Assert.Equal(1, calls);
        Assert.True(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task SaveCoreThrowing_ReturnsFalseRaisesFailedAndReleasesGate()
    {
        int failed = 0;
        var throws = true;
        var (session, timer) = Create(() => throws
            ? Task.FromException<bool>(new InvalidOperationException("boom"))
            : Task.FromResult(true));
        session.SaveFailed += (_, _) => failed++;

        session.NotifyChanged();
        Assert.False(await session.SaveAsync(completeEditing: false));
        throws = false;
        Assert.True(await session.SaveAsync(completeEditing: false));

        Assert.Equal(1, failed);
    }

    [Fact]
    public async Task FlushAsync_WhenCleanAndIdle_SkipsSave()
    {
        int saves = 0;
        var (session, timer) = Create(() =>
        {
            saves++;
            return Task.FromResult(true);
        });

        Assert.True(await session.FlushAsync());
        Assert.Equal(0, saves);
    }

    [Fact]
    public async Task FlushAsync_WaitsForInFlightSaveAndDoesNotDoubleSave()
    {
        int saves = 0;
        var gate = new TaskCompletionSource<bool>();
        var (session, timer) = Create(async () =>
        {
            saves++;
            if (saves == 1)
            {
                return await gate.Task;
            }

            return true;
        });

        session.NotifyChanged();
        Task<bool> first = session.SaveAsync(completeEditing: false);
        Task<bool> flush = session.FlushAsync();
        Assert.False(first.IsCompleted);
        gate.SetResult(true);
        Assert.True(await first);
        Assert.True(await flush);

        Assert.Equal(1, saves);
        Assert.False(session.HasUnsavedChanges);
    }

    [Fact]
    public void MarkDirty_DirtiesWithoutSchedulingTimer()
    {
        var (session, timer) = Create(() => Task.FromResult(true));

        session.MarkDirty();

        Assert.True(session.HasUnsavedChanges);
        Assert.False(session.IsAutoSaveScheduled);
    }

    [Fact]
    public void ScheduleAutoSave_StartsTimerWithoutMarkingDirty()
    {
        var (session, timer) = Create(() => Task.FromResult(true));

        session.ScheduleAutoSave();

        Assert.True(session.IsAutoSaveScheduled);
        Assert.False(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task Reset_ClearsDirtyAndStopsTimer()
    {
        var (session, timer) = Create(() => Task.FromResult(true));

        session.NotifyChanged();
        session.Reset();

        Assert.False(session.HasUnsavedChanges);
        Assert.False(session.IsAutoSaveScheduled);
        Assert.True(await session.FlushAsync());
    }

    [Fact]
    public void AbandonDirtyDraft_DropsPendingChanges()
    {
        var (session, timer) = Create(() => Task.FromResult(true));

        session.NotifyChanged();
        session.AbandonDirtyDraft();

        Assert.False(session.HasUnsavedChanges);
    }
}
