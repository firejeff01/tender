using Tender.Core.Models;

namespace Tender.Crawler.Application;

public interface IDailySummaryService
{
    /// <summary>
    /// 依據合併後的 tenders.json 與本次 run 結果，更新 summary.json。
    /// totalCount = 該日 items 總筆數；其餘欄位來自 run 結果。
    /// </summary>
    Task GenerateAsync(DateOnly date, CrawlRun lastRun, CancellationToken ct = default);
}
