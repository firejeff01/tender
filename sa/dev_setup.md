# 開發環境與建置（Dev Setup）

> 本文件提供建置「標案搜尋工具」所需的環境、工具、NuGet 套件與指令。
> 對應實作由 csharp-expert 階段使用。
> 產出日期：2026-05-08

---

## 1. 必要工具

| 工具 | 版本 | 用途 |
|---|---|---|
| .NET SDK | **.NET 8 SDK**（8.0.x，LTS） | 主要 SDK |
| Visual Studio 2022 (17.8+) 或 JetBrains Rider 2024.x | 任一 | IDE，需含「.NET 桌面開發」工作負載 |
| WiX Toolset | v4.x | 建立 MSI 安裝包 |
| WiX Visual Studio Extension（HeatWave） | 對應 v4 | 在 VS 中編輯 .wxs |
| Git | 任意新版本 | 版本控制 |
| PowerShell | 5.1 或 7.x | 在 Windows 上執行測試指令 |
| Windows 10/11 | x64 | 目標平台 |

> **注意**：Microsoft.Playwright 為 R1 風險的備案，僅在 Phase 2 PoC 失敗時加入。首次安裝後需執行 `playwright.ps1 install chromium` 下載 chromium runtime（約 130 MB）。

---

## 2. NuGet 套件清單（依專案）

### 2.1 `Tender.Core`

| 套件 | 版本（建議） | 用途 |
|---|---|---|
| 無外部相依 | - | 純領域模型，不引入第三方套件 |

> System.Text.Json 為 .NET 8 內建，不需額外安裝。

### 2.2 `Tender.Storage`

| 套件 | 版本 | 用途 |
|---|---|---|
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.x | DI 介面 |

### 2.3 `Tender.Crawler`

| 套件 | 版本 | 用途 |
|---|---|---|
| Microsoft.Extensions.Hosting | 8.0.x | Generic Host（Console App lifetime + DI） |
| Microsoft.Extensions.Http | 8.0.x | HttpClientFactory |
| AngleSharp | 1.1.x | HTML 解析 |
| System.CommandLine | 2.0.0-beta4（或更新） | 命令列參數解析 |
| Polly | 8.x | 重試策略（指數退避） |
| Microsoft.Playwright | 1.48.x | **R1 風險備案**：僅在 PoC 失敗時加入 |

### 2.4 `Tender.Desktop`

| 套件 | 版本 | 用途 |
|---|---|---|
| CommunityToolkit.Mvvm | 8.3.x | MVVM 基礎（ObservableObject、RelayCommand） |
| Microsoft.Extensions.Hosting | 8.0.x | DI + Generic Host |
| ClosedXML | 0.104.x（或 0.102+） | Excel 匯出 |

### 2.5 `Tender.Installer`（WiX 4.x）

WiX 不使用 NuGet，由 .wixproj 與 wix CLI 處理。需另外安裝 `wix` dotnet tool：

```powershell
dotnet tool install --global wix --version 4.0.5
```

如需 Custom Action（C# 寫的 Managed CA），則新增一個獨立的 `Tender.Installer.CustomActions` Class Library 專案，加入：

| 套件 | 版本 |
|---|---|
| WixToolset.Dtf.WindowsInstaller | 4.0.x |
| WixToolset.DUtil | 4.0.x |

### 2.6 `tests/*`

| 套件 | 版本 | 用途 |
|---|---|---|
| xunit | 2.9.x | 單元測試框架 |
| xunit.runner.visualstudio | 2.8.x | VS / dotnet test runner |
| Microsoft.NET.Test.Sdk | 17.11.x | 測試 SDK |
| FluentAssertions | 6.12.x | 斷言可讀性 |
| Moq | 4.20.x | Mock 框架 |
| Reqnroll.xUnit | 2.x | BDD 框架（SpecFlow 後繼者） |
| Reqnroll.Tools.MsBuild.Generation | 2.x | 自動產生 .feature 對應的 .cs |
| Microsoft.Extensions.DependencyInjection | 8.0.x | 測試容器 |

> **注意**：Reqnroll 為 SpecFlow fork（SpecFlow 已於 2024 停止維護）。專案統一採用 Reqnroll，命名空間為 `Reqnroll`。

---

## 3. 建立 Solution 與專案的指令清單

於 `C:\WorkSpace\tender\` 根目錄執行：

```powershell
# 1. 建立 solution
dotnet new sln -n Tender

