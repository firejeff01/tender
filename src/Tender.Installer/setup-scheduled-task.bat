@echo off
setlocal

REM 建立每日 17:00 自動爬取的 Task Scheduler 任務
REM 此 .bat 必須以系統管理員身份執行

set TASK_NAME=TenderSearch.DailyCrawl
set CRAWLER_PATH=%~dp0..\crawler\Tender.Crawler.exe

if not exist "%CRAWLER_PATH%" (
    echo [Error] 找不到 %CRAWLER_PATH%
    pause
    exit /b 1
)

echo 建立排程任務 %TASK_NAME%
echo 執行檔：%CRAWLER_PATH%
echo 觸發時間：每日 17:00

schtasks /Create ^
    /SC DAILY ^
    /TN "%TASK_NAME%" ^
    /TR "\"%CRAWLER_PATH%\" --mode scheduled" ^
    /ST 17:00 ^
    /RL LIMITED ^
    /F

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [Error] 建立排程失敗，請以系統管理員身份執行此 .bat
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo 排程任務建立成功
echo 可在「Task Scheduler」中查看 %TASK_NAME%
pause
