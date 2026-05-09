using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Tender.Desktop.Converters;

/// <summary>把 "#RRGGBB" 字串轉成 <see cref="Color"/>，供 SolidColorBrush.Color 綁定。</summary>
public sealed class ColorStringToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            try { return (Color)ColorConverter.ConvertFromString(s); }
            catch { /* fallthrough */ }
        }
        return Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
