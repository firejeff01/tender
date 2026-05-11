# MVP 實作順序建議（Implementation Plan）

> 本文件提供 csharp-expert 階段的實作順序、每階段任務與 Acceptance Criteria。
> 依照「最低風險先驗證、底層先建立、UI 在最後」的原則排序。
> 對應 PM 範圍：`pm/tender_software_requirements.md` 第 10 節 MVP。
> 產出日期：2026-05-08

---

## Phase 0：Solution 骨架（0.5 day）

**目標**：建立可編譯通過的 solution 結構，確認專案相依關係正確。

### Tasks

1. 依 `dev_setup.md` 第 3 節執行 `dotnet new` 與 `dotnet sln add`。
2. 安裝關鍵 NuGet 套件（暫不安裝 Playwright 與 Polly 以外的細節，後續階段補上）。
3. 把 `sa/engineer_features/*.feature` 與 `sa/step_definitions/*.cs` 複製到 `tests/Tender.AcceptanceTests/`。
4. 移除 `dotnet new xunit` 預設產生的 `UnitTest1.cs`。
5. 在 `Tender.Desktop/App.xaml.cs` 暫時加 `MessageBox.Show("Hello")` 的最簡 ViewModel 確認 WPF 能跑。

### Acceptance Criteria

- `dotnet build` 對整個 solution 成功，無 warning。
- `dotnet test` 跑得起來（即使所有 acceptance test 都 NotImplemented 失敗，至少 collector 正常）。
- `dotnet run --project src/Tender.Desktop` 能開啟空視窗。

---

## Phase 1：Tender.Core 領域模型 + Tender.Storage 基礎設施（1.5 ~ 2 days）

**目標**：完成最底層、可獨立測試的領域模型與 JSON 讀寫。

### 1.1 Tender.Core

**Tasks**：
- 實作所有 record / class（依 `data_models.md`）：
  - `TenderItem`、`DailyTenderSnapshot`、`DailySummary`、`CrawlRun`、`CrawlRunLog`、`MonthlyCalendarView`、`MonthlyCalendarDay`
  - `KeywordSet`、`KeywordGroup`、`KeywordItem`、`UserMarks`、`UserMark`、`AppSettings`
  - 列舉：`RunStatus`、`TriggerSource`、`SortKey`、`SortDirection`
- 實作 `ITaiwanDateConverter` + `TaiwanDateConverter`
- 實作 `IClock` 介面 + `SystemClock`
- 實作 `TenderMethodMapping` 常數類別（值暫填 `"TBD"`，待 Phase 2 PoC 補正）
- 實作 `IKeywordMatcher` + `KeywordMatcher`（純邏輯）
- 實作 `ISearchService` + `SearchService`（純邏輯）
- 定義例外類別：`CrawlNetworkException`、`CorruptedDataException`、`CrawlerLockedException`、`ParseException`

**Tests** (`Tender.Core.Tests`)：
- `TaiwanDateConverter` 雙向轉換測試（含閏年、跨月、邊界）
- `KeywordMatcher.Match` 對 8 個 keyword group 的命中測試
- `SearchService.Search` 對所有 SearchCriteria 欄位的單測 + 組合測試（AND 邏輯、排序、null 值排尾）

### 1.2 Tender.Storage

**Tasks**：
- 實作 `IDataPaths` + `LocalAppDataPaths`
- 實作 `IAtomicJsonWriter` + `AtomicJsonWriter`（暫存檔 + File.Move）
- 實作所有 Repository：`ITenderRepository`、`IDailySummaryRepository`、`ICrawlRunLogRepository`、`IErrorLogWriter`、`IKeywordsRepository`、`IUserMarksRepository`、`IAppSettingsRepository`
- 實作 `MergeDailySnapshotAsync` 的去重 + 分類為 inserted/updated/skipped 邏輯
- 內建 `default-keywords.json` 嵌入資源，供首次執行時建立

