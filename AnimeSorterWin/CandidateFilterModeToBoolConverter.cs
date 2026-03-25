using System;
using System.Globalization;
using System.Windows.Data;
using AnimeSorterWin.Models;

namespace AnimeSorterWin;

/// <summary>
/// 把 CandidateFilterMode 映射到 RadioButton.IsChecked（ConverterParameter 为枚举名）。
/// </summary>
public sealed class CandidateFilterModeToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not CandidateFilterMode mode)
            return false;

        var target = parameter?.ToString();
        if (string.IsNullOrWhiteSpace(target))
            return false;

        return string.Equals(mode.ToString(), target, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool b || !b)
            return System.Windows.Data.Binding.DoNothing;

        var target = parameter?.ToString();
        if (string.IsNullOrWhiteSpace(target))
            return System.Windows.Data.Binding.DoNothing;

        if (Enum.TryParse<CandidateFilterMode>(target, ignoreCase: true, out var parsed))
            return parsed;

        return System.Windows.Data.Binding.DoNothing;
    }
}

