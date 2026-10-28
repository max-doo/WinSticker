using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace WinSticker.Controls;

public sealed partial class PinStateIcon : UserControl
{
    public static readonly DependencyProperty IsPinnedProperty =
        DependencyProperty.Register(
            nameof(IsPinned),
            typeof(bool),
            typeof(PinStateIcon),
            new PropertyMetadata(false, OnIsPinnedChanged));

    public static readonly DependencyProperty IsFavoriteStyleProperty =
        DependencyProperty.Register(nameof(IsFavoriteStyle), typeof(bool), typeof(PinStateIcon),
            new PropertyMetadata(false, OnIsPinnedChanged));

    // Star outline points, clockwise from the top point. XAML cannot share a
    // reusable Geometry resource on WinUI (the abstract Geometry type is not
    // creatable from markup and PathGeometry takes no figure shorthand
    // string), so the favorite shape is built here. Each Path needs its own
    // Geometry instance: WinUI rejects handing one instance to two Paths.
    private static readonly Point[] FavoriteShapePoints =
    [
        new(8, 1), new(10.1, 5.4), new(15, 6.1), new(11.5, 9.5), new(12.3, 14.5),
        new(8, 12.1), new(3.7, 14.5), new(4.5, 9.5), new(1, 6.1), new(5.9, 5.4)
    ];

    // The pin outline declared by this control's markup; restored whenever the
    // icon leaves the favorite style.
    private readonly Geometry? _unpinnedPinGeometry;
    private readonly Geometry? _pinnedPinGeometry;

    public PinStateIcon()
    {
        InitializeComponent();
        _unpinnedPinGeometry = UnpinnedPath.Data;
        _pinnedPinGeometry = PinnedPath.Data;
        UpdatePinState();
    }

    public bool IsPinned
    {
        get => (bool)GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    public bool IsFavoriteStyle
    {
        get => (bool)GetValue(IsFavoriteStyleProperty);
        set => SetValue(IsFavoriteStyleProperty, value);
    }

    private static void OnIsPinnedChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args) =>
        ((PinStateIcon)sender).UpdatePinState();

    private void UpdatePinState()
    {
        if (PinnedPath is null || UnpinnedPath is null)
        {
            return;
        }

        if (IsFavoriteStyle)
        {
            UnpinnedPath.Data = CreateFavoriteGeometry();
            PinnedPath.Data = CreateFavoriteGeometry();
        }
        else
        {
            UnpinnedPath.Data = _unpinnedPinGeometry;
            PinnedPath.Data = _pinnedPinGeometry;
        }

        PinnedPath.Visibility = IsPinned ? Visibility.Visible : Visibility.Collapsed;
        UnpinnedPath.Visibility = IsPinned ? Visibility.Collapsed : Visibility.Visible;
    }

    private static PathGeometry CreateFavoriteGeometry()
    {
        var figure = new PathFigure
        {
            StartPoint = FavoriteShapePoints[0],
            IsClosed = true,
            IsFilled = true
        };
        foreach (Point point in FavoriteShapePoints.Skip(1))
        {
            figure.Segments.Add(new LineSegment { Point = point });
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
