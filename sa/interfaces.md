# 主要介面與類別契約（Interfaces）

> 本文件列出每個專案內主要 interface 與類別簽章，僅定義契約不含實作。
> 對應實作由 csharp-expert 階段完成。
> 命名空間慣例：`Tender.<ProjectName>.<SubFolder>`。
> 產出日期：2026-05-08

---

## 1. `Tender.Core` — 領域服務契約

### 1.1 搜尋服務

```csharp
namespace Tender.Core.Search;

/// <summary>
/// 搜尋條件值物件，由 Desktop 的 ViewModel 組裝後傳入 SearchService。
/// 多項條件之間採 AND 邏輯。
/// </summary>
public sealed record SearchCriteria
{
    /// <summary>使用者搜尋框輸入字串，以空白分割為多個關鍵字（AND 命中、模糊查詢）。</summary>
    public string? KeywordQuery { get; init; }

    /// <summary>關鍵字按鈕命中項（沿用 Excel 既有，多個按鈕之間採 OR 命中）。</summary>
    public IReadOnlyList<string> ActiveKeywordButtons { get; init; } = Array.Empty<string>();

    /// <summary>招標方式（業務名稱），null 為不篩選。</summary>
    public string? TenderMethod { get; init; }

    /// <summary>採購性質，null 為不篩選。</summary>
    public string? ProcurementType { get; init; }

    /// <summary>公告日期區間（含端點），格式為民國年「115/05/06」。</summary>
    public string? AnnouncementDateFrom { get; init; }
    public string? AnnouncementDateTo { get; init; }

    /// <summary>截止投標日期區間（含端點），格式為民國年。</summary>
    public string? BidDeadlineFrom { get; init; }
    public string? BidDeadlineTo { get; init; }

    /// <summary>預算金額區間（含端點），單位為元。</summary>
    public long? BudgetMin { get; init; }
    public long? BudgetMax { get; init; }

    /// <summary>是否只看尚未截止（bidDeadline >= today）。</summary>
    public bool ShowActiveOnly { get; init; }
}

public enum SortKey
{
    None,
    AgencyName,
    TenderName,
    AnnouncementDate,
    BidDeadline,
    BudgetAmount,
}

public enum SortDirection { Ascending, Descending }

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
```

### 1.2 關鍵字命中

```csharp
namespace Tender.Core.Keywords;

/// <summary>
/// 關鍵字命中服務。
/// - 寫入端：爬蟲在去重後對每筆標案標註 matchedKeywords。
/// - 查詢端：日期查詢頁的關鍵字按鈕透過此服務判定是否符合。
/// </summary>
public interface IKeywordMatcher
{
    /// <summary>
    /// 對單筆標案計算所有命中的關鍵字。
    /// 比對規則：採模糊查詢（子字串包含即命中），依 KeywordItem.TargetField 決定比對欄位。
    /// </summary>
    IReadOnlyList<string> Match(TenderItem item, KeywordSet keywordSet);

    /// <summary>
    /// 判定一筆標案是否命中指定關鍵字（用於 UI 篩選按鈕點擊後的判定）。
    /// </summary>
    bool IsMatch(TenderItem item, string keyword, string targetField);
}
```

### 1.3 民國年/西元年轉換

```csharp
namespace Tender.Core.DateConversion;

public interface ITaiwanDateConverter
{
    /// <summary>「115/05/08」 → DateOnly(2026,5,8)。解析失敗回傳 null。</summary>
    DateOnly? RocToDateOnly(string rocDate);

    /// <summary>DateOnly(2026,5,8) → 「115/05/08」。</summary>
    string DateOnlyToRoc(DateOnly date);

    /// <summary>判斷民國年字串是否在指定西元年區間內（含端點）。</summary>
    bool IsRocDateInRange(string rocDate, DateOnly from, DateOnly to);
}
```

### 1.4 招標方式對應表

