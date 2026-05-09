using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tender.Core.Models;
using Tender.Desktop.Services;
using Tender.Storage.Paths;
using Tender.Storage.Repositories;

namespace Tender.Desktop.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly Func<DailyQueryViewModel> _dailyVmFactory;
    private readonly ICrawlerLauncher _crawlerLauncher;
    private readonly IMissedRunDetector _missedRunDetector;
    private readonly IErrorSummaryDialog _errorDialog;
    private readonly IDataPaths _dataPaths;
    private readonly IDailySummaryRepository _summaryRepo;
    private readonly Func<Window> _keywordsManagerFactory;
    private readonly Func<Window> _appSettingsFactory;
    private readonly IUpdateChecker _updateChecker;
    private readonly IBrowserLauncher _browser;
    private readonly IDataCleanupService _cleanupService;
    private readonly INotificationService _notifications;

    public MonthlyCalendarViewModel CalendarVm { get; }

    [ObservableProperty]
    private DailyQueryViewModel? _currentDailyQueryVm;

    [ObservableProperty]
    private bool _isCrawlerRunning;

    [ObservableProperty]
    private string? _crawlerStatusMessage;

    [ObservableProperty]
    private double _crawlerProgressValue;

    /// <summary>今日是否已成功跑過爬蟲（影響「立即更新」按鈕顯示樣式）。</summary>
    [ObservableProperty]
    private bool _isTodayCrawled;

    /// <summary>GitHub 上是否有更新版本可下載。</summary>
    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private UpdateInfo? _updateInfo;

    public bool IsDailyQueryVisible => CurrentDailyQueryVm != null;

    public ShellViewModel(
        MonthlyCalendarViewModel calendarVm,
        Func<DailyQueryViewModel> dailyVmFactory,
        ICrawlerLauncher crawlerLauncher,
        IMissedRunDetector missedRunDetector,
        IErrorSummaryDialog errorDialog,
        IDataPaths dataPaths,
        IDailySummaryRepository summaryRepo,
        Func<Window> keywordsManagerFactory,
        Func<Window> appSettingsFactory,
        IUpdateChecker updateChecker,
        IBrowserLauncher browser,
        IDataCleanupService cleanupService,
        INotificationService notifications)
    {
        CalendarVm = calendarVm;
        _dailyVmFactory = dailyVmFactory;
        _crawlerLauncher = crawlerLauncher;
        _missedRunDetector = missedRunDetector;
        _errorDialog = errorDialog;
        _dataPaths = dataPaths;
        _summaryRepo = summaryRepo;
        _keywordsManagerFactory = keywordsManagerFactory;
        _appSettingsFactory = appSettingsFactory;
        _updateChecker = updateChecker;
        _browser = browser;
        _cleanupService = cleanupService;
        _notifications = notifications;
    }

    [RelayCommand]
    private void OpenUpdatePage()
    {
        var url = UpdateInfo?.DownloadUrl ?? UpdateInfo?.ReleasePageUrl;
        if (!string.IsNullOrEmpty(url))
            _browser.Open(url);
    }

    [RelayCommand]
    private async Task OpenKeywordsManagerAsync(CancellationToken ct)
    {
        var dlg = _keywordsManagerFactory();
        dlg.Owner = Application.Current.MainWindow;
        dlg.ShowDialog();

        // 對話框關閉後，重新載入當前查詢頁的 KeywordGroups 與資料
        if (CurrentDailyQueryVm != null)
        {
            CurrentDailyQueryVm.KeywordGroups.Clear();
            await CurrentDailyQueryVm.LoadCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private void OpenAppSettings()
    {
        var dlg = _appSettingsFactory();
        dlg.Owner = Application.Current.MainWindow;
        dlg.ShowDialog();
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        // 先檢查今日是否已成功跑過
        await RefreshTodayCrawledAsync(ct);

        // 載入行事曆（快速）
        await CalendarVm.LoadMonthCommand.ExecuteAsync(null);

        // 背景檢查 missed run、更新檢查、資料清理（不阻擋 UI）
        _ = CheckMissedRunInBackgroundAsync(ct);
        _ = CheckUpdateInBackgroundAsync(ct);
        _ = RunCleanupInBackgroundAsync(ct);
    }

    private async Task RunCleanupInBackgroundAsync(CancellationToken ct)
    {
        try
        {
            var result = await _cleanupService.CleanupAsync(ct);
            if (result.RemovedFolders > 0)
            {
                CrawlerStatusMessage = $"已自動清理 {result.RemovedFolders} 個過期資料夾";
                await CalendarVm.LoadMonthCommand.ExecuteAsync(null);
            }
        }
        catch
        {
            // 清理失敗不影響主功能
        }
    }

    private async Task CheckUpdateInBackgroundAsync(CancellationToken ct)
    {
        try
        {
            var info = await _updateChecker.CheckAsync(ct);
            UpdateInfo = info;
            IsUpdateAvailable = info.IsUpdateAvailable;
        }
        catch
        {
            // 更新檢查失敗不影響主功能
        }
    }

    private async Task RefreshTodayCrawledAsync(CancellationToken ct)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var summary = await _summaryRepo.LoadAsync(today, ct);
            IsTodayCrawled = summary?.LastRunStatus == RunStatus.Success;
        }
        catch
        {
            IsTodayCrawled = false;
        }
    }

    private async Task CheckMissedRunInBackgroundAsync(CancellationToken ct)
    {
        var progress = new Progress<CrawlerProgressEvent>(evt =>
        {
            CrawlerStatusMessage = $"補跑：{evt.Message}";
            if (evt.PercentComplete.HasValue)
                CrawlerProgressValue = evt.PercentComplete.Value;
        });

        try
        {
            var result = await _missedRunDetector.CheckAndCatchupAsync(progress, ct);
            if (result.CatchupTriggered)
            {
                CrawlerStatusMessage = result.Reason ?? "補跑完成";
                await CalendarVm.LoadMonthCommand.ExecuteAsync(null);
                await RefreshTodayCrawledAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // app closing, ignore
        }
        catch (Exception ex)
        {
            CrawlerStatusMessage = $"補跑偵測失敗：{ex.Message}";
        }
        finally
        {
            IsCrawlerRunning = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToDayAsync(DateOnly date)
    {
        // 失敗日：直接彈錯誤摘要，不切換到查詢頁（因為通常無資料）
        var day = CalendarVm.View?.Days.FirstOrDefault(d => d.Date == date);
        if (day?.Summary?.LastRunStatus == RunStatus.Failed)
        {
            await _errorDialog.ShowAsync(
                date,
                day.Summary.ErrorMessage ?? "未知錯誤",
                _dataPaths.ErrorsLogFile(date));
            return;
        }

        var vm = _dailyVmFactory();
        vm.Date = date;
        CurrentDailyQueryVm = vm;
        OnPropertyChanged(nameof(IsDailyQueryVisible));
        await vm.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void NavigateBackToCalendar()
    {
        CurrentDailyQueryVm = null;
        OnPropertyChanged(nameof(IsDailyQueryVisible));
    }

    [RelayCommand(CanExecute = nameof(CanRunCrawler))]
    private async Task RunCrawlerNowAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var progress = new Progress<CrawlerProgressEvent>(evt =>
        {
            CrawlerStatusMessage = evt.Message;
            if (evt.PercentComplete.HasValue)
                CrawlerProgressValue = evt.PercentComplete.Value;
        });

        try
        {
            IsCrawlerRunning = true;
            RunCrawlerNowCommand.NotifyCanExecuteChanged();

            CrawlerStatusMessage = "啟動爬蟲...";
            CrawlerProgressValue = 0;

            var exitCode = await _crawlerLauncher.LaunchAsync(
                CrawlerMode.Manual, today, progress, ct);

            if (exitCode == 0)
            {
                // 成功：清除狀態訊息，按鈕轉綠色（透過 IsTodayCrawled）
                CrawlerStatusMessage = null;
                CrawlerProgressValue = 0;
                _notifications.ShowInfo("標案查詢", $"今日（{today:yyyy-MM-dd}）爬蟲已完成");
            }
            else if (exitCode == 4)
            {
                CrawlerStatusMessage = "另一個爬蟲已在執行";
            }
            else
            {
                CrawlerStatusMessage = $"爬蟲結束，exit code = {exitCode}";
            }

            // 重新整理行事曆、查詢頁、今日狀態
            await CalendarVm.LoadMonthCommand.ExecuteAsync(null);
            if (CurrentDailyQueryVm != null && CurrentDailyQueryVm.Date == today)
                await CurrentDailyQueryVm.LoadCommand.ExecuteAsync(null);
            await RefreshTodayCrawledAsync(ct);
        }
        catch (Exception ex)
        {
            CrawlerStatusMessage = $"啟動失敗：{ex.Message}";
            MessageBox.Show(ex.ToString(), "Crawler 啟動失敗", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsCrawlerRunning = false;
            RunCrawlerNowCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRunCrawler() => !IsCrawlerRunning;
}
