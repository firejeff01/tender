# 政府電子採購網 標案查詢工具 (TenderSearch)

> 自動每日抓取政府電子採購網（[web.pcc.gov.tw](https://web.pcc.gov.tw/)）招標公告，
> 提供桌面行事曆瀏覽、即時搜尋／關鍵字篩選、收藏與 Excel 匯出功能。

---

## 安裝

### 一般使用者

1. 雙擊 `dist/TenderSearch.msi` 進行安裝
2. 安裝完成後會自動：
   - 在 `C:\Program Files\TenderSearch\` 部署檔案
   - 桌面與「開始」功能表建立「標案查詢」捷徑
   - 在 Task Scheduler 建立 `TenderSearch.DailyCrawl` 每日 17:00 自動爬取任務
3. 如果 Task Scheduler 沒被自動建立（權限不足），請從「開始」功能表 → 「標案查詢」資料夾 → 「建立每日排程」捷徑（**右鍵 → 以系統管理員身份執行**）

### 系統需求

- **作業系統**：Windows 10/11 (x64)
- **執行環境**：.NET 8 Desktop Runtime（`TenderSearch.msi` 為 framework-dependent，需先安裝 runtime）
  - 下載：<https://dotnet.microsoft.com/download/dotnet/8.0/runtime>
  - 或建置 self-contained MSI：`.\build\build-msi.ps1 -SelfContained`（檔案會變大到 ~80MB）

---

## 使用方式

### 主要功能

#### 月份行事曆
- 顯示當月每日標案總筆數
- **失敗日**會以紅底 + ⚠ 標示，點選會彈出錯誤摘要
- **無資料日**為灰底
- 上方 chips 顯示：本月總筆數、有資料天數、失敗天數
- 月份切換：◀ ▶ 或「本月」按鈕

#### 日期查詢頁
點某一天進入查詢頁，可以：

- **搜尋**：輸入關鍵字（空白分割多詞，AND 命中）
- **招標方式 / 採購性質** 下拉篩選
- **預算金額**區間篩選
- **截止投標**日期區間篩選
- **9 個關鍵字群組下拉**（資訊系統 / XR/AI / 資安 / ESG / 業務雜項 / 智慧管考 / 倉儲 / 地區 / 指定機關），勾選後 OR 命中
- **只看尚未截止** / **只看收藏** checkbox
- **排序**：機關名稱 / 標案名稱 / 公告日期 / 截止投標 / 預算金額（升降序）
- **★ 收藏**：點 DataGrid 第一欄星號 toggle，存入 `user-marks.json`，可用「只看收藏」篩選
- **詳情面板**：右側顯示選取項目完整資訊
- **雙擊** DataGrid 列或詳情面板按鈕「🔗 在瀏覽器開啟詳情頁」開啟政府網站

#### 立即更新
上方工具列「🔄 立即更新（今日）」：
- 啟動 Crawler 子程序，stdout 即時通報進度
- 完成後自動刷新行事曆
- **若今日已成功跑過，按鈕會變綠**

#### 匯出 Excel
進入查詢頁後，工具列右上「📊 匯出 Excel」：
- 匯出「目前篩選後」的標案
- 10 個欄位（機關 / 標案名稱 / 招標方式 / ... / 命中關鍵字）
- 標案名稱 / 檢視連結 / 機關名稱：標案名稱 三欄為 hyperlink

### 設定（左上 ⚙ 選單）

#### 🔧 管理關鍵字
- 左側群組清單，可新增、刪除、重新命名
- 右側為該群組的關鍵字 DataGrid：可編輯關鍵字、比對欄位（tenderName / agencyName / any）、啟用 toggle、刪除
- 「💾 儲存」寫回 `keywords.json`，下次抓取會套用新關鍵字

#### ⚙ 應用設定
- **排程執行時間**（HH:mm，預設 17:00）
  - ⚠ 修改後需到 Task Scheduler 手動修改 `TenderSearch.DailyCrawl` 任務的觸發時間
- **啟動時自動補跑**：若 17:00 後啟動且今日無資料則自動補跑
- **請求間隔（毫秒）**：禮貌爬取間隔，建議 ≥ 1500
- **最大重試次數**：網路錯誤時的指數退避重試上限

---

## 資料儲存路徑

預設位置：`%LocalAppData%\TenderSearch\data\`

```
data/
├── 2026-05/
│   └── 2026-05-08/
│       ├── tenders.json         # 當日標案完整資料
│       ├── summary.json         # 當日摘要（總筆數、執行狀態）
│       ├── crawl-runs.json      # 該日所有執行紀錄
│       └── errors.log           # 該日錯誤日誌（JSON Lines）
├── settings/
│   ├── keywords.json            # 關鍵字設定
│   ├── user-marks.json          # 收藏標案
│   └── app-settings.json        # 應用設定
└── locks/
    └── crawler.lock             # 防重複執行鎖
```

---

## 開發

### 專案結構

```
src/
├── Tender.Core/          # 領域模型 + 純邏輯（搜尋 / 關鍵字命中 / 民國年轉換）
├── Tender.Storage/       # JSON 讀寫 + 原子寫入 + Repository
├── Tender.Crawler/       # Console 爬蟲（HttpClient + AngleSharp）
├── Tender.Desktop/       # WPF 桌面 UI（CommunityToolkit.Mvvm + Microsoft.Extensions.Hosting）
└── Tender.Installer/     # WiX 5 MSI 安裝程式

tests/
├── Tender.Core.Tests/        (78 tests)
├── Tender.Storage.Tests/     (24 tests)
├── Tender.Crawler.Tests/     (58 tests)
└── Tender.AcceptanceTests/   (Reqnroll Step Definitions 為 SA 階段 scaffold，未實作)

build/
├── build-msi.ps1         # Publish + Build MSI 整合腳本
└── convert-icon.ps1      # PNG → 多尺寸 ICO（自動裁透明邊框 + Zoom）

dist/
└── TenderSearch.msi      # 出貨 MSI（4.8 MB framework-dependent）
```

### 建置

```powershell
# 還原 + Build 整個 solution
dotnet build Tender.slnx

# 跑全部 unit tests（160 個）
dotnet test tests/Tender.Core.Tests/Tender.Core.Tests.csproj --no-build
dotnet test tests/Tender.Crawler.Tests/Tender.Crawler.Tests.csproj --no-build
dotnet test tests/Tender.Storage.Tests/Tender.Storage.Tests.csproj --no-build

# 直接執行桌面（dev）
dotnet run --project src/Tender.Desktop

# 手動跑爬蟲（測試）
dotnet run --project src/Tender.Crawler -- --mode poc --target-date 2026-05-08
dotnet run --project src/Tender.Crawler -- --mode manual --target-date 2026-05-08

# 建置 MSI（framework-dependent，4.8 MB）
.\build\build-msi.ps1

# 建置 self-contained MSI（含 .NET 8 runtime，~80 MB）
.\build\build-msi.ps1 -SelfContained

# 重新生成 ICO（PNG 變動時）
.\build\convert-icon.ps1
```

### 主要技術棧

- **.NET 8 / C# 12**（Crawler + Core + Storage：cross-platform；Desktop：win-only WPF）
- **WPF + CommunityToolkit.Mvvm**（[ObservableProperty] / [RelayCommand] 源產生器）
- **Microsoft.Extensions.Hosting**（Desktop 內 IHost + DI）
- **AngleSharp**（HTML 解析）
- **ClosedXML**（Excel 匯出）
- **WiX Toolset 5.0.2**（MSI 打包）
- **xUnit + FluentAssertions + Moq**（單元測試）

### Crawler 抓取邏輯

對 `https://web.pcc.gov.tw/prkms/tender/common/advanced/readTenderAdvanced` 發 GET：

| 條件 | 值 |
|---|---|
| 招標類型 (`tenderType`) | `TENDER_DECLARATION`（招標公告） |
| 招標方式 (`tenderWay`) | `TENDER_WAY_1`（公開招標）/ `TENDER_WAY_12`（公開取得電子報價單）/ `TENDER_WAY_4`（經公開評選或公開徵求之限制性招標）— 三種輪流跑 |
| 公告日期 (`dateType=isDate` + `tenderStartDate=tenderEndDate=YYYY/MM/DD` 西元) | 鎖單日 |
| 分頁 | `d-49738-p=N`（displaytag 約定）；`pageSize=2000` 通常一次拉完一天 |

每天約抓 800~900 筆（三種招標方式合計），整個流程 ~5 秒。
資料欄位包含：機關名稱、標案名稱、招標方式、採購性質、公告日期、截止投標、預算金額、檢視連結、命中關鍵字。

---

## 疑難排解

### 安裝後沒有 Task Scheduler 任務

原因：MSI 安裝時 Custom Action 需要系統管理員權限。

解法：
1. 打開「開始」功能表 → 「標案查詢」資料夾
2. **右鍵點「建立每日排程」捷徑** → 「以系統管理員身份執行」

### 雙擊桌面捷徑沒反應

原因：未安裝 .NET 8 Desktop Runtime。

解法：
- 下載 .NET 8 Desktop Runtime：<https://dotnet.microsoft.com/download/dotnet/8.0/runtime>
- 或重建為 self-contained MSI：`.\build\build-msi.ps1 -SelfContained`

### 立即更新失敗 / 顯示 exit code 4

原因：`%LocalAppData%\TenderSearch\data\locks\crawler.lock` 殘留（前次爬蟲未正常結束）。

解法：手動刪除 `crawler.lock` 後重試。

### 行事曆某天顯示「失敗」

原因：該日爬蟲執行時遇到錯誤（網路、解析、I/O 等）。

解法：點該天 → 彈出錯誤摘要視窗 → 看 `errors.log` 細節判斷原因。

---

## 發版流程（給維護者）

**整個流程已 CI 自動化**：你只要 push 一個 tag，GitHub Actions 會自動 build MSI、跑單元測試、發 Release 並附上 MSI。

### 一般流程

1. **commit 你的程式碼變更**
   ```powershell
   git add -A
   git commit -m "Your change"
   git push
   ```

2. **打版本 tag 並 push**

   版本號格式 `v{major}.{year-2025}.{MMDD}.{HHmm}`，例如 2026/05/09 17:30 就是 `v1.1.509.1730`：

   ```powershell
   git tag v1.1.509.1730
   git push origin v1.1.509.1730
   ```

3. **GitHub Actions 自動執行**（約 3~5 分鐘）：
   - 拉程式碼 → 裝 .NET 8 + WiX 5
   - 跑全部 unit tests
   - `build-msi.ps1 -Version 1.1.509.1730`
   - 用該 tag 自動建 Release，把 `TenderSearch.msi` 上傳為 asset
   - 進度可在 <https://github.com/firejeff01/tender/actions> 看

4. **使用者下次開 app**：
   - 啟動時背景呼叫 GitHub API
   - 偵測到 GitHub 上的 tag 版本 > 本機 assembly 版本
   - 工具列下方出現黃色 banner：「🆕 有新版本可下載：1.1.509.1730（目前 v1.0.0.0）  點此下載 →」
   - 使用者點 banner → 預設瀏覽器開 release page 下載 MSI
   - 下載完雙擊 → MajorUpgrade 自動覆蓋

### 本機 dry-run（不發版）

如果只想本機測試 build 是否正常、不發版：

```powershell
.\build\build-msi.ps1                       # 自動產生時間版本號
.\build\build-msi.ps1 -SelfContained        # 含 .NET 8 runtime（~80MB）
.\build\build-msi.ps1 -Version "1.0.0.0"   # 強制指定版本（測試降版）
```

### 更新檢查機制

- 程式啟動時於背景呼叫 `https://api.github.com/repos/firejeff01/tender/releases/latest`
- 若失敗（無網路、私有 repo 沒授權、API rate limit）→ 靜默失敗，不影響主程式
- 比對版本：當前 assembly 版本 vs tag 解析出的 Version
- 不會自動下載，僅顯示提示讓使用者主動點選

---

## 授權

本軟體為內部使用工具，無對外授權聲明。
資料來源：政府電子採購網，資料權屬於原始公告機關所有。
