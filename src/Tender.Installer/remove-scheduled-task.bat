@echo off
setlocal

set TASK_NAME=TenderSearch.DailyCrawl

echo 移除排程任務 %TASK_NAME%
schtasks /Delete /TN "%TASK_NAME%" /F

if %ERRORLEVEL% EQU 0 (
    echo 排程任務已移除
) else (
    echo [Warn] 移除失敗（可能該任務不存在）
)

pause
