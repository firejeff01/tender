# 資料模型（Data Models）

> 本文件定義 `Tender.Core` 的領域模型，以及 `Tender.Storage` 對應的 JSON Schema。
> 對應 PM Aggregates：`DailyTenderSnapshot`、`DailySummary`、`CrawlRunLog`、`MonthlyCalendarView`、`KeywordSet`、`UserMarks`、`AppSettings`。
> 所有時間欄位採 ISO 8601 含時區（`yyyy-MM-ddTHH:mm:sszzz`）。
> 民國年欄位（`announcementDate`、`bidDeadline`）保留來源網站格式 `115/05/08`，由 UI 與匯出時透過 `ITaiwanDateConverter` 處理。
> 產出日期：2026-05-08

---

## 1. 領域模型總覽

| 模型 | 對應 JSON 檔案 | 所在專案 |
|---|---|---|
| `TenderItem` | `tenders.json` 的 `items[]` 元素 | `Tender.Core` |
| `DailyTenderSnapshot` | `tenders.json` 整體 | `Tender.Core` |
| `DailySummary` | `summary.json` | `Tender.Core` |
| `CrawlRun` | `crawl-runs.json` 的 `runs[]` 元素 | `Tender.Core` |
| `CrawlRunLog` | `crawl-runs.json` 整體 | `Tender.Core` |
| `MonthlyCalendarView` | （彙總，無對應檔） | `Tender.Core` |
| `MonthlyCalendarDay` | （彙總） | `Tender.Core` |
| `KeywordGroup` / `KeywordItem` / `KeywordSet` | `settings/keywords.json` | `Tender.Core` |
| `UserMark` / `UserMarks` | `settings/user-marks.json` | `Tender.Core` |
| `AppSettings` | `settings/app-settings.json` | `Tender.Core` |
| `TenderMethod`（enum） | （常數對應表） | `Tender.Core` |
| `ProcurementType`（enum） | （常數對應表） | `Tender.Core` |
| `RunStatus`（enum） | - | `Tender.Core` |
| `TriggerSource`（enum） | - | `Tender.Core` |

> **DTO vs Domain Model 策略**：首版不分 DTO 與 Domain Model。`Tender.Core` 內的 record/class 直接以 `[JsonPropertyName]` 標註對應 JSON 欄位，由 `Tender.Storage` 直接序列化/反序列化。若未來 JSON schema 與內部模型分歧，再抽離 DTO 層。

---

## 2. C# 類別定義

### 2.1 `TenderItem`（標案資料）

```csharp
namespace Tender.Core.Models;

/// <summary>
/// 單筆標案資料，對應 tenders.json 內 items[] 元素。
/// 同日內以 SourcePk 為唯一鍵。
/// </summary>
public sealed record TenderItem
{
    /// <summary>
    /// 政府電子採購網標案唯一識別碼，建議取自 detail URL 的 pk 參數。
    /// 例：「NzEyMTUxODc=」（base64 字串）
    /// </summary>
    [JsonPropertyName("sourcePk")]
    public required string SourcePk { get; init; }

    /// <summary>機關名稱。</summary>
    [JsonPropertyName("agencyName")]
    public required string AgencyName { get; init; }

    /// <summary>機關代碼。</summary>
    [JsonPropertyName("agencyCode")]
    public string? AgencyCode { get; init; }

    /// <summary>標案名稱。</summary>
    [JsonPropertyName("tenderName")]
    public required string TenderName { get; init; }

    /// <summary>標案案號。</summary>
    [JsonPropertyName("tenderNo")]
    public string? TenderNo { get; init; }

    /// <summary>
    /// 招標方式（保留來源網站文字，例：「公開招標」、「公開取得報價單或企劃書」）。
    /// 業務分類請參考 TenderMethod enum 的對應表。
    /// </summary>
    [JsonPropertyName("tenderMethod")]
    public required string TenderMethod { get; init; }

    /// <summary>採購性質（例：「財物類」、「勞務類」、「工程類」）。</summary>
    [JsonPropertyName("procurementType")]
    public string? ProcurementType { get; init; }

    /// <summary>
    /// 公告日期，民國年格式（例：「115/05/08」）。保留來源格式以利匯出再用 Excel 篩選。
    /// 解析為西元年請使用 ITaiwanDateConverter。
    /// </summary>
    [JsonPropertyName("announcementDate")]
    public required string AnnouncementDate { get; init; }

    /// <summary>截止投標日期，民國年格式。</summary>
    [JsonPropertyName("bidDeadline")]
    public string? BidDeadline { get; init; }

    /// <summary>預算金額，單位為新台幣元。null 表示未公告金額或解析失敗。</summary>
    [JsonPropertyName("budgetAmount")]
    public long? BudgetAmount { get; init; }

    /// <summary>標案詳情頁 URL。例：https://web.pcc.gov.tw/prkms/urlSelector/common/tpam?pk=...</summary>
    [JsonPropertyName("detailUrl")]
    public required string DetailUrl { get; init; }

    /// <summary>
    /// 命中關鍵字清單，由 IKeywordMatcher 在去重後標註。
    /// 對應 PM 的 keyword_filter feature。
    /// </summary>
    [JsonPropertyName("matchedKeywords")]
    public IReadOnlyList<string> MatchedKeywords { get; init; } = Array.Empty<string>();

    /// <summary>該筆首次寫入此日 tenders.json 的時間。</summary>
    [JsonPropertyName("createdAt")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>該筆最近一次被擷取到的時間。同日重抓時更新此值，CreatedAt 不變。</summary>
    [JsonPropertyName("lastSeenAt")]
    public required DateTimeOffset LastSeenAt { get; init; }
}
```

