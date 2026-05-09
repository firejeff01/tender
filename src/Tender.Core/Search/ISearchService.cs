using Tender.Core.Models;

namespace Tender.Core.Search;

/// <summary>
/// 對單日標案資料集合套用搜尋條件、排序，回傳過濾後結果。
/// 不負責讀檔，呼叫端先載入 DailyTenderSnapshot 後傳入。
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// 套用搜尋條件並排序。
    /// </summary>
    /// <param name="items">輸入的標案集合（通常為當日全部）。</param>
    /// <param name="criteria">搜尋條件。</param>
    /// <param name="sortKey">排序欄位。</param>
    /// <param name="direction">排序方向。</param>
    /// <param name="todayForActiveCheck">用於「只看尚未截止」比對的今日日期。</param>
    /// <returns>過濾並排序後的標案集合。</returns>
    IReadOnlyList<TenderItem> Search(
        IReadOnlyList<TenderItem> items,
        SearchCriteria criteria,
        SortKey sortKey,
        SortDirection direction,
        DateOnly todayForActiveCheck);
}
