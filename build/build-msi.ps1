#requires -Version 5.1
<#
.SYNOPSIS
    Build TenderSearch.msi installer.

.DESCRIPTION
    1. Publish Tender.Crawler (framework-dependent, win-x64) → publish/crawler/
    2. Publish Tender.Desktop (framework-dependent, win-x64) → publish/desktop/
    3. Build src/Tender.Installer/Tender.Installer.wixproj → TenderSearch.msi
    4. Copy MSI to dist/

.EXAMPLE
    .\build\build-msi.ps1
    .\build\build-msi.ps1 -Configuration Debug
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    # 預設 self-contained（含 .NET 10 runtime，~80MB，使用者不用先裝 runtime）。
    # 加 -Slim 改成 framework-dependent（~5MB，但使用者要先裝 .NET 10 Desktop Runtime）。
    [switch]$Slim,
    # 舊參數，保留向下相容（與 Slim 相反義）
    [switch]$SelfContained,
    # 強制指定版本號；省略時自動依目前時間產生 1.{year-2025}.{MMDD}.{HHmm}
    [string]$Version
)

# 決定 self-contained：預設 true，-Slim 才轉成 false
$useSelfContained = -not $Slim

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$pubRoot = Join-Path $repoRoot 'publish'
$distRoot = Join-Path $repoRoot 'dist'

# 自動產生版本號（時間遞增 → MajorUpgrade 自動覆蓋舊版）
#
# ⚠️ MSI ProductVersion 限制：
#   - 前 3 段 (Major.Minor.Build) 才會用於升級比較，第 4 段 (Revision) 被忽略
#   - Major 上限 255、Minor 上限 255、Build 上限 65535
#
# 編碼方式（前 3 段都會被 MSI 用於比較，且每分鐘 build 都單調遞增）：
#   Major = 1                                                  (產品版本線，固定)
#   Minor = (Year-2026)*12 + (Month-1)                         (每月遞增；~21 年後達 255)
#   Build = (Day-1)*1440 + Hour*60 + Minute                    (月內每分鐘遞增；最大 44639)
#   Rev   = Month*100 + Day                                    (純資訊提示，看版號可推日期)
if (-not $Version) {
    $now = Get-Date
    $minor = ($now.Year - 2026) * 12 + ($now.Month - 1)
    $build = ($now.Day - 1) * 1440 + $now.Hour * 60 + $now.Minute
    $rev = $now.Month * 100 + $now.Day
    $Version = "1.$minor.$build.$rev"
}

Write-Host "=== TenderSearch MSI Build ===" -ForegroundColor Cyan
Write-Host "Repo: $repoRoot"
Write-Host "Configuration: $Configuration"
Write-Host ("Mode: {0}" -f $(if ($useSelfContained) { 'self-contained (含 .NET 10 runtime, ~80MB)' } else { 'framework-dependent (slim, ~5MB)' }))
Write-Host "Version: $Version"

# Step 1: Clean publish/dist
if (Test-Path $pubRoot) { Remove-Item $pubRoot -Recurse -Force }
if (-not (Test-Path $distRoot)) { New-Item -ItemType Directory -Path $distRoot | Out-Null }

# Step 2: Publish projects
$selfContainedFlag = if ($useSelfContained) { 'true' } else { 'false' }

Write-Host ""
Write-Host "[1/3] Publishing Tender.Crawler..." -ForegroundColor Yellow
& dotnet publish (Join-Path $repoRoot 'src\Tender.Crawler\Tender.Crawler.csproj') `
    -c $Configuration -r win-x64 --self-contained $selfContainedFlag `
    -o (Join-Path $pubRoot 'crawler') --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Crawler publish failed" }

Write-Host "[2/3] Publishing Tender.Desktop..." -ForegroundColor Yellow
& dotnet publish (Join-Path $repoRoot 'src\Tender.Desktop\Tender.Desktop.csproj') `
    -c $Configuration -r win-x64 --self-contained $selfContainedFlag `
    -o (Join-Path $pubRoot 'desktop') --nologo --verbosity quiet `
    -p:Version=$Version -p:AssemblyVersion=$Version -p:FileVersion=$Version
if ($LASTEXITCODE -ne 0) { throw "Desktop publish failed" }

Write-Host "[3/3] Building MSI (version $Version)..." -ForegroundColor Yellow
& dotnet build (Join-Path $repoRoot 'src\Tender.Installer\Tender.Installer.wixproj') `
    -c $Configuration --nologo --verbosity quiet `
    -p:BuildVersion=$Version
if ($LASTEXITCODE -ne 0) { throw "MSI build failed" }

# Step 4: Copy MSI to dist
$msiSrc = Join-Path $repoRoot "src\Tender.Installer\bin\x64\$Configuration\TenderSearch.msi"
$msiDest = Join-Path $distRoot 'TenderSearch.msi'
Copy-Item $msiSrc $msiDest -Force

$info = Get-Item $msiDest
Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host ("MSI: {0}" -f $msiDest)
Write-Host ("Version: {0}" -f $Version)
Write-Host ("Size: {0:N1} MB" -f ($info.Length / 1MB))