### 2.2 `DailyTenderSnapshot`

```csharp
namespace Tender.Core.Models;

/// <summary>
/// 當日標案快照，對應 data/yyyy-MM/yyyy-MM-dd/tenders.json。
/// </summary>
public sealed record DailyTenderSnapshot
{
    /// <summary>該快照的資料所屬日期（西元年），例：「2026-05-08」。</summary>
    [JsonPropertyName("date")]
    public required string Date { get; init; }

    /// <summary>本檔案產生時間。</summary>
    [JsonPropertyName("generatedAt")]
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>資料來源 URL（首版固定為政府電子採購網首頁）。</summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>該日標案清單，以 SourcePk 為唯一鍵。</summary>
    [JsonPropertyName("items")]
    public required IReadOnlyList<TenderItem> Items { get; init; }
}
```

### 2.3 `DailySummary`

```csharp
namespace Tender.Core.Models;

/// <summary>
/// 當日摘要，對應 data/yyyy-MM/yyyy-MM-dd/summary.json。
/// 月份行事曆首頁優先讀取此檔，不解析 tenders.json。
/// </summary>
public sealed record DailySummary
{
    [JsonPropertyName("date")]
    public required string Date { get; init; }

    /// <summary>該日標案總筆數（去重後）。</summary>
    [JsonPropertyName("totalCount")]
    public required int TotalCount { get; init; }

    /// <summary>最近一次該日爬蟲執行狀態。</summary>
    [JsonPropertyName("lastRunStatus")]
    public required RunStatus LastRunStatus { get; init; }

    /// <summary>最近一次該日爬蟲執行完成時間（成功或失敗皆更新）。</summary>
    [JsonPropertyName("lastRunAt")]
    public required DateTimeOffset LastRunAt { get; init; }

    /// <summary>最近一次新增筆數。</summary>
    [JsonPropertyName("insertedCount")]
    public int InsertedCount { get; init; }

    /// <summary>最近一次更新筆數（同 sourcePk 重新出現）。</summary>
    [JsonPropertyName("updatedCount")]
    public int UpdatedCount { get; init; }

    /// <summary>最近一次略過筆數（已存在且無變更）。</summary>
    [JsonPropertyName("skippedCount")]
    public int SkippedCount { get; init; }

    /// <summary>失敗或部分失敗的錯誤摘要，成功且無部分錯誤時為 null。</summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}
```

### 2.4 `CrawlRun` / `CrawlRunLog`