```csharp
namespace Tender.Core.Constants;

/// <summary>
/// 招標方式業務名稱與政府電子採購網實際 option/value 的對應表。
/// 第 11 節風險 R3：實際 option/value 需於 Phase 2 PoC 階段確認，本 class 為佔位符。
/// </summary>
public static class TenderMethodMapping
{
    /// <summary>業務名稱 → 政府網站 option value（PoC 後填入正確值）。</summary>
    public static IReadOnlyDictionary<string, string> BusinessNameToOptionValue { get; } =
        new Dictionary<string, string>
        {
            // TODO: PoC 階段確認下列 value
            { "公開招標", "TBD" },
            { "公開取得電子報價單", "TBD" },
            { "經公開評選或公開徵求之限制性招標", "TBD" },
        };

    /// <summary>網站文字 → 業務名稱反向對應（用於解析時正規化）。</summary>
    public static string NormalizeFromWebText(string webText);
}
```

---

## 2. `Tender.Storage` — 持久化契約

### 2.1 資料路徑

```csharp
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

    /// <summary>data/locks/crawler.lock，防重複執行。</summary>
    string CrawlerLockFile { get; }

    /// <summary>列出指定月份所有有資料的日期。</summary>
    IReadOnlyList<DateOnly> ListDaysWithData(int year, int month);
}
```

### 2.2 標案資料儲存

```csharp
namespace Tender.Storage.Repositories;

/// <summary>
/// 當日 tenders.json 讀寫，含合併、去重、原子替換。
/// </summary>
public interface ITenderRepository
{
    /// <summary>讀取指定日的快照，不存在回傳 null，損毀回傳特殊狀態。</summary>
    Task<DailyTenderSnapshot?> LoadAsync(DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// 將新爬到的標案合併進該日快照。
    /// - 以 SourcePk 去重。
    /// - 既有 + 新項目分類為 inserted/updated/skipped。
    /// - 寫入採暫存檔加原子替換（先寫 .tmp 再 File.Move）。
    /// </summary>
    /// <returns>合併結果統計（inserted/updated/skipped 筆數）。</returns>
    Task<MergeResult> MergeDailySnapshotAsync(
        DateOnly date,
        IReadOnlyList<TenderItem> incomingItems,
        DateTimeOffset now,
        CancellationToken ct = default);

    /// <summary>檢查指定日的 tenders.json 是否存在。</summary>
    bool Exists(DateOnly date);
}

public sealed record MergeResult(int InsertedCount, int UpdatedCount, int SkippedCount);
```

### 2.3 摘要與紀錄

```csharp
namespace Tender.Storage.Repositories;

public interface IDailySummaryRepository
{
    Task<DailySummary?> LoadAsync(DateOnly date, CancellationToken ct = default);

    /// <summary>原子寫入 summary.json。</summary>
    Task SaveAsync(DailySummary summary, CancellationToken ct = default);
}

public interface ICrawlRunLogRepository
{
    Task<CrawlRunLog?> LoadAsync(DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// 在 crawl-runs.json 末尾新增一筆 run 紀錄（若檔案不存在則建立）。
    /// 採讀取-合併-原子寫入流程。
    /// </summary>
    Task AppendRunAsync(DateOnly date, CrawlRun run, CancellationToken ct = default);
}

public interface IErrorLogWriter
{
    /// <summary>
    /// 對指定日的 errors.log 追加一行 JSON Lines 紀錄。
    /// </summary>
    Task AppendAsync(DateOnly date, ErrorLogEntry entry, CancellationToken ct = default);
}

public sealed record ErrorLogEntry(
    DateTimeOffset Timestamp,
    string Severity,        // "info" | "warning" | "error"
    string Source,          // "crawler" | "desktop" | "storage"
    string? RunId,
    string Message,
    string? ExceptionDetail,
    int? Page);
```

### 2.4 設定檔

```csharp
namespace Tender.Storage.Repositories;

public interface IKeywordsRepository
{
    Task<KeywordSet> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(KeywordSet set, CancellationToken ct = default);
}

public interface IUserMarksRepository
{
    Task<UserMarks> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(UserMarks marks, CancellationToken ct = default);
}

public interface IAppSettingsRepository
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
```

### 2.5 原子寫入器（內部共用）

```csharp
namespace Tender.Storage.Atomic;

/// <summary>
/// 暫存檔加原子替換策略的共用實作。
/// 寫入流程：寫 .tmp → fsync → File.Move(overwrite: true)。
/// </summary>
internal interface IAtomicJsonWriter
{
    Task WriteAsync<T>(string finalPath, T data, CancellationToken ct = default);
}
```