**Tests** (`Tender.Storage.Tests`)：
- 對每個 Repository 的 LoadAsync / SaveAsync 進行雙向測試（用 Path.GetTempPath 做隔離）
- `AtomicJsonWriter` 測試：
  - 正常寫入後內容正確
  - 寫入過程拋例外時 `.tmp` 殘留但 `final` 不存在或為原值
  - 並發寫入兩次不應 race
- `MergeDailySnapshotAsync` 的所有合併情境（含 PM Gherkin `daily_crawl.feature` 第 6 個 Scenario）

### Acceptance Criteria

- `Tender.Core.Tests` 與 `Tender.Storage.Tests` 全綠。
- `sa/engineer_features/crawl_logging.feature` 中**只涉及 Storage 層**的 Scenario（如「同日資料夾尚未存在時自動建立」「errors.log 跨日不互相覆蓋」）的 Reqnroll 測試可以開始通過。

---

## Phase 2：Tender.Crawler PoC + 完整實作（2 ~ 3 days）

**目標**：先驗證政府網站可達（**R1 風險最關鍵**），再完成完整爬蟲流程。

### 2.1 PoC（半天，**最高優先**）

**Tasks**：
- 在 `Tender.Crawler` 內建立最小 spike：用 `HttpClient` GET `https://web.pcc.gov.tw/prkms/tender/common/advanced/readTenderAdvanced` 並列印前 1000 字元 HTML。
- 嘗試對「公開招標 + 公告日期 = today」送出查詢，觀察回應結構。
- 用 AngleSharp 解析回應，列印「能不能取出至少一筆標案的 tenderName」。
- 命令列：`dotnet run --project src/Tender.Crawler -- --mode poc --target-date 2026-05-08`

**Acceptance Criteria**：
- 任一情境成立則繼續：
  - **A. 能取得標案資料**：HttpClient + AngleSharp 路線可行 → 進 2.2 用 HttpClient 實作。
  - **B. 取不到標案資料（空殼 HTML / JS-only）**：→ 引入 `Microsoft.Playwright`，2.2 用 Playwright 實作。
- **同時記錄三件事**：
  1. 政府網站 HTTP 回應 Header（特別是 `Set-Cookie`、`X-Frame-Options`）
  2. 「公開招標」、「公開取得報價單或企劃書」、「經公開評選或公開徵求之限制性招標」三者實際的 option/value（補回 `TenderMethodMapping`）
  3. 標案詳情頁的 `pk` 參數格式

### 2.2 完整爬蟲實作

**Tasks**：
- 實作 `ICrawler` + `HttpClientCrawler`（或 `PlaywrightCrawler`，依 PoC 結果）
  - 含 Polly 重試策略（指數退避，最多 3 次）
  - 禮貌延遲（每分頁 1.5 秒，可由 AppSettings 調整）
  - User-Agent 設定
- 實作 `ITenderParser` + `AngleSharpTenderParser`
  - 對缺欄位採 nullable，不丟整批
- 實作 `IDailySummaryService`
- 實作 `ICrawlerOrchestrator` + `CrawlerOrchestrator`
  - 取得 `crawler.lock` 防重複執行
  - 串接 ICrawler → ITenderParser → IKeywordMatcher → ITenderRepository.MergeDailySnapshotAsync → IDailySummaryService → ICrawlRunLogRepository.AppendRunAsync
  - 例外處理：依架構文件第 8 節分類
- 實作 `IProgressReporter` + `JsonLinesProgressReporter`（stdout）
- 實作命令列入口（System.CommandLine）：`--mode`、`--target-date`、`--engine`（覆寫 AppSettings）

**Tests** (`Tender.Crawler.Tests`)：
- `AngleSharpTenderParser` 對 sample HTML（fixture）的解析測試
- `CrawlerOrchestrator` 整合測試：用 Mock ICrawler 驗證所有 Scenario：
  - `daily_crawl.feature` 全部 Scenario
  - `crawl_logging.feature` 中跟 Orchestrator 流程相關的 Scenario