```csharp
namespace Tender.Core.Models;

/// <summary>
/// 單次爬蟲執行紀錄。
/// </summary>
public sealed record CrawlRun
{
    /// <summary>執行 ID，建議格式 yyyyMMdd-HHmmss（例：「20260508-170000」）。</summary>
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    /// <summary>本次目標查詢日期（公告日期條件）。</summary>
    [JsonPropertyName("targetDate")]
    public required string TargetDate { get; init; }

    /// <summary>觸發來源：scheduled/catchup/manual/manual-redo。</summary>
    [JsonPropertyName("triggerSource")]
    public required TriggerSource TriggerSource { get; init; }

    [JsonPropertyName("startedAt")]
    public required DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("finishedAt")]
    public required DateTimeOffset FinishedAt { get; init; }

    [JsonPropertyName("status")]
    public required RunStatus Status { get; init; }

    [JsonPropertyName("insertedCount")]
    public int InsertedCount { get; init; }

    [JsonPropertyName("updatedCount")]
    public int UpdatedCount { get; init; }

    [JsonPropertyName("skippedCount")]
    public int SkippedCount { get; init; }

    /// <summary>錯誤摘要（失敗時必填，部分失敗時填提示，成功時為 null）。</summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 該日所有執行紀錄的集合，對應 crawl-runs.json。
/// </summary>
public sealed record CrawlRunLog
{
    [JsonPropertyName("date")]
    public required string Date { get; init; }

    /// <summary>該日所有 run 紀錄，依 StartedAt 升冪排列。</summary>
    [JsonPropertyName("runs")]
    public required IReadOnlyList<CrawlRun> Runs { get; init; }
}
```

### 2.5 列舉（Enums）

```csharp
namespace Tender.Core.Models;

public enum RunStatus
{
    [JsonStringEnumMemberName("success")] Success,
    [JsonStringEnumMemberName("failed")] Failed,
    [JsonStringEnumMemberName("skipped")] Skipped,
}

public enum TriggerSource
{
    /// <summary>每日 17:00 Task Scheduler 預設執行。</summary>
    [JsonStringEnumMemberName("scheduled")] Scheduled,
    /// <summary>桌面程式啟動偵測或 Task Scheduler missed run 補跑。</summary>
    [JsonStringEnumMemberName("catchup")] Catchup,
    /// <summary>使用者手動「立即更新」抓當天。</summary>
    [JsonStringEnumMemberName("manual")] Manual,
    /// <summary>使用者對指定日「重新抓取此日」。</summary>
    [JsonStringEnumMemberName("manual-redo")] ManualRedo,
}
```

### 2.6 `MonthlyCalendarView`（彙總視圖）

```csharp
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
```

### 2.7 `KeywordSet`

```csharp
namespace Tender.Core.Models;

public sealed record KeywordSet
{
    [JsonPropertyName("groups")]
    public required IReadOnlyList<KeywordGroup> Groups { get; init; }
}

public sealed record KeywordGroup
{
    /// <summary>分類名稱（資訊系統／XR/AI／資安/無障礙／ESG/碳管理／業務雜項／智慧管考／倉儲自動化／地區／指定機關）。</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("items")]
    public required IReadOnlyList<KeywordItem> Items { get; init; }
}

public sealed record KeywordItem
{
    [JsonPropertyName("keyword")]
    public required string Keyword { get; init; }

    /// <summary>
    /// 命中欄位：tenderName / agencyName / any（兩者皆比對）。
    /// 地區關鍵字採 any（沿用 Excel 巨集行為）。
    /// </summary>
    [JsonPropertyName("targetField")]
    public required string TargetField { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;
}
```

### 2.8 `UserMark` / `UserMarks`

```csharp
namespace Tender.Core.Models;

public sealed record UserMarks
{
    [JsonPropertyName("marks")]
    public required IReadOnlyList<UserMark> Marks { get; init; }
}

public sealed record UserMark
{
    [JsonPropertyName("sourcePk")]
    public required string SourcePk { get; init; }

    [JsonPropertyName("isFavorite")]
    public bool IsFavorite { get; init; }

    [JsonPropertyName("isRead")]
    public bool IsRead { get; init; }

    [JsonPropertyName("isExcluded")]
    public bool IsExcluded { get; init; }

    [JsonPropertyName("note")]
    public string Note { get; init; } = string.Empty;
}
```

### 2.9 `AppSettings`

