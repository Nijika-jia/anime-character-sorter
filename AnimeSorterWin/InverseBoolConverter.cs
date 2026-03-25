using System.Globalization;
using System.Windows.Data;

namespace AnimeSorterWin;

/// <summary>
/// 用于把 bool 取反：常用于按钮 IsEnabled 反向绑定。
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? !b : true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