---

## 3. `Tender.Crawler` — 爬蟲契約

### 3.1 入口與命令列

```csharp
namespace Tender.Crawler;

public sealed record CrawlerArgs
{
    public required CrawlerMode Mode { get; init; }
    public required DateOnly TargetDate { get; init; }
}

public enum CrawlerMode
{
    Scheduled,    // --mode scheduled
    Catchup,      // --mode catchup
    Manual,       // --mode manual
    ManualRedo,   // --mode manual-redo
    Poc,          // --mode poc（Phase 2 風險驗證用）
}

/// <summary>
/// Tender.Crawler.exe 的入口，由桌面程式或 Task Scheduler 啟動。
/// Exit Code：
///   0 = success
///   1 = network failure
///   2 = parse failure
///   3 = io failure
///   4 = locked（另一個 run 進行中）
///   5 = invalid args
/// </summary>
public static class Program
{
    public static Task<int> Main(string[] args);
}
```

### 3.2 爬蟲流程協調器

```csharp
namespace Tender.Crawler.Application;

public interface ICrawlerOrchestrator
{
    /// <summary>
    /// 執行一次完整的爬蟲流程：查詢 → 解析 → 命中關鍵字 → 合併 → 寫摘要 → 寫 run 紀錄。
    /// 取得 crawler.lock，若已被持有則回傳 Skipped。
    /// </summary>
    Task<CrawlRun> RunAsync(CrawlerArgs args, CancellationToken ct = default);
}
```

### 3.3 爬蟲（HTTP 抓取）

```csharp
namespace Tender.Crawler.Spider;

public interface ICrawler
{
    /// <summary>
    /// 對政府電子採購網查詢指定日期、指定招標方式的標案，回傳原始 HTML 頁面集合。
    /// 內部負責分頁、重試、禮貌延遲、User-Agent 設定。
    /// </summary>
    /// <param name="targetDate">公告日期條件。</param>
    /// <param name="tenderMethods">招標方式業務名稱清單（內部會經 TenderMethodMapping 轉為網站 option value）。</param>
    /// <returns>分頁結果，每項包含原始 HTML 與來源 URL。失敗頁會以 ParseFailure 標示。</returns>
    Task<IReadOnlyList<FetchedPage>> FetchAsync(
        DateOnly targetDate,
        IReadOnlyList<string> tenderMethods,
        CancellationToken ct = default);
}

public sealed record FetchedPage(int PageNumber, string SourceUrl, string? Html, Exception? Error);
```

### 3.4 解析器

```csharp
namespace Tender.Crawler.Parsing;

public interface ITenderParser
{
    /// <summary>
    /// 將單頁 HTML 解析為 TenderItem 集合。
    /// 解析失敗時拋 ParseException，呼叫端負責記錄到 errors.log 並繼續其他頁。
    /// </summary>
    IReadOnlyList<TenderItem> Parse(string html, DateTimeOffset now);
}

public sealed class ParseException : Exception
{
    public int PageNumber { get; }
    public ParseException(int pageNumber, string message, Exception? inner = null);
}
```

### 3.5 摘要產生

```csharp
namespace Tender.Crawler.Application;

public interface IDailySummaryService
{
    /// <summary>
    /// 依據合併後的 tenders.json 與本次 run 結果，更新 summary.json。
    /// totalCount = 該日 items 總筆數；其餘欄位來自 run 結果。
    /// </summary>
    Task GenerateAsync(DateOnly date, CrawlRun lastRun, CancellationToken ct = default);
}
```

### 3.6 進度通報（stdout JSON Lines）

```csharp
namespace Tender.Crawler.Reporting;

/// <summary>
/// 將進度事件以 JSON Lines 格式寫到 stdout，供桌面程式解析顯示進度條。
/// </summary>
public interface IProgressReporter
{
    void Report(ProgressEvent evt);
}

public sealed record ProgressEvent(string Stage, string Message, int? PageNumber, double? PercentComplete);
```

---

## 4. `Tender.Desktop` — UI 層契約

### 4.1 ViewModel 主要類別

