# 標案搜尋工具 MVP — Event Storming

> 範圍：需求文件第 10 節 MVP，包含每日自動爬蟲、月份行事曆、日期查詢、關鍵字篩選、Excel 匯出、安裝與排程、更新紀錄與錯誤日誌。
> 本文件為 PM 階段業務描述，不涉及 C# 類別或 WPF 控制項實作細節。

---

## 第一層 Big Picture（核心流程概觀）

時間軸由左至右，依業務流程串接領域事件：

```
[排程時間到] → 已啟動每日爬蟲 → 已查詢政府電子採購網 → 已解析標案清單
            → 已去重當日標案 → 已寫入當日標案資料 → 已產生當日摘要
            → 已記錄爬蟲執行結果 → 已更新行事曆筆數

[使用者開啟桌面程式] → 已載入月份行事曆 → 已顯示當月每日筆數
                  → 使用者點擊某日 → 已載入該日標案資料 → 使用者搜尋/篩選
                  → 已產生篩選結果 → 使用者匯出 → 已匯出 Excel 檔案

[排程錯過] → 已偵測未跑排程 → 已啟動補跑 → 已寫入補跑結果
[安裝程式執行] → 已建立資料目錄 → 已建立桌面捷徑 → 已建立 Windows 排程任務
```

---

## 第二層 Process Modeling（依 Bounded Context 拆解）

### Bounded Context 1：每日爬蟲（Daily Crawl）

#### Actors（參與者）
- 藍色：`Windows Task Scheduler`（排程觸發者）
- 藍色：`使用者`（手動立即更新的觸發者）
- 黃色：`Tender.Crawler`（爬蟲背景程式）
- 黃色：`政府電子採購網`（外部系統，資料來源）

#### Commands（指令，藍色便利貼）
- 🔵 `啟動每日爬蟲`：由 Windows Task Scheduler 在每日 17:00 觸發
- 🔵 `啟動手動更新`：由使用者在桌面程式按下「立即更新」觸發
- 🔵 `重新抓取指定日`：由使用者在行事曆對某一天執行補抓觸發
- 🔵 `查詢政府電子採購網`：依招標方式與當日公告日期送出查詢
- 🔵 `解析標案清單`：將回應頁解析為標案資料模型
- 🔵 `去重當日標案`：以 `sourcePk` 為唯一鍵與當日既有資料合併
- 🔵 `寫入當日標案資料`：以暫存檔加原子替換策略寫入 `tenders.json`
- 🔵 `產生當日摘要`：寫入 `summary.json`
- 🔵 `記錄爬蟲執行結果`：寫入 `crawl-runs.json`
- 🔵 `記錄錯誤日誌`：寫入 `errors.log`
- 🔵 `補跑遺漏排程`：開機後若偵測前次排程未完成則觸發

#### Domain Events（領域事件，橘色便利貼，過去式）
- 🟠 `每日爬蟲已啟動`
- 🟠 `手動更新已啟動`
- 🟠 `指定日重抓已啟動`
- 🟠 `政府電子採購網查詢已送出`
- 🟠 `標案清單已解析`
- 🟠 `招標方式對應已套用`（公開招標／公開取得報價單或企劃書／經公開評選或公開徵求之限制性招標）
- 🟠 `當日標案已去重`
- 🟠 `當日標案資料已寫入`
- 🟠 `當日摘要已產生`
- 🟠 `爬蟲執行結果已記錄`
- 🟠 `關鍵字命中已標註`（命中關鍵字寫入 `matchedKeywords`）
- 🟠 `爬蟲已重試`
- 🟠 `爬蟲已失敗`
- 🟠 `錯誤日誌已寫入`
- 🟠 `分頁已擷取`
- 🟠 `補跑排程已執行`

#### Aggregates / 資料模型（黃色便利貼）
- 🟡 `DailyTenderSnapshot`（當日標案快照）
  - 對應檔案：`data/yyyy-MM/yyyy-MM-dd/tenders.json`
  - 主要欄位：`date`、`generatedAt`、`source`、`items[]`
  - `items[]` 欄位：`sourcePk`、`agencyName`、`agencyCode`、`tenderName`、`tenderNo`、`tenderMethod`、`procurementType`、`announcementDate`、`bidDeadline`、`budgetAmount`、`detailUrl`、`matchedKeywords`、`createdAt`、`lastSeenAt`
  - 不變條件：同日內 `sourcePk` 唯一
