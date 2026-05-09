using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Tender.Desktop.Converters;

/// <summary>
/// 數值 0 → Collapsed，非 0 → Visible。
/// ConverterParameter="Inverse" 反轉。
/// </summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isZero = value switch
        {
            int i => i == 0,
            long l => l == 0,
            double d => d == 0,
            _ => value == null,
        };
        var inverse = parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
        if (inverse) isZero = !isZero;
        return isZero ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