```csharp
namespace Tender.Core.Models;

public sealed record AppSettings
{
    /// <summary>排程執行時間，格式 HH:mm。預設 17:00。</summary>
    [JsonPropertyName("scheduledTime")]
    public string ScheduledTime { get; init; } = "17:00";

    /// <summary>是否啟用開機補跑。預設 true。</summary>
    [JsonPropertyName("catchupEnabled")]
    public bool CatchupEnabled { get; init; } = true;

    /// <summary>爬蟲引擎：httpclient（首版預設）或 playwright（PoC 失敗時切換）。</summary>
    [JsonPropertyName("crawlerEngine")]
    public string CrawlerEngine { get; init; } = "httpclient";

    /// <summary>爬蟲分頁間隔毫秒數，遵守禮貌爬取。</summary>
    [JsonPropertyName("requestDelayMs")]
    public int RequestDelayMs { get; init; } = 1500;

    /// <summary>最大重試次數。</summary>
    [JsonPropertyName("maxRetries")]
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// 預設要擷取的招標方式（業務名稱）。實際送網站的 option/value 由 TenderMethodMapping 決定。
    /// </summary>
    [JsonPropertyName("targetTenderMethods")]
    public IReadOnlyList<string> TargetTenderMethods { get; init; } = new[]
    {
        "公開招標",
        "公開取得報價單或企劃書",
        "經公開評選或公開徵求之限制性招標"
    };
}
```

---

## 3. JSON Schema（檔案層級）

### 3.1 `tenders.json`

```json
{
  "date": "2026-05-08",
  "generatedAt": "2026-05-08T17:05:30+08:00",
  "source": "https://web.pcc.gov.tw/pis/",
  "items": [
    {
      "sourcePk": "NzEyMTUxODc=",
      "agencyName": "經濟部水利署",
      "agencyCode": "3.9.1",
      "tenderName": "智慧水利監測系統建置案",
      "tenderNo": "1140508-A001",
      "tenderMethod": "公開招標",
      "procurementType": "勞務類",
      "announcementDate": "115/05/08",
      "bidDeadline": "115/05/12",
      "budgetAmount": 1000000,
      "detailUrl": "https://web.pcc.gov.tw/prkms/urlSelector/common/tpam?pk=NzEyMTUxODc=",
      "matchedKeywords": ["智慧", "系統", "建置"],
      "createdAt": "2026-05-08T17:05:30+08:00",
      "lastSeenAt": "2026-05-08T17:05:30+08:00"
    }
  ]
}
```

### 3.2 `summary.json`

```json
{
  "date": "2026-05-08",
  "totalCount": 55,
  "lastRunStatus": "success",
  "lastRunAt": "2026-05-08T17:05:30+08:00",
  "insertedCount": 55,
  "updatedCount": 0,
  "skippedCount": 0,
  "errorMessage": null
}
```

### 3.3 `crawl-runs.json`

```json
{
  "date": "2026-05-08",
  "runs": [
    {
      "runId": "20260508-170000",
      "targetDate": "2026-05-08",
      "triggerSource": "scheduled",
      "startedAt": "2026-05-08T17:00:00+08:00",
      "finishedAt": "2026-05-08T17:05:30+08:00",
      "status": "success",
      "insertedCount": 55,
      "updatedCount": 0,
      "skippedCount": 0,
      "errorMessage": null
    }
  ]
}
```

### 3.4 `errors.log`（JSON Lines）

```jsonl
{"timestamp":"2026-05-08T17:01:23+08:00","severity":"warning","source":"crawler","runId":"20260508-170000","message":"Page 3 parse failed: missing td.tenderName","page":3}
{"timestamp":"2026-05-08T17:02:00+08:00","severity":"error","source":"crawler","runId":"20260508-170000","message":"HttpRequestException: timeout after 30s","exception":"System.Net.Http.HttpRequestException..."}
```

### 3.5 `settings/keywords.json`（內建初始值由安裝程式建立或首次執行時生成）

