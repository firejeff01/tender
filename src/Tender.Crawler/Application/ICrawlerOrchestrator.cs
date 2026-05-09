using Tender.Core.Models;

namespace Tender.Crawler.Application;

public interface ICrawlerOrchestrator
{
    /// <summary>
    /// 執行一次完整的爬蟲流程：查詢 → 解析 → 命中關鍵字 → 合併 → 寫摘要 → 寫 run 紀錄。
    /// 取得 crawler.lock，若已被持有則回傳 Skipped。
    /// </summary>
    Task<CrawlRun> RunAsync(CrawlerArgs args, CancellationToken ct = default);
}