```csharp
namespace Tender.Desktop.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    public MonthlyCalendarViewModel CalendarVm { get; }
    public DailyQueryViewModel? CurrentDailyQueryVm { get; private set; }
    public SideSummaryViewModel SideSummaryVm { get; }

    [RelayCommand] private Task LoadAsync();
    [RelayCommand] private Task RunCrawlerNowAsync();
    [RelayCommand] private Task NavigateToDayAsync(DateOnly date);
    [RelayCommand] private Task NavigateBackToCalendarAsync();
}

public partial class MonthlyCalendarViewModel : ObservableObject
{
    [ObservableProperty] private int _year;
    [ObservableProperty] private int _month;
    [ObservableProperty] private MonthlyCalendarView? _view;

    [RelayCommand] private Task LoadMonthAsync();
    [RelayCommand] private void GoPreviousMonth();
    [RelayCommand] private void GoNextMonth();
    [RelayCommand] private void GoToCurrentMonth();
    [RelayCommand] private Task ShowFailedSummaryAsync(DateOnly date);
}

public partial class DailyQueryViewModel : ObservableObject
{
    public DateOnly Date { get; }
    [ObservableProperty] private IReadOnlyList<TenderItem> _items;
    [ObservableProperty] private SearchCriteria _criteria;
    [ObservableProperty] private SortKey _sortKey;
    [ObservableProperty] private SortDirection _sortDirection;

    [RelayCommand] private Task LoadAsync();
    [RelayCommand] private void ApplySearch();
    [RelayCommand] private void ToggleKeywordButton(string keyword);
    [RelayCommand] private void ClearKeywordFilter();
    [RelayCommand] private Task GoPreviousDayAsync();
    [RelayCommand] private Task GoNextDayAsync();
    [RelayCommand] private Task ExportAsync();
    [RelayCommand] private void OpenDetail(TenderItem item);
}
```

### 4.2 Application Services（Desktop 內部協調）

```csharp
namespace Tender.Desktop.Services;

public interface IMonthlyCalendarService
{
    /// <summary>
    /// 載入指定月份的行事曆視圖：列出該月所有日資料夾，讀取每日 summary.json，組成 MonthlyCalendarView。
    /// 不解析 tenders.json。
    /// </summary>
    Task<MonthlyCalendarView> LoadMonthAsync(int year, int month, CancellationToken ct = default);

    /// <summary>重新讀取單日 summary.json，用於「立即更新」完成後刷新行事曆。</summary>
    Task<MonthlyCalendarDay> RefreshDayAsync(DateOnly date, CancellationToken ct = default);
}

public interface ICrawlerLauncher
{
    /// <summary>
    /// 啟動 Tender.Crawler.exe 子程序並等待結束。
    /// 透過 stdout 取得 ProgressEvent 並轉發給呼叫端。
    /// </summary>
    Task<int> LaunchAsync(
        CrawlerMode mode,
        DateOnly targetDate,
        IProgress<ProgressEvent>? progress,
        CancellationToken ct = default);
}

public interface IMissedRunDetector
{
    /// <summary>
    /// 在桌面程式啟動時呼叫，偵測今日是否有 missed run（17:00 已過但 summary.json 不存在）。
    /// 若有 missed → 觸發 ICrawlerLauncher.LaunchAsync(Catchup, today)。
    /// </summary>
    Task<MissedRunResult> CheckAndCatchupAsync(CancellationToken ct = default);
}

public sealed record MissedRunResult(bool WasMissed, bool CatchupTriggered, string? Reason);

public interface IExcelExporter
{
    /// <summary>
    /// 匯出標案集合為 .xlsx。
    /// 包含「機關名稱：標案名稱」合併欄位，標案名稱與檢視欄保留超連結。
    /// </summary>
    Task ExportAsync(
        IReadOnlyList<TenderItem> items,
        string savePath,
        CancellationToken ct = default);
}

public interface IBrowserLauncher
{
    /// <summary>以使用者預設瀏覽器開啟 URL。</summary>
    void Open(string url);
}

public interface ISaveFileDialogService
{
    /// <summary>
    /// 顯示另存新檔對話框，回傳使用者選擇的路徑；取消則回傳 null。
    /// 預設副檔名 .xlsx，預設檔名格式建議為 「標案_yyyyMMdd.xlsx」。
    /// </summary>
    string? ShowSaveAsXlsx(string suggestedFileName);
}

public interface IErrorSummaryDialog
{
    /// <summary>顯示錯誤摘要視窗，內容來自 summary.json 的 errorMessage。</summary>
    Task ShowAsync(DateOnly date, string errorMessage, string errorsLogPath);
}
```

