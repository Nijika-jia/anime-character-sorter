using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AnimeSorterWin.Models;

namespace AnimeSorterWin.Converters;

public sealed class PendingStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not PendingItemStatus status)
            return System.Windows.Media.Brushes.Transparent;

        return status switch
        {
            PendingItemStatus.Confirmed => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 238, 168)), // light green
            PendingItemStatus.Skipped => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 230, 230)), // light gray
            _ => System.Windows.Media.Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