- 🟡 `DailySummary`（當日摘要）
  - 對應檔案：`summary.json`
  - 欄位：`date`、`totalCount`、`lastRunStatus`、`lastRunAt`、`insertedCount`、`updatedCount`、`skippedCount`、`errorMessage`
- 🟡 `CrawlRunLog`（每日爬蟲執行紀錄）
  - 對應檔案：`crawl-runs.json`
  - 欄位：`date`、`runs[]`（`runId`、`targetDate`、`startedAt`、`finishedAt`、`status`、`insertedCount`、`updatedCount`、`skippedCount`、`errorMessage`）
- 🟡 `ErrorLog`
  - 對應檔案：`errors.log`

#### Policies / 業務規則（紫色便利貼）
- 🟣 當 `每日爬蟲已啟動` → 以「當天日期」作為公告日期查詢條件
- 🟣 當 `標案清單已解析` 後 → 一律執行 `去重當日標案`（同一天重複執行也不可重複）
- 🟣 當 `當日標案已去重` → 對標案名稱套用 Excel 既有關鍵字命中標註
- 🟣 當 `當日標案資料已寫入` → 立即執行 `產生當日摘要`
- 🟣 當 `爬蟲執行結果已記錄` 且狀態為成功 → 行事曆該日筆數需更新
- 🟣 當 `爬蟲已失敗` → `summary.json` 的 `lastRunStatus` 必須為 `failed` 並寫入 `errorMessage`
- 🟣 當單頁解析失敗 → 不可中斷整批任務，需記錄錯誤後繼續其他頁
- 🟣 當 17:00 電腦離線或關機 → 下一次開機後需偵測並補跑一次
- 🟣 當寫入過程中斷 → 既有 `tenders.json` 不可被破壞（以 `.tmp` 暫存後原子替換）
- 🟣 當使用者手動執行重新抓取指定日 → 結果需明確標示為補抓，不影響當日 17:00 排程的「預設行為」定義

---

### Bounded Context 2：月份行事曆首頁（Monthly Calendar）

#### Actors
- 藍色：`使用者`
- 黃色：`Tender.Desktop`（桌面 UI）

#### Commands
- 🔵 `載入月份行事曆`：開啟程式或切換月份時觸發
- 🔵 `切換上一個月`
- 🔵 `切換下一個月`
- 🔵 `回到本月`
- 🔵 `點擊日期格`：進入該日資料查詢頁
- 🔵 `查看失敗摘要`：點擊有警示符號的日期格

#### Domain Events
- 🟠 `月份行事曆已載入`
- 🟠 `當月每日筆數已顯示`
- 🟠 `今日格已醒目標示`
- 🟠 `失敗日已顯示警示`
- 🟠 `月份摘要已顯示`（本月累計、今日新增、最近一次更新時間）
- 🟠 `已跳轉至日期查詢頁`

#### Aggregates / 資料模型
- 🟡 `MonthlyCalendarView`（行事曆視圖，唯讀彙總）
  - 來源：每日 `summary.json`
  - 欄位：`year`、`month`、`days[]`（`date`、`totalCount`、`lastRunStatus`、`lastRunAt`）

#### Policies
- 🟣 行事曆載入時 → 優先讀取每日 `summary.json`，不解析 `tenders.json`
- 🟣 沒有資料的日期 → 顯示 `0` 或不顯示筆數，視覺上需可區分有/無資料
- 🟣 `lastRunStatus = failed` 的日期 → 顯示警示符號
- 🟣 只有有資料的日期格才可點擊進入查詢頁

---

### Bounded Context 3：指定日期資料查詢（Daily Query）

#### Actors
- 藍色：`使用者`
- 黃色：`Tender.Desktop`

#### Commands
- 🔵 `載入指定日標案資料`
- 🔵 `切換到前一天`
- 🔵 `切換到後一天`
- 🔵 `返回月份行事曆`
- 🔵 `輸入關鍵字搜尋`（搜尋 `tenderName`、`agencyName`、`tenderNo`）
- 🔵 `套用招標方式篩選`
- 🔵 `套用採購性質篩選`
- 🔵 `套用公告日期區間`
- 🔵 `套用截止投標日期區間`
- 🔵 `套用預算金額區間`
- 🔵 `套用只看尚未截止`
- 🔵 `點擊欄位排序`
- 🔵 `開啟標案詳情連結`

#### Domain Events
- 🟠 `指定日標案資料已載入`
- 🟠 `搜尋結果已產生`
- 🟠 `篩選結果已產生`
- 🟠 `排序已套用`
- 🟠 `已開啟政府電子採購網標案頁`
- 🟠 `已切換查詢日期`

