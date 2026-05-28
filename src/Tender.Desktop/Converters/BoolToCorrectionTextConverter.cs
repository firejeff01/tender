using System.Globalization;
using System.Windows.Data;

namespace Tender.Desktop.Converters;

public sealed class BoolToCorrectionTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? " (更正公告)" : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