```json
{
  "groups": [
    {
      "name": "資訊系統",
      "items": [
        { "keyword": "數位", "targetField": "tenderName", "enabled": true },
        { "keyword": "系統", "targetField": "tenderName", "enabled": true },
        { "keyword": "管理", "targetField": "tenderName", "enabled": true },
        { "keyword": "建置", "targetField": "tenderName", "enabled": true },
        { "keyword": "網", "targetField": "tenderName", "enabled": true },
        { "keyword": "資訊", "targetField": "tenderName", "enabled": true },
        { "keyword": "學習", "targetField": "tenderName", "enabled": true },
        { "keyword": "知識", "targetField": "tenderName", "enabled": true },
        { "keyword": "平台", "targetField": "tenderName", "enabled": true },
        { "keyword": "入口", "targetField": "tenderName", "enabled": true },
        { "keyword": "服務", "targetField": "tenderName", "enabled": true }
      ]
    },
    {
      "name": "XR/AI",
      "items": [
        { "keyword": "虛擬", "targetField": "tenderName", "enabled": true },
        { "keyword": "擴增", "targetField": "tenderName", "enabled": true },
        { "keyword": "混合", "targetField": "tenderName", "enabled": true },
        { "keyword": "電子", "targetField": "tenderName", "enabled": true },
        { "keyword": "AR", "targetField": "tenderName", "enabled": true },
        { "keyword": "VR", "targetField": "tenderName", "enabled": true },
        { "keyword": "MR", "targetField": "tenderName", "enabled": true },
        { "keyword": "XR", "targetField": "tenderName", "enabled": true },
        { "keyword": "AI", "targetField": "tenderName", "enabled": true },
        { "keyword": "人工智慧", "targetField": "tenderName", "enabled": true },
        { "keyword": "數位雙生", "targetField": "tenderName", "enabled": true },
        { "keyword": "數位孿生", "targetField": "tenderName", "enabled": true },
        { "keyword": "沉浸式", "targetField": "tenderName", "enabled": true }
      ]
    },
    {
      "name": "資安/無障礙",
      "items": [
        { "keyword": "ISMS", "targetField": "tenderName", "enabled": true },
        { "keyword": "ISRM", "targetField": "tenderName", "enabled": true },
        { "keyword": "資訊安全", "targetField": "tenderName", "enabled": true },
        { "keyword": "無障礙", "targetField": "tenderName", "enabled": true }
      ]
    },
    {
      "name": "ESG/碳管理",
      "items": [
        { "keyword": "ESG", "targetField": "tenderName", "enabled": true },
        { "keyword": "永續", "targetField": "tenderName", "enabled": true },
        { "keyword": "淨零", "targetField": "tenderName", "enabled": true },
        { "keyword": "碳排", "targetField": "tenderName", "enabled": true },
        { "keyword": "碳盤查", "targetField": "tenderName", "enabled": true }
      ]
    },
    {
      "name": "業務雜項",
      "items": [
        { "keyword": "補助", "targetField": "tenderName", "enabled": true },
        { "keyword": "津貼", "targetField": "tenderName", "enabled": true },
        { "keyword": "雲端", "targetField": "tenderName", "enabled": true },
        { "keyword": "AWS", "targetField": "tenderName", "enabled": true },
        { "keyword": "報名", "targetField": "tenderName", "enabled": true },
        { "keyword": "無人", "targetField": "tenderName", "enabled": true },
        { "keyword": "巡檢", "targetField": "tenderName", "enabled": true },
        { "keyword": "志工", "targetField": "tenderName", "enabled": true },
        { "keyword": "共同供應契約", "targetField": "tenderName", "enabled": true }
      ]
    },
    {
      "name": "智慧管考",
      "items": [
        { "keyword": "管考", "targetField": "tenderName", "enabled": true },
        { "keyword": "智慧", "targetField": "tenderName", "enabled": true },
        { "keyword": "計畫", "targetField": "tenderName", "enabled": true },
        { "keyword": "案管", "targetField": "tenderName", "enabled": true },
        { "keyword": "監控", "targetField": "tenderName", "enabled": true },
        { "keyword": "桌牌", "targetField": "tenderName", "enabled": true },
        { "keyword": "作業", "targetField": "tenderName", "enabled": true },
        { "keyword": "表單", "targetField": "tenderName", "enabled": true },
        { "keyword": "偵測", "targetField": "tenderName", "enabled": true },
        { "keyword": "整合", "targetField": "tenderName", "enabled": true }
      ]
    },
    {
      "name": "倉儲自動化",
      "items": [
        { "keyword": "倉儲", "targetField": "tenderName", "enabled": true },
        { "keyword": "搬運", "targetField": "tenderName", "enabled": true },
        { "keyword": "自動化", "targetField": "tenderName", "enabled": true },
        { "keyword": "物料", "targetField": "tenderName", "enabled": true },
        { "keyword": "AVG", "targetField": "tenderName", "enabled": true },
        { "keyword": "WMS", "targetField": "tenderName", "enabled": true },
        { "keyword": "儲存", "targetField": "tenderName", "enabled": true },
        { "keyword": "智慧倉儲", "targetField": "tenderName", "enabled": true },
        { "keyword": "倉儲管理", "targetField": "tenderName", "enabled": true },
        { "keyword": "物料管理", "targetField": "tenderName", "enabled": true },
        { "keyword": "揀貨系統", "targetField": "tenderName", "enabled": true }
      ]
    },
    {
      "name": "地區",
      "items": [
        { "keyword": "屏東", "targetField": "any", "enabled": true },
        { "keyword": "高雄", "targetField": "any", "enabled": true },
        { "keyword": "臺南", "targetField": "any", "enabled": true },
        { "keyword": "嘉義", "targetField": "any", "enabled": true },
        { "keyword": "雲林", "targetField": "any", "enabled": true },
        { "keyword": "南投", "targetField": "any", "enabled": true },
        { "keyword": "彰化", "targetField": "any", "enabled": true },
        { "keyword": "臺中", "targetField": "any", "enabled": true },
        { "keyword": "苗栗", "targetField": "any", "enabled": true },
        { "keyword": "新竹", "targetField": "any", "enabled": true },
        { "keyword": "桃園", "targetField": "any", "enabled": true },
        { "keyword": "新北", "targetField": "any", "enabled": true },
        { "keyword": "臺北", "targetField": "any", "enabled": true },
        { "keyword": "基隆", "targetField": "any", "enabled": true },
        { "keyword": "宜蘭", "targetField": "any", "enabled": true },
        { "keyword": "花蓮", "targetField": "any", "enabled": true },
        { "keyword": "臺東", "targetField": "any", "enabled": true },
        { "keyword": "墾丁", "targetField": "any", "enabled": true },
        { "keyword": "澎湖", "targetField": "any", "enabled": true },
        { "keyword": "金門", "targetField": "any", "enabled": true }
      ]
    },
    {
      "name": "指定機關",
      "items": [
        { "keyword": "經濟部水利署", "targetField": "agencyName", "enabled": true },
        { "keyword": "原住民族委員會", "targetField": "agencyName", "enabled": true },
        { "keyword": "財團法人職業災害預防及重建中心", "targetField": "agencyName", "enabled": true }
      ]
    }
  ]
}
```