#### Aggregates / 資料模型
- 🟡 `DailyQueryView`
  - 來源：當日 `tenders.json`
  - 子集合：`filteredItems[]`、`activeFilters`、`sortKey`

#### Policies
- 🟣 進入查詢頁 → 預設只顯示該日期的標案資料
- 🟣 多關鍵字 → 視為 OR 命中（沿用 Excel 既有「命中任一關鍵字即顯示」行為）
- 🟣 點擊「檢視」連結 → 以使用者預設瀏覽器開啟 `detailUrl`
- 🟣 切換到無資料的日期 → 顯示空狀態，不報錯

---

### Bounded Context 4：關鍵字快速篩選（Keyword Filter，沿用 Excel 既有）

#### Actors
- 藍色：`使用者`
- 黃色：`Tender.Desktop`

#### Commands
- 🔵 `套用 Excel 既有關鍵字按鈕`（資訊系統／XR/AI／ESG/碳管理／倉儲自動化）
- 🔵 `套用地區關鍵字`
- 🔵 `套用機關關鍵字`
- 🔵 `清除關鍵字篩選`

#### Domain Events
- 🟠 `關鍵字篩選已套用`
- 🟠 `命中關鍵字已標示`
- 🟠 `關鍵字篩選已清除`

#### Aggregates / 資料模型
- 🟡 `KeywordSet`（沿用 Excel 既有清單，需求文件 2.2 節）
  - 資訊系統：數位、系統、管理、建置、網、資訊、學習、知識、平台、入口、服務
  - XR/AI：虛擬、擴增、混合、電子、AR、VR、MR、XR、AI、人工智慧、數位雙生、數位孿生、沉浸式
  - 資安/無障礙：ISMS、ISRM、資訊安全、無障礙
  - ESG/碳管理：ESG、永續、淨零、碳排、碳盤查
  - 業務雜項：補助、津貼、雲端、AWS、報名、無人、巡檢、志工、共同供應契約
  - 智慧管考：管考、智慧、計畫、案管、監控、桌牌、作業、表單、偵測、整合
  - 倉儲自動化：倉儲、搬運、自動化、物料、AVG、WMS、儲存、智慧倉儲、倉儲管理、物料管理、揀貨系統
  - 地區：屏東、高雄、臺南、嘉義、雲林、南投、彰化、臺中、苗栗、新竹、桃園、新北、臺北、基隆、宜蘭、花蓮、臺東、墾丁、澎湖、金門
  - 指定機關：經濟部水利署、原住民族委員會、財團法人職業災害預防及重建中心

#### Policies
- 🟣 標案名稱關鍵字 → 預設搜尋 `tenderName`
- 🟣 地區關鍵字 → 可搜尋 `agencyName` 或標案名稱（沿用 Excel 巨集行為）
- 🟣 機關關鍵字 → 預設搜尋 `agencyName`
- 🟣 命中的關鍵字 → 寫入 `matchedKeywords`，並在列表「命中關鍵字」欄顯示

---

### Bounded Context 5：匯出 Excel（Export）

#### Actors
- 藍色：`使用者`
- 黃色：`Tender.Desktop`

#### Commands
- 🔵 `匯出當前結果為 Excel`

#### Domain Events
- 🟠 `匯出檔案已產生`
- 🟠 `匯出已失敗`

#### Aggregates / 資料模型
- 🟡 `ExportRequest`
  - 來源：當前 `DailyQueryView` 的篩選結果
  - 輸出：`.xlsx` 檔案，欄位至少對應 Excel 既有欄位（標案名稱、招標方式、採購性質、公告日期、截止投標、預算金額、機關名稱、檢視連結）
  - 標案名稱或檢視欄需保留可點擊超連結

#### Policies
- 🟣 匯出範圍 → 套用使用者目前日期與搜尋/篩選條件後的結果集
- 🟣 匯出格式 → 適合再用 Excel 開啟篩選

---

### Bounded Context 6：安裝與排程任務建立（Installation）

#### Actors
- 藍色：`使用者`（執行安裝程式）
- 黃色：`Tender.Installer`

#### Commands
- 🔵 `執行安裝`
- 🔵 `建立資料根目錄`
- 🔵 `建立桌面捷徑`
- 🔵 `建立開始功能表捷徑`
- 🔵 `建立 Windows 排程任務`

#### Domain Events
- 🟠 `資料根目錄已建立`（`%LocalAppData%/TenderSearch/data/`）
- 🟠 `桌面捷徑已建立`
- 🟠 `開始功能表捷徑已建立`
- 🟠 `Windows 排程任務已建立`（每日 17:00）
- 🟠 `安裝已完成`
- 🟠 `安裝已失敗`

