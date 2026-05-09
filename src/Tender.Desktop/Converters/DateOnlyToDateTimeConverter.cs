using System.Globalization;
using System.Windows.Data;

namespace Tender.Desktop.Converters;

/// <summary>DatePicker 期望 DateTime?，VM 用 DateOnly? 時雙向轉換。</summary>
public sealed class DateOnlyToDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DateOnly d => d.ToDateTime(TimeOnly.MinValue),
            null => null,
            _ => null,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DateTime dt => DateOnly.FromDateTime(dt),
            null => null,
            _ => null,
        };
    }
}
