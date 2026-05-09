using Tender.Core.Models;

namespace Tender.Desktop.Services;

/// <summary>
/// 月份行事曆服務：讀取每日 summary.json，組成 MonthlyCalendarView。
/// 不解析 tenders.json（資料量大，留給點進當日才載入）。
/// </summary>
public interface IMonthlyCalendarService
{
    /// <summary>
    /// 載入指定月份的行事曆視圖：列出該月所有日資料夾，讀取每日 summary.json。
    /// 無資料的日 Summary 為 null。
    /// </summary>
    Task<MonthlyCalendarView> LoadMonthAsync(int year, int month, CancellationToken ct = default);

    /// <summary>重新讀取單日 summary.json（用於「立即更新」完成後刷新）。</summary>
    Task<MonthlyCalendarDay> RefreshDayAsync(DateOnly date, CancellationToken ct = default);
}
