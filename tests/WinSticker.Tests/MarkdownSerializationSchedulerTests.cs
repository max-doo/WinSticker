using WinSticker.Services;

namespace WinSticker.Tests;

public sealed class MarkdownSerializationSchedulerTests
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

    private sealed class Harness
    {
        public FakeTimer Timer { get; } = new();
        public string CurrentText { get; set; } = string.Empty;
        public List<string> Published { get; } = [];
        public int SerializeCount { get; private set; }

        public MarkdownSerializationScheduler Create() => new(
            Timer,
            () =>
            {
                SerializeCount++;
                return CurrentText;
            },
            Published.Add);
    }

    [Fact]
    public void NotifyChanged_DoesNotPublishImmediately()
    {
        var harness = new Harness { CurrentText = "甲" };
        using var scheduler = harness.Create();

        scheduler.NotifyChanged();
        scheduler.NotifyChanged();
        scheduler.NotifyChanged();

        Assert.Empty(harness.Published);
        Assert.True(scheduler.HasPending);
        Assert.Equal(0, harness.SerializeCount);
    }

    [Fact]
    public void NotifyChanged_RestartsDebounceWindowEachTime()
    {
        var harness = new Harness();
        using var scheduler = harness.Create();

        scheduler.NotifyChanged();
        scheduler.NotifyChanged();

        Assert.Equal(2, harness.Timer.StartCount);
    }

    [Fact]
    public void Tick_PublishesLatestTextExactlyOnce()
    {
        var harness = new Harness();
        using var scheduler = harness.Create();

        scheduler.NotifyChanged();
        harness.CurrentText = "第一次";
        scheduler.NotifyChanged();
        harness.CurrentText = "第二次";
        scheduler.NotifyChanged();
        harness.Timer.Fire();

        Assert.Equal(["第二次"], harness.Published);
        Assert.False(scheduler.HasPending);
    }

    [Fact]
    public void Tick_WithoutPendingChange_DoesNotPublish()
    {
        var harness = new Harness { CurrentText = "文本" };
        using var scheduler = harness.Create();

        harness.Timer.Fire();

        Assert.Empty(harness.Published);
        Assert.Equal(0, harness.SerializeCount);
    }

    [Fact]
    public void Flush_PublishesLatestTextBeforeDebounceWindowElapses()
    {
        var harness = new Harness();
        using var scheduler = harness.Create();

        scheduler.NotifyChanged();
        harness.CurrentText = "待冲刷";
        scheduler.Flush();

        Assert.Equal(["待冲刷"], harness.Published);
        Assert.False(scheduler.HasPending);
        Assert.False(harness.Timer.IsEnabled);

        // A later tick must not publish a second time.
        harness.Timer.Fire();
        Assert.Single(harness.Published);
    }

    [Fact]
    public void Flush_WithoutPendingChange_IsNoOp()
    {
        var harness = new Harness { CurrentText = "文本" };
        using var scheduler = harness.Create();

        scheduler.Flush();

        Assert.Empty(harness.Published);
        Assert.Equal(0, harness.SerializeCount);
    }

    [Fact]
    public void Flush_AfterPublish_CallsSerializeAgainOnlyWhenNewChangeArrived()
    {
        var harness = new Harness();
        using var scheduler = harness.Create();

        scheduler.NotifyChanged();
        harness.Timer.Fire();
        Assert.Single(harness.Published);

        harness.CurrentText = "又改了";
        scheduler.NotifyChanged();
        scheduler.Flush();

        Assert.Equal(["", "又改了"], harness.Published);
    }

    [Fact]
    public void Dispose_StopsDebounceAndTickNoLongerPublishes()
    {
        var harness = new Harness();
        var scheduler = harness.Create();

        scheduler.NotifyChanged();
        scheduler.Dispose();

        Assert.False(harness.Timer.IsEnabled);
        harness.Timer.Fire();
        Assert.Empty(harness.Published);
    }
}