#### Aggregates / 資料模型
- 🟡 `InstallationOutcome`
  - 欄位：`dataRootPath`、`desktopShortcutCreated`、`startMenuShortcutCreated`、`scheduledTaskCreated`、`scheduledTime`

#### Policies
- 🟣 安裝完成後 → 排程任務的執行時間預設為每日 17:00
- 🟣 排程任務 → 即使桌面程式未開啟仍能執行
- 🟣 若資料根目錄已存在 → 不可覆蓋既有資料

---

### Bounded Context 7：更新紀錄與錯誤日誌（Crawl Logging）

#### Actors
- 黃色：`Tender.Crawler`
- 藍色：`使用者`（檢視紀錄）

#### Commands
- 🔵 `寫入爬蟲執行紀錄`
- 🔵 `寫入錯誤日誌`
- 🔵 `檢視最近一次更新結果`
- 🔵 `檢視某日錯誤摘要`

#### Domain Events
- 🟠 `爬蟲執行紀錄已新增`（含開始時間、結束時間、成功/失敗、新增筆數、更新筆數、略過重複筆數、錯誤訊息）
- 🟠 `錯誤日誌已寫入`
- 🟠 `最近一次更新結果已顯示`
- 🟠 `某日錯誤摘要已顯示`

#### Aggregates / 資料模型
- 🟡 `CrawlRunLog`（同 Bounded Context 1）
- 🟡 `ErrorLog`

#### Policies
- 🟣 每次爬蟲執行（無論成功或失敗）→ 必須在 `crawl-runs.json` 新增一筆紀錄
- 🟣 任何解析錯誤、網路錯誤、檔案寫入錯誤 → 必須寫入 `errors.log`
- 🟣 行事曆首頁側邊摘要 → 必須能呈現「最近一次更新時間」與「最近失敗紀錄」

---

## 跨 Context Policy（Big Picture 級的政策）

- 🟣 當 `當日摘要已產生` → 月份行事曆首頁該日格筆數需同步更新
- 🟣 當 `爬蟲已失敗` → 月份行事曆首頁該日格需顯示警示符號
- 🟣 當 `安裝已完成` → 系統具備自主每日 17:00 執行能力，無須使用者另行設定
- 🟣 當同一標案在跨日仍出現於查詢結果 → 每日快照各自保留該標案紀錄（首版規則）

---

## 待確認事項

### 已由使用者於 2026-05-08 決議

- ✅ **多關鍵字搜尋邏輯**：採用 **AND 命中**，且關鍵字支援**模糊查詢**（包含子字串視為命中）。例：搜尋「智慧 倉儲」時，標案名稱需同時包含這兩個子字串。
- ✅ **排程任務權限**：採用 **Per-user 任務**（Windows Task Scheduler 安裝於當前使用者下，不需系統管理員權限）。
- ✅ **補跑遺漏排程的觸發**：**桌面程式啟動偵測 + Task Scheduler 內建 missed run 兩者都做**（雙保險）。
- ✅ **匯出 Excel 儲存位置**：採用**另存新檔對話框**，由使用者每次自行決定路徑與檔名。

### 仍待確認（建議於 SA 階段或 spike 後決議，首版採暫定值）

1. **招標方式文字對應**：政府網站實際 option/value 需以政府電子採購網為準後建立對應表。本文件以「公開招標／公開取得報價單或企劃書／經公開評選或公開徵求之限制性招標」為業務名稱（暫定）。
2. **「當天最新標案」定義**：首版以「公告日期等於當天」為條件（暫定），是否未來改用上架時間或異動時間需另行決議。
3. **跨日重複標案的快照規則**：首版規則為「該日查詢結果若有出現就保留於該日紀錄」（暫定），是否額外標記「首次出現日」未來決議。
4. **手動立即更新的目標日期**：以「手動立即更新＝抓當天」、「重新抓取指定日＝補抓特定日」兩個明確指令區分（暫定）。
5. **失敗日警示符號的形式**：屬 UX 範疇，留待設計階段。
6. **錯誤日誌格式**：`errors.log` 建議採用 **JSON Lines** 結構化格式以利工程排查（暫定，由 SA 確認）。
7. **「最近一次更新時間」彙總範圍**：暫定為「全系統最近一次」（跨所有日期的最後一次成功更新）。
8. **預算金額區間單位**：暫定為「元」，與 `budgetAmount` 欄位一致。