### Acceptance Criteria

- PoC 階段確認可行性。
- `daily_crawl.feature` 與 `crawl_logging.feature` 中 Reqnroll 測試全綠（不含 UI 部分）。
- 手動執行 `dotnet run --project src/Tender.Crawler -- --mode manual --target-date 今天` 能在本機產生真實 `tenders.json`。

---

## Phase 3：Tender.Desktop UI（行事曆 → 查詢頁）（3 ~ 4 days）

**目標**：完成首頁行事曆與日期查詢頁的 MVP UI。

### 3.1 應用層服務

**Tasks**：
- `IMonthlyCalendarService` + `MonthlyCalendarService`
- `ICrawlerLauncher` + `CrawlerLauncher`（Process.Start + stdout 解析）
- `IMissedRunDetector` + `MissedRunDetector`（桌面啟動偵測補跑）
- `IBrowserLauncher` + `ProcessStartBrowserLauncher`
- `ISaveFileDialogService` + `WpfSaveFileDialogService`（Microsoft.Win32.SaveFileDialog）
- `IErrorSummaryDialog` + `WpfErrorSummaryDialog`

### 3.2 ViewModel

**Tasks**：
- `ShellViewModel`、`MonthlyCalendarViewModel`、`SideSummaryViewModel`、`DailyQueryViewModel`
- 使用 `CommunityToolkit.Mvvm` 的 `[ObservableProperty]` 與 `[RelayCommand]`
- 在 `App.xaml.cs` 建立 `IHost`，註冊所有 DI

### 3.3 View（XAML）

**Tasks**：
- `ShellWindow.xaml`：主視窗 + ContentControl 切換 CalendarView / DailyQueryView
- `MonthlyCalendarView.xaml`：自訂行事曆控制項
  - 用 `UniformGrid` 7 行排列，每格綁定 `MonthlyCalendarDay`
  - 今日 highlight、無資料 grey out、失敗日警示符號
- `DailyQueryView.xaml`：
  - 上方工具列（搜尋、招標方式、採購性質、預算區間、匯出）
  - 左側篩選面板（關鍵字按鈕 + 地區/機關）
  - 中央 DataGrid（綁 FilteredItems）
  - 右側詳情面板（綁定選取的 TenderItem）
- `SideSummary.xaml`（嵌入 ShellWindow 側邊）
- `ErrorSummaryDialog.xaml`（彈窗）

**Tests** (`Tender.AcceptanceTests`)：
- `monthly_calendar.feature`、`daily_query.feature`、`keyword_filter.feature` 全部 Scenario 通過

### Acceptance Criteria

- 開啟桌面程式可看到行事曆，點擊 Phase 2 產生的真實資料對應日期，能進入查詢頁、看到資料、套用搜尋與篩選。
- `monthly_calendar.feature`、`daily_query.feature`、`keyword_filter.feature` 全綠。

---

## Phase 4：匯出 Excel + 排程整合（1.5 ~ 2 days）

### 4.1 Excel 匯出

**Tasks**：
- `IExcelExporter` + `ClosedXmlExcelExporter`
- 表頭固定欄位：標案名稱、招標方式、採購性質、公告日期、截止投標、預算金額、機關名稱、檢視連結、機關名稱：標案名稱、命中關鍵字
- 「標案名稱」與「檢視連結」與「機關名稱：標案名稱」欄位寫入 hyperlink
- 預算金額為 number、日期為 text
- DailyQueryViewModel.ExportCommand 串接 SaveFileDialog → ExcelExporter

**Tests** (`Tender.AcceptanceTests`)：
- `export_excel.feature` 全部 Scenario 通過

### 4.2 桌面啟動偵測補跑（整合）

