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

    /// <summary>預設根目錄（%LocalAppData%/TenderSearch/data/）。供 UI 顯示「還原預設」用。</summary>
    string DefaultDataRoot { get; }

    /// <summary>
    /// 變更 DataRoot 並持久化到 bootstrap 設定檔，所有依賴 IDataPaths 的 repository
    /// 下次呼叫即會使用新路徑。傳入空字串或 null 視為還原預設。
    /// </summary>
    void ChangeRoot(string? newRoot);

    /// <summary>DataRoot 變更後觸發，參數為新 root。訂閱者可重建 watcher 等資源。</summary>
    event Action<string>? DataRootChanged;
}
