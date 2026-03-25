using System.Globalization;
using System.Windows.Data;
using AnimeSorterWin.Models;

namespace AnimeSorterWin;

/// <summary>
/// 将 OutputOrganizationMode 映射到 RadioButton.IsChecked 的 bool。
/// ConverterParameter 为：SeriesThenCharacter / SeriesOnly / CharacterOnly / CharacterThenSeries。
/// </summary>
public sealed class OutputOrganizationModeToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not OutputOrganizationMode mode)
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

        if (Enum.TryParse<OutputOrganizationMode>(target, ignoreCase: true, out var parsed))
            return parsed;

        return System.Windows.Data.Binding.DoNothing;
    }
}

