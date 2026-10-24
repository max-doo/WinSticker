using System;
using System.Globalization;
using System.IO;
using WinSticker.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI.Text;

namespace WinSticker.Helpers;

public sealed partial class QuickPanelBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility.Visible;
    }
}

public sealed partial class QuickPanelInverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is not Visibility.Visible;
    }
}

public sealed partial class QuickPanelCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed partial class QuickPanelZeroCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed partial class QuickPanelDueDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DateTimeOffset dto)
        {
            return string.Empty;
        }

        var local = dto.ToLocalTime();
        var today = DateTimeOffset.Now.Date;
        var due = local.Date;

        if (due < today)
        {
            return "已逾期";
        }
        if (due == today)
        {
            return "今天";
        }
        if (due == today.AddDays(1))
        {
            return "明天";
        }
        return $"{local.Month:00}-{local.Day:00}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed partial class QuickPanelDueDateForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush OverdueBrush = new(Windows.UI.Color.FromArgb(0xFF, 0xE3, 0x4D, 0x4D));
    private static readonly SolidColorBrush TodayBrush = new(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x78, 0xD4));
    private static readonly SolidColorBrush NormalBrush = new(Windows.UI.Color.FromArgb(0x99, 0x80, 0x80, 0x80));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DateTimeOffset dto)
        {
            return NormalBrush;
        }

        var local = dto.ToLocalTime();
        var today = DateTimeOffset.Now.Date;
        var due = local.Date;

        if (due < today)
        {
            return OverdueBrush;
        }
        if (due == today)
        {
            return TodayBrush;
        }
        return NormalBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed partial class QuickPanelItemTypeGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is QuickCaptureItem item)
        {
            return item.Type switch
            {
                QuickCaptureItemType.Image => "\uEB9F",
                QuickCaptureItemType.Link => "\uE71B",
                _ => "\uE8C8"
            };
        }
        return "\uE8C8";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed partial class QuickPanelClipboardDisplayTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is QuickCaptureItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.Title))
            {
                return item.Title;
            }
            if (item.Type == QuickCaptureItemType.Link && !string.IsNullOrWhiteSpace(item.Url))
            {
                return item.Url;
            }
            if (item.Type == QuickCaptureItemType.Image && string.IsNullOrWhiteSpace(item.Body))
            {
                return "[图片]";
            }
            return string.IsNullOrWhiteSpace(item.Body) ? "(空内容)" : item.Body;
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed partial class QuickPanelClipboardTypeBadgeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is QuickCaptureItem item)
        {
            return item.Type switch
            {
                QuickCaptureItemType.Image => "图片",
                QuickCaptureItemType.Link => "链接",
                _ => "文本"
            };
        }
        return "文本";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed partial class QuickPanelDateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTimeOffset dto)
        {
            return dto.ToLocalTime().ToString("MM-dd HH:mm");
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed partial class QuickPanelStarGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? "\uE735" : "\uE734";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed partial class QuickPanelStarForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush StarredBrush = new(Windows.UI.Color.FromArgb(0xFF, 0xF2, 0xC9, 0x4C));
    private static readonly SolidColorBrush UnstarredBrush = new(Windows.UI.Color.FromArgb(0x99, 0x80, 0x80, 0x80));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? StarredBrush : UnstarredBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

    public sealed partial class QuickPanelItemIsImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is QuickCaptureItem item &&
                TryResolveExistingImagePath(item) is not null)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }

        // Mirrors QuickPanelImageSourceConverter's path resolution so an image
        // record whose file went missing degrades to the text card instead of
        // rendering an empty image card.
        internal static string? TryResolveExistingImagePath(QuickCaptureItem item)
        {
            string? path = item.ImagePath;
            if (string.IsNullOrWhiteSpace(path) && item.Attachments.Count > 0)
            {
                path = item.Attachments[0].FilePath;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return File.Exists(path) ? path : null;
            }
            catch
            {
                return null;
            }
        }
    }

    public sealed partial class QuickPanelItemIsNotImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is QuickCaptureItem item &&
                QuickPanelItemIsImageConverter.TryResolveExistingImagePath(item) is not null)
            {
                return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed partial class QuickPanelImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        string? path = null;
        if (value is QuickCaptureItem item)
        {
            path = item.ImagePath;
            if (string.IsNullOrWhiteSpace(path) && item.Attachments.Count > 0)
            {
                path = item.Attachments[0].FilePath;
            }
        }
        else if (value is string str)
        {
            path = str;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            if (File.Exists(path))
            {
                return new BitmapImage(new Uri(path));
            }
        }
        catch
        {
            // ignore invalid URI or file access issues
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed partial class QuickPanelTodoStrikethroughConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? TextDecorations.Strikethrough : TextDecorations.None;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

public sealed partial class QuickPanelTodoCompletedOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? 0.55 : 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