# 2. 建立各專案（依相依順序）
dotnet new classlib -n Tender.Core           -o src/Tender.Core           -f net8.0
dotnet new classlib -n Tender.Storage        -o src/Tender.Storage        -f net8.0
dotnet new console  -n Tender.Crawler        -o src/Tender.Crawler        -f net8.0
dotnet new wpf      -n Tender.Desktop        -o src/Tender.Desktop        -f net8.0

# 3. 測試專案
dotnet new xunit    -n Tender.Core.Tests          -o tests/Tender.Core.Tests          -f net8.0
dotnet new xunit    -n Tender.Storage.Tests       -o tests/Tender.Storage.Tests       -f net8.0
dotnet new xunit    -n Tender.Crawler.Tests       -o tests/Tender.Crawler.Tests       -f net8.0
dotnet new reqnroll-project -n Tender.AcceptanceTests -o tests/Tender.AcceptanceTests -f net8.0
# ↑ 若該模板不存在，改用 'dotnet new xunit' 後手動加入 Reqnroll NuGet

# 4. 加入 solution
dotnet sln Tender.sln add `
    src/Tender.Core/Tender.Core.csproj `
    src/Tender.Storage/Tender.Storage.csproj `
    src/Tender.Crawler/Tender.Crawler.csproj `
    src/Tender.Desktop/Tender.Desktop.csproj `
    tests/Tender.Core.Tests/Tender.Core.Tests.csproj `
    tests/Tender.Storage.Tests/Tender.Storage.Tests.csproj `
    tests/Tender.Crawler.Tests/Tender.Crawler.Tests.csproj `
    tests/Tender.AcceptanceTests/Tender.AcceptanceTests.csproj

# 5. 設定專案相依（依 architecture.md）
dotnet add src/Tender.Storage/Tender.Storage.csproj reference src/Tender.Core/Tender.Core.csproj

dotnet add src/Tender.Crawler/Tender.Crawler.csproj reference `
    src/Tender.Core/Tender.Core.csproj `
    src/Tender.Storage/Tender.Storage.csproj

dotnet add src/Tender.Desktop/Tender.Desktop.csproj reference `
    src/Tender.Core/Tender.Core.csproj `
    src/Tender.Storage/Tender.Storage.csproj

dotnet add tests/Tender.Core.Tests/Tender.Core.Tests.csproj reference src/Tender.Core/Tender.Core.csproj

dotnet add tests/Tender.Storage.Tests/Tender.Storage.Tests.csproj reference src/Tender.Storage/Tender.Storage.csproj

dotnet add tests/Tender.Crawler.Tests/Tender.Crawler.Tests.csproj reference src/Tender.Crawler/Tender.Crawler.csproj

dotnet add tests/Tender.AcceptanceTests/Tender.AcceptanceTests.csproj reference `
    src/Tender.Core/Tender.Core.csproj `
    src/Tender.Storage/Tender.Storage.csproj `
    src/Tender.Crawler/Tender.Crawler.csproj `
    src/Tender.Desktop/Tender.Desktop.csproj
```

### 3.1 安裝關鍵 NuGet 套件

```powershell
# Tender.Storage
dotnet add src/Tender.Storage package Microsoft.Extensions.DependencyInjection.Abstractions --version 8.0.2

# Tender.Crawler
dotnet add src/Tender.Crawler package Microsoft.Extensions.Hosting --version 8.0.1
dotnet add src/Tender.Crawler package Microsoft.Extensions.Http --version 8.0.1
dotnet add src/Tender.Crawler package AngleSharp --version 1.1.2
dotnet add src/Tender.Crawler package System.CommandLine --version 2.0.0-beta4.22272.1
dotnet add src/Tender.Crawler package Polly --version 8.4.2

# Tender.Desktop
dotnet add src/Tender.Desktop package CommunityToolkit.Mvvm --version 8.3.2
dotnet add src/Tender.Desktop package Microsoft.Extensions.Hosting --version 8.0.1
dotnet add src/Tender.Desktop package ClosedXML --version 0.104.2

# 測試專案
foreach ($proj in @(
    "tests/Tender.Core.Tests/Tender.Core.Tests.csproj",
    "tests/Tender.Storage.Tests/Tender.Storage.Tests.csproj",
    "tests/Tender.Crawler.Tests/Tender.Crawler.Tests.csproj"
)) {
    dotnet add $proj package FluentAssertions --version 6.12.1
    dotnet add $proj package Moq --version 4.20.72
}

