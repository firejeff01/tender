using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Tender.Desktop.Converters;

/// <summary>
/// 物件 != null → Visible, == null → Collapsed。
/// 傳入 ConverterParameter="Inverse" 會反轉。
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value != null;
        var inverse = parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
        if (inverse) hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
