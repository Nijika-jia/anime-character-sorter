using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace AnimeSorterWin.Converters;

/// <summary>
/// 把完整文件路径转换为文件名。
/// </summary>
public sealed class FilePathToFileNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
            return Path.GetFileName(s);
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