---

## 5. `Tender.Installer` — 安裝程式契約

WiX 不寫 C# 介面，但下列為 Custom Action 的 C# 簽章（若採用 Managed Custom Action）：

```csharp
namespace Tender.Installer.CustomActions;

public static class TaskSchedulerActions
{
    /// <summary>
    /// 在當前使用者下建立每日 17:00 的排程任務。
    /// 使用 Microsoft.Win32.TaskScheduler（WiX Toolset 的 TaskSchedulerExtension）或 schtasks.exe。
    /// 任務 XML 設定 StartWhenAvailable = true 以支援 missed run 補跑。
    /// </summary>
    [CustomAction]
    public static ActionResult CreateDailyTask(Session session);

    /// <summary>解除安裝時移除排程任務。</summary>
    [CustomAction]
    public static ActionResult RemoveDailyTask(Session session);
}

public static class DataDirectoryActions
{
    /// <summary>
    /// 建立資料根目錄 %LocalAppData%/TenderSearch/data/。
    /// 若已存在則保留既有內容（不覆蓋）。
    /// 同時放入 settings/keywords.json 預設清單。
    /// </summary>
    [CustomAction]
    public static ActionResult EnsureDataRoot(Session session);
}
```

---

## 6. 共用例外類型

```csharp
namespace Tender.Core.Exceptions;

/// <summary>網路或外部資源不可用。</summary>
public class CrawlNetworkException : Exception { /* ... */ }

/// <summary>JSON 檔案損毀無法解析。</summary>
public class CorruptedDataException : Exception
{
    public string FilePath { get; }
}

/// <summary>另一個 run 正在進行中（已持有 crawler.lock）。</summary>
public class CrawlerLockedException : Exception { /* ... */ }
```

---

## 7. DI 註冊建議（依專案）

```csharp
// Tender.Core
services.AddSingleton<ITaiwanDateConverter, TaiwanDateConverter>();
services.AddSingleton<IKeywordMatcher, KeywordMatcher>();
services.AddSingleton<ISearchService, SearchService>();

// Tender.Storage
services.AddSingleton<IDataPaths, LocalAppDataPaths>();
services.AddSingleton<IAtomicJsonWriter, AtomicJsonWriter>();
services.AddSingleton<ITenderRepository, TenderRepository>();
services.AddSingleton<IDailySummaryRepository, DailySummaryRepository>();
services.AddSingleton<ICrawlRunLogRepository, CrawlRunLogRepository>();
services.AddSingleton<IErrorLogWriter, ErrorLogWriter>();
services.AddSingleton<IKeywordsRepository, KeywordsRepository>();
services.AddSingleton<IUserMarksRepository, UserMarksRepository>();
services.AddSingleton<IAppSettingsRepository, AppSettingsRepository>();

// Tender.Crawler
services.AddHttpClient<ICrawler, HttpClientCrawler>();
services.AddSingleton<ITenderParser, AngleSharpTenderParser>();
services.AddSingleton<IDailySummaryService, DailySummaryService>();
services.AddSingleton<ICrawlerOrchestrator, CrawlerOrchestrator>();
services.AddSingleton<IProgressReporter, JsonLinesProgressReporter>();

// Tender.Desktop
services.AddSingleton<IMonthlyCalendarService, MonthlyCalendarService>();
services.AddSingleton<ICrawlerLauncher, CrawlerLauncher>();
services.AddSingleton<IMissedRunDetector, MissedRunDetector>();
services.AddSingleton<IExcelExporter, ClosedXmlExcelExporter>();
services.AddSingleton<IBrowserLauncher, ProcessStartBrowserLauncher>();
services.AddSingleton<ISaveFileDialogService, WpfSaveFileDialogService>();
services.AddSingleton<IErrorSummaryDialog, WpfErrorSummaryDialog>();
services.AddSingleton<ShellViewModel>();
```