dotnet add tests/Tender.AcceptanceTests package Reqnroll.xUnit --version 2.2.1
dotnet add tests/Tender.AcceptanceTests package Reqnroll.Tools.MsBuild.Generation --version 2.2.1
dotnet add tests/Tender.AcceptanceTests package FluentAssertions --version 6.12.1
dotnet add tests/Tender.AcceptanceTests package Moq --version 4.20.72
dotnet add tests/Tender.AcceptanceTests package Microsoft.Extensions.DependencyInjection --version 8.0.1
dotnet add tests/Tender.AcceptanceTests package ClosedXML --version 0.104.2
```

> 版本號為撰寫此文件時的當前穩定版，csharp-expert 階段執行 `dotnet add package` 時會自動取最新；若需鎖定版本可參考上述。

### 3.2 將 SA 階段的 .feature 與 Steps.cs 連結到測試專案

```powershell
# 將 sa/engineer_features 內的 .feature 複製或建立 link 到 tests/Tender.AcceptanceTests/Features/
New-Item -ItemType Directory -Path tests/Tender.AcceptanceTests/Features -Force
Copy-Item sa/engineer_features/*.feature tests/Tender.AcceptanceTests/Features/

# 將 sa/step_definitions 內的 .cs 複製到 tests/Tender.AcceptanceTests/Steps/
New-Item -ItemType Directory -Path tests/Tender.AcceptanceTests/Steps -Force
Copy-Item sa/step_definitions/*.cs tests/Tender.AcceptanceTests/Steps/
```

> 建議實作階段：以**複製**為起點而非 link，避免 SA 文件變動同時影響執行測試。後續若 PM/SA 文件更新，再手動同步。

---

## 4. 建置與執行指令

### 4.1 還原與建置

```powershell
dotnet restore
dotnet build -c Debug
```

### 4.2 執行所有測試

```powershell
dotnet test
```

### 4.3 執行特定專案的測試

```powershell
dotnet test tests/Tender.Core.Tests
dotnet test tests/Tender.AcceptanceTests
```

### 4.4 執行特定 Reqnroll Feature

```powershell
dotnet test tests/Tender.AcceptanceTests --filter "FullyQualifiedName~MonthlyCalendar"
```

### 4.5 執行桌面程式（開發中）

```powershell
dotnet run --project src/Tender.Desktop
```

### 4.6 執行爬蟲（手動）

```powershell
# 抓當天
dotnet run --project src/Tender.Crawler -- --mode manual --target-date 2026-05-08

# PoC 驗證網站可達（Phase 2）
dotnet run --project src/Tender.Crawler -- --mode poc --target-date 2026-05-08
```

### 4.7 發行與打包（Release）

```powershell
# 發行所有桌面端程式（Self-contained，避免使用者安裝 .NET runtime）
dotnet publish src/Tender.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish/desktop
dotnet publish src/Tender.Crawler -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish/crawler

# 建置 MSI（在 Tender.Installer 專案目錄下）
cd src/Tender.Installer
wix build Product.wxs -d "DesktopOutDir=..\..\publish\desktop" -d "CrawlerOutDir=..\..\publish\crawler" -o ..\..\publish\TenderSearch.msi
cd ..\..
```

---

## 5. 開發 Tips

- **Reqnroll 自動程式碼生成**：`.feature` 檔案改動後，IDE 應自動產生 `.feature.cs`。若沒有，執行 `dotnet build` 即可觸發 `Reqnroll.Tools.MsBuild.Generation`。
- **WPF + DI**：`Tender.Desktop` 採用 Generic Host 整合 WPF，於 `App.xaml.cs` 建立 `IHost`，把 `ShellWindow` 註冊為 ViewModel 注入的對象。
- **時區**：所有測試強制 UTC+8，可在 `Hooks.cs` 透過 `TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time")` 設定 `IClock` 的時區。
- **路徑差異**：開發機與測試容器的 `%LocalAppData%` 不同，所有對外 API 透過 `IDataPaths.DataRoot` 抽象，測試以臨時目錄替換。

---

## 6. CI/CD（建議）

雖非 MVP 必要，但建議首版即引入：

- **GitHub Actions**：在每次 push 觸發 `dotnet test`。
- **發行流程**：透過 release tag 觸發 `dotnet publish` 並上傳 MSI 到 GitHub Releases。

```yaml
# .github/workflows/ci.yml（範例骨架）
name: CI
on: [push, pull_request]
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release --logger "trx"
```
