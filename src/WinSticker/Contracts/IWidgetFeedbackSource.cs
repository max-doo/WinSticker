using WinSticker.Models;

namespace WinSticker.Contracts;

public sealed class WidgetFeedbackRequestedEventArgs(
    WidgetFeedbackRequest request) : EventArgs
{
    public WidgetFeedbackRequest Request { get; } = request;
}

public interface IWidgetFeedbackSource
{
    event EventHandler<WidgetFeedbackRequestedEventArgs>? FeedbackRequested;
}
