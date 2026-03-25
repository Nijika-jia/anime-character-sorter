using System.Globalization;
using System.Windows.Data;
using AnimeSorterWin.Models;

namespace AnimeSorterWin;

/// <summary>
/// RadioButton 的 IsChecked 绑定转换器：
/// ConverterParameter = "Copy"/"Move" 时，把 FileOperationMode 映射成 bool。
/// </summary>
public sealed class FileOperationModeToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not FileOperationMode mode)
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

        if (Enum.TryParse<FileOperationMode>(target, ignoreCase: true, out var mode))
            return mode;

        return System.Windows.Data.Binding.DoNothing;
    }
}