**Tasks**：
- `App.OnStartup` 中呼叫 `IMissedRunDetector.CheckAndCatchupAsync`
- 透過 ViewModel 顯示「正在補跑」進度條
- 補跑完成後刷新 ShellViewModel.CalendarVm

**Tests**：
- `daily_crawl.feature` 中的補跑 Scenario（已在 Phase 2 寫測試，此階段確保整合通過）

### Acceptance Criteria

- 在查詢頁點「匯出」可成功產生 .xlsx，內容含超連結。
- 模擬「17:00 缺執行 + 22:30 開機」情境，桌面程式啟動會自動觸發補跑。
- `export_excel.feature` 全綠。

---

## Phase 5：Tender.Installer（WiX MSI）（1.5 ~ 2 days）

**目標**：產出可發布的 MSI，含資料目錄、捷徑、Task Scheduler 任務。

### Tasks

- 建立 `Tender.Installer` (.wixproj)：
  - `Product.wxs`：產品資訊、UI 參考
  - `Files.wxs`：包含 `publish/desktop/*` 與 `publish/crawler/*`
  - `Shortcuts.wxs`：桌面與開始功能表捷徑
  - `Registry.wxs`：寫入安裝路徑（為了 MSI uninstall）
- 建立 `Tender.Installer.CustomActions` 專案（.NET Framework 4.x，因為 Managed CA 仍依賴 framework；或採用 wix v4 的 BootstrapperApplication 內嵌 .NET 8）
  - `DataDirectoryActions.EnsureDataRoot`
  - `TaskSchedulerActions.CreateDailyTask` / `RemoveDailyTask`
  - 任務 XML 設定：`StartWhenAvailable = true`、`RunLevel = LeastPrivilege`
- 在 Product.wxs 串接 Custom Action 至 InstallExecuteSequence

### Tests

- `installation.feature` 中的 Custom Action 單元測試（用 InMemoryTaskSchedulerAdapter）
- 手動 QA 驗證真實 MSI 安裝流程：
  - 全新安裝 → 桌面捷徑 / 資料目錄 / 排程任務皆建立
  - 重新安裝 → 既有資料保留
  - 解除安裝 → 排程移除、資料保留
  - 等待至 17:00 觀察 Task Scheduler 自動觸發

### Acceptance Criteria

- 產出單檔 `TenderSearch.msi`，雙擊可完成安裝。
- 安裝後可從桌面捷徑開啟程式、可從 Task Scheduler 看到 `TenderSearch.DailyCrawl` 任務。
- `installation.feature` Custom Action 單元測試全綠（端到端 QA 由人工驗證並記錄）。

---

## 跨階段持續任務

- **每完成一個 Phase**：Reqnroll Acceptance Tests 對應的 Scenario 應全綠。
- **每完成一個 Phase**：更新 `errors.log` 與 `crawl-runs.json` 在真實環境的觀察，回饋給 SA 與 PM 確認。
- **R1 風險持續監控**：Phase 2 PoC 後若決定切換 Playwright，更新 `architecture.md` 9.1 節並通知整個團隊。

---

## 預估總時程

| Phase | 預估工時 |
|---|---|
| Phase 0 | 0.5 day |
| Phase 1 | 1.5 ~ 2 days |
| Phase 2 | 2 ~ 3 days |
| Phase 3 | 3 ~ 4 days |
| Phase 4 | 1.5 ~ 2 days |
| Phase 5 | 1.5 ~ 2 days |
| **合計** | **10 ~ 13.5 days** |

> 不含人工 QA、文件撰寫、跨團隊溝通。一人 full-time 投入估計 2 ~ 3 週。

---

## 風險再評估時點

| 時點 | 應評估的風險 |
|---|---|
| Phase 2 PoC 後 | R1（網站可達） + R3（option/value） |
| Phase 3 結束 | R2（反爬蟲，是否觀察到 IP 被擋） |
| Phase 5 安裝測試 | R6（Per-user Task Scheduler 權限）+ R8（%LocalAppData% 寫入權限） |
