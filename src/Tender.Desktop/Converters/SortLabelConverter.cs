using System.Globalization;
using System.Windows.Data;
using TSortKey = Tender.Core.Search.SortKey;
using TSortDirection = Tender.Core.Search.SortDirection;

namespace Tender.Desktop.Converters;

/// <summary>把 SortKey / SortDirection 轉成中文顯示字串。</summary>
public sealed class SortLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            TSortKey k => k switch
            {
                TSortKey.None => "不排序",
                TSortKey.AgencyName => "機關名稱",
                TSortKey.TenderName => "標案名稱",
                TSortKey.AnnouncementDate => "公告日期",
                TSortKey.BidDeadline => "截止投標",
                TSortKey.BudgetAmount => "預算金額",
                _ => k.ToString(),
            },
            TSortDirection d => d switch
            {
                TSortDirection.Ascending => "升序（小→大）",
                TSortDirection.Descending => "降序（大→小）",
                _ => d.ToString(),
            },
            _ => value?.ToString() ?? string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