### 3.6 `settings/user-marks.json`

```json
{
  "marks": [
    {
      "sourcePk": "NzEyMTUxODc=",
      "isFavorite": true,
      "isRead": false,
      "isExcluded": false,
      "note": ""
    }
  ]
}
```

### 3.7 `settings/app-settings.json`

```json
{
  "scheduledTime": "17:00",
  "catchupEnabled": true,
  "crawlerEngine": "httpclient",
  "requestDelayMs": 1500,
  "maxRetries": 3,
  "targetTenderMethods": [
    "公開招標",
    "公開取得報價單或企劃書",
    "經公開評選或公開徵求之限制性招標"
  ]
}
```

---

## 4. 民國年/西元年處理策略

| 場景 | 採用格式 | 原因 |
|---|---|---|
| `tenders.json` 的 `announcementDate`、`bidDeadline` | 民國年（`115/05/08`） | 保留來源網站原始格式，匯出 Excel 時對使用者最熟悉 |
| 資料夾名稱 | 西元年（`2026-05-08`） | 排序友善、跨系統相容 |
| 程式內部運算（區間篩選） | `DateOnly` (西元) | 透過 `ITaiwanDateConverter.RocToDateOnly()` 轉換 |
| `summary.json` 的 `date` | 西元年（`2026-05-08`） | 與資料夾一致 |
| `crawl-runs.json` 的 `targetDate` | 西元年 | 同上 |
| 所有 `*At` 時間欄位 | ISO 8601 含時區 | 標準格式 |

**`ITaiwanDateConverter` 對外 API**：

```csharp
public interface ITaiwanDateConverter
{
    /// <summary>「115/05/08」 → DateOnly(2026,5,8)。解析失敗回傳 null。</summary>
    DateOnly? RocToDateOnly(string rocDate);
    /// <summary>DateOnly(2026,5,8) → 「115/05/08」。</summary>
    string DateOnlyToRoc(DateOnly date);
}
```

---

## 5. 設計備註

- 所有檔案層級的 record 採 `required` 屬性 + `init` setter，在反序列化時若缺欄位會丟出，逼迫呼叫端用 try/catch 並寫入 errors.log。
- `IReadOnlyList<T>` 對外，內部組合時使用 `List<T>`，序列化時 `System.Text.Json` 會自動處理。
- 列舉採 `JsonStringEnumMemberName` 確保 JSON 內為小寫字串，符合 PM Gherkin 中「`status: 'success'`」的字面值。
