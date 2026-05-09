namespace Tender.Storage.Paths;

/// <summary>
/// 集中管理所有檔案路徑規則。實作從 IAppSettingsRepository 或環境變數取得 root。
/// </summary>
public interface IDataPaths
{
    /// <summary>資料根目錄，預設 %LocalAppData%/TenderSearch/data/。</summary>
    string DataRoot { get; }

    /// <summary>data/yyyy-MM/yyyy-MM-dd/ 該日資料夾。</summary>
    string DailyFolder(DateOnly date);

    /// <summary>data/yyyy-MM/yyyy-MM-dd/tenders.json。</summary>
    string TendersFile(DateOnly date);

    /// <summary>data/yyyy-MM/yyyy-MM-dd/summary.json。</summary>
    string SummaryFile(DateOnly date);

    /// <summary>data/yyyy-MM/yyyy-MM-dd/crawl-runs.json。</summary>
    string CrawlRunsFile(DateOnly date);

    /// <summary>data/yyyy-MM/yyyy-MM-dd/errors.log。</summary>
    string ErrorsLogFile(DateOnly date);

    /// <summary>data/settings/keywords.json。</summary>
    string KeywordsFile { get; }

    string UserMarksFile { get; }
    string AppSettingsFile { get; }
    string SavedSearchesFile { get; }

    /// <summary>data/locks/crawler.lock，防重複執行。</summary>
    string CrawlerLockFile { get; }

    /// <summary>列出指定月份所有有資料的日期。</summary>
    IReadOnlyList<DateOnly> ListDaysWithData(int year, int month);
}
