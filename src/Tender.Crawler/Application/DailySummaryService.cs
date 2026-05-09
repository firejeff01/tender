using Tender.Core.Models;
using Tender.Storage.Repositories;

namespace Tender.Crawler.Application;

/// <summary>
/// 依據合併後的 tenders.json 與本次 run 結果，更新 summary.json。
/// totalCount = 該日 items 總筆數（來自 tenders.json）。
/// 其餘欄位來自 run 結果。
/// </summary>
public sealed class DailySummaryService : IDailySummaryService
{
    private readonly ITenderRepository _tenderRepo;
    private readonly IDailySummaryRepository _summaryRepo;

    public DailySummaryService(
        ITenderRepository tenderRepo,
        IDailySummaryRepository summaryRepo)
    {
        _tenderRepo = tenderRepo;
        _summaryRepo = summaryRepo;
    }

    public async Task GenerateAsync(DateOnly date, CrawlRun lastRun, CancellationToken ct = default)
    {
        // 取得當日標案總筆數
        int totalCount = 0;
        try
        {
            var snapshot = await _tenderRepo.LoadAsync(date, ct);
            totalCount = snapshot?.Items.Count ?? 0;
        }
        catch
        {
            // 若讀取失敗，totalCount 保持 0
        }

        var summary = new DailySummary
        {
            Date = date.ToString("yyyy-MM-dd"),
            TotalCount = totalCount,
            LastRunStatus = lastRun.Status,
            LastRunAt = lastRun.FinishedAt,
            InsertedCount = lastRun.InsertedCount,
            UpdatedCount = lastRun.UpdatedCount,
            SkippedCount = lastRun.SkippedCount,
            ErrorMessage = lastRun.ErrorMessage,
        };

        await _summaryRepo.SaveAsync(summary, ct);
    }
}
