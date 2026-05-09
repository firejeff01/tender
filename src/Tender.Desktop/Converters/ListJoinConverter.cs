using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace Tender.Desktop.Converters;

/// <summary>
/// 把任意 IEnumerable 轉成以分隔符串接的字串。預設分隔符為「、」。
/// </summary>
public sealed class ListJoinConverter : IValueConverter
{
    public string Separator { get; set; } = "、";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable enumerable && value is not string)
        {
            var parts = new List<string>();
            foreach (var item in enumerable)
            {
                if (item != null) parts.Add(item.ToString() ?? string.Empty);
            }
            return string.Join(Separator, parts);
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
