using Tender.Core.DateConversion;
using Tender.Core.Models;

namespace Tender.Core.Search;

/// <summary>
/// 搜尋服務實作。
/// 多關鍵字 (KeywordQuery) 採 AND + 模糊查詢（子字串包含）。
/// ActiveKeywordButtons 採 OR（任一命中即可）。
/// 各大條件（KeywordQuery、ActiveKeywordButtons、TenderMethod 等）之間採 AND 邏輯。
/// null 值排尾：排序時 null 欄位（BidDeadline、BudgetAmount）排在最後。
/// </summary>
public sealed class SearchService : ISearchService
{
    private readonly ITaiwanDateConverter _dateConverter;

    public SearchService(ITaiwanDateConverter dateConverter)
    {
        _dateConverter = dateConverter;
    }

    /// <inheritdoc/>
    public IReadOnlyList<TenderItem> Search(
        IReadOnlyList<TenderItem> items,
        SearchCriteria criteria,
        SortKey sortKey,
        SortDirection direction,
        DateOnly todayForActiveCheck)
    {
        var query = items.AsEnumerable();

        // 1. KeywordQuery
        //    - 分隔符：逗號（半形 ','、全形 '，'）；token 之間採 OR
        //    - 空白為字面值的一部分（例「AI 系統」就找含「AI 系統」這個整字串）
        //    - TitleOnly：只比對 TenderName，否則比對 TenderName OR AgencyName
        //    - Exclude：把命中者排除（與 TitleOnly 獨立，可單獨啟用）
        if (!string.IsNullOrWhiteSpace(criteria.KeywordQuery))
        {
            var tokens = criteria.KeywordQuery
                .Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (tokens.Length > 0)
            {
                var titleOnly = criteria.KeywordTitleOnly;
                var exclude   = criteria.KeywordExclude;
                Func<TenderItem, bool> anyTokenHit = item =>
                    tokens.Any(t =>
                        item.TenderName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (!titleOnly && item.AgencyName.Contains(t, StringComparison.OrdinalIgnoreCase)));

                query = exclude
                    ? query.Where(item => !anyTokenHit(item))
                    : query.Where(item => anyTokenHit(item));
            }
        }

        // 2. ActiveKeywordButtons：OR 邏輯（任一命中）
        if (criteria.ActiveKeywordButtons.Count > 0)
        {
            query = query.Where(item =>
                criteria.ActiveKeywordButtons.Any(btn =>
                    item.MatchedKeywords.Contains(btn, StringComparer.Ordinal)));
        }

        // 3. TenderMethod 篩選
        if (!string.IsNullOrWhiteSpace(criteria.TenderMethod))
        {
            var method = criteria.TenderMethod;
            query = query.Where(item =>
                item.TenderMethod.Equals(method, StringComparison.OrdinalIgnoreCase));
        }

        // 4. ProcurementType 篩選
        if (!string.IsNullOrWhiteSpace(criteria.ProcurementType))
        {
            var pt = criteria.ProcurementType;
            query = query.Where(item =>
                item.ProcurementType != null &&
                item.ProcurementType.Equals(pt, StringComparison.OrdinalIgnoreCase));
        }

        // 5. AnnouncementDate 區間篩選
        if (!string.IsNullOrWhiteSpace(criteria.AnnouncementDateFrom))
        {
            var from = _dateConverter.RocToDateOnly(criteria.AnnouncementDateFrom);
            if (from.HasValue)
            {
                query = query.Where(item =>
                {
                    var d = _dateConverter.RocToDateOnly(item.AnnouncementDate);
                    return d.HasValue && d.Value >= from.Value;
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(criteria.AnnouncementDateTo))
        {
            var to = _dateConverter.RocToDateOnly(criteria.AnnouncementDateTo);
            if (to.HasValue)
            {
                query = query.Where(item =>
                {
                    var d = _dateConverter.RocToDateOnly(item.AnnouncementDate);
                    return d.HasValue && d.Value <= to.Value;
                });
            }
        }

        // 6. BidDeadline 區間篩選
        if (!string.IsNullOrWhiteSpace(criteria.BidDeadlineFrom))
        {
            var from = _dateConverter.RocToDateOnly(criteria.BidDeadlineFrom);
            if (from.HasValue)
            {
                query = query.Where(item =>
                {
                    if (item.BidDeadline is null) return false;
                    var d = _dateConverter.RocToDateOnly(item.BidDeadline);
                    return d.HasValue && d.Value >= from.Value;
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(criteria.BidDeadlineTo))
        {
            var to = _dateConverter.RocToDateOnly(criteria.BidDeadlineTo);
            if (to.HasValue)
            {
                query = query.Where(item =>
                {
                    if (item.BidDeadline is null) return false;
                    var d = _dateConverter.RocToDateOnly(item.BidDeadline);
                    return d.HasValue && d.Value <= to.Value;
                });
            }
        }

        // 7. BudgetAmount 區間篩選
        if (criteria.BudgetMin.HasValue)
        {
            var min = criteria.BudgetMin.Value;
            query = query.Where(item => item.BudgetAmount.HasValue && item.BudgetAmount.Value >= min);
        }

        if (criteria.BudgetMax.HasValue)
        {
            var max = criteria.BudgetMax.Value;
            query = query.Where(item => item.BudgetAmount.HasValue && item.BudgetAmount.Value <= max);
        }

        // 8. ShowActiveOnly：BidDeadline >= today
        if (criteria.ShowActiveOnly)
        {
            query = query.Where(item =>
            {
                if (item.BidDeadline is null) return false;
                var d = _dateConverter.RocToDateOnly(item.BidDeadline);
                return d.HasValue && d.Value >= todayForActiveCheck;
            });
        }

        // 9. 排序（null 值排尾）
        var result = ApplySort(query, sortKey, direction);
        return result.ToList().AsReadOnly();
    }

    private static IEnumerable<TenderItem> ApplySort(
        IEnumerable<TenderItem> query,
        SortKey sortKey,
        SortDirection direction)
    {
        return (sortKey, direction) switch
        {
            (SortKey.None, _) => query,
            (SortKey.AgencyName, SortDirection.Ascending) =>
                query.OrderBy(x => x.AgencyName, StringComparer.Ordinal),
            (SortKey.AgencyName, SortDirection.Descending) =>
                query.OrderByDescending(x => x.AgencyName, StringComparer.Ordinal),
            (SortKey.TenderName, SortDirection.Ascending) =>
                query.OrderBy(x => x.TenderName, StringComparer.Ordinal),
            (SortKey.TenderName, SortDirection.Descending) =>
                query.OrderByDescending(x => x.TenderName, StringComparer.Ordinal),
            (SortKey.AnnouncementDate, SortDirection.Ascending) =>
                query.OrderBy(x => x.AnnouncementDate, StringComparer.Ordinal),
            (SortKey.AnnouncementDate, SortDirection.Descending) =>
                query.OrderByDescending(x => x.AnnouncementDate, StringComparer.Ordinal),
            // null 排尾：null 的 BidDeadline 排最後
            (SortKey.BidDeadline, SortDirection.Ascending) =>
                query.OrderBy(x => x.BidDeadline is null ? 1 : 0)
                     .ThenBy(x => x.BidDeadline, StringComparer.Ordinal),
            (SortKey.BidDeadline, SortDirection.Descending) =>
                query.OrderBy(x => x.BidDeadline is null ? 1 : 0)
                     .ThenByDescending(x => x.BidDeadline, StringComparer.Ordinal),
            // null 排尾：null 的 BudgetAmount 排最後
            (SortKey.BudgetAmount, SortDirection.Ascending) =>
                query.OrderBy(x => x.BudgetAmount.HasValue ? 0 : 1)
                     .ThenBy(x => x.BudgetAmount ?? 0),
            (SortKey.BudgetAmount, SortDirection.Descending) =>
                query.OrderBy(x => x.BudgetAmount.HasValue ? 0 : 1)
                     .ThenByDescending(x => x.BudgetAmount ?? 0),
            _ => query,
        };
    }
}
