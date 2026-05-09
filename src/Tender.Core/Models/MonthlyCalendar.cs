namespace Tender.Core.Models;

/// <summary>
/// 月份行事曆視圖（唯讀彙總）。由 IMonthlyCalendarService 從每日 summary.json 組合產出。
/// </summary>
public sealed record MonthlyCalendarView
{
    public required int Year { get; init; }
    public required int Month { get; init; }

    /// <summary>當月所有日期（含無資料日，無資料日的 Summary 為 null）。</summary>
    public required IReadOnlyList<MonthlyCalendarDay> Days { get; init; }

    /// <summary>本月累計標案總數（每日 summary.totalCount 加總）。</summary>
    public required int MonthlyTotalCount { get; init; }
}

public sealed record MonthlyCalendarDay
{
    public required DateOnly Date { get; init; }
    /// <summary>null 代表該日無 summary.json。</summary>
    public DailySummary? Summary { get; init; }
    /// <summary>summary.json 存在但解析失敗。</summary>
    public bool IsCorrupted { get; init; }
}
