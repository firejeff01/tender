using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tender.Core.Constants;
using Tender.Core.Models;
using Tender.Desktop.Services;
using Tender.Storage.Repositories;

namespace Tender.Desktop.ViewModels;

public partial class AppSettingsViewModel : ObservableObject
{
    private readonly IAppSettingsRepository _repo;

    [ObservableProperty]
    private string _scheduledTime = "17:00";

    [ObservableProperty]
    private bool _catchupEnabled = true;

    [ObservableProperty]
    private int _requestDelayMs = 1500;

    [ObservableProperty]
    private int _maxRetries = 3;

    [ObservableProperty]
    private int _dataRetentionMonths = 6;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    public ObservableCollection<TenderMethodOption> TenderMethodOptions { get; } = new();

    public AppSettingsViewModel(IAppSettingsRepository repo)
    {
        _repo = repo;
        // 列出所有支援的招標方式（從 TenderMethodMapping 取出名稱）
        foreach (var name in TenderMethodMapping.BusinessNameToOptionValue.Keys)
        {
            var opt = new TenderMethodOption(name);
            opt.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TenderMethodOption.IsSelected))
                    HasUnsavedChanges = true;
            };
            TenderMethodOptions.Add(opt);
        }
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            var s = await _repo.LoadAsync(ct);
            ScheduledTime = s.ScheduledTime;
            CatchupEnabled = s.CatchupEnabled;
            RequestDelayMs = s.RequestDelayMs;
            MaxRetries = s.MaxRetries;
            DataRetentionMonths = s.DataRetentionMonths;

            // 還原招標方式勾選狀態
            var saved = new HashSet<string>(s.TargetTenderMethods);
            foreach (var opt in TenderMethodOptions)
                opt.IsSelected = saved.Contains(opt.Name);

            HasUnsavedChanges = false;
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"載入失敗：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            // 簡單驗證
            if (!System.Text.RegularExpressions.Regex.IsMatch(ScheduledTime, @"^\d{2}:\d{2}$"))
            {
                StatusMessage = "排程時間格式錯誤（應為 HH:mm）";
                return;
            }
            if (RequestDelayMs < 0 || RequestDelayMs > 60_000)
            {
                StatusMessage = "請求間隔應介於 0 ~ 60000 毫秒";
                return;
            }
            if (MaxRetries < 0 || MaxRetries > 10)
            {
                StatusMessage = "重試次數應介於 0 ~ 10";
                return;
            }

            var selected = TenderMethodOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList();
            if (selected.Count == 0)
            {
                StatusMessage = "至少需勾選一種招標方式";
                return;
            }

            var s = new AppSettings
            {
                ScheduledTime = ScheduledTime,
                CatchupEnabled = CatchupEnabled,
                RequestDelayMs = RequestDelayMs,
                MaxRetries = MaxRetries,
                DataRetentionMonths = DataRetentionMonths,
                TargetTenderMethods = selected.AsReadOnly(),
            };
            await _repo.SaveAsync(s, ct);
            HasUnsavedChanges = false;

            // 同步 Task Scheduler：不存在就建立、已存在就以新時間覆寫（/Create /F 同時涵蓋兩種情況）
            var taskOk = TryCreateOrUpdateScheduledTask(ScheduledTime);
            StatusMessage = taskOk
                ? $"儲存成功，每日排程已設定為 {ScheduledTime}"
                : $"儲存成功，但建立排程失敗（可能找不到 Crawler 執行檔或權限不足）";
        }
        catch (Exception ex)
        {
            StatusMessage = $"儲存失敗：{ex.Message}";
        }
    }

    /// <summary>
    /// 用 schtasks /Create /F 建立或覆寫每日排程任務。
    /// 屬於使用者層級任務（以目前登入帳號身份執行），不需 admin 權限。
    /// </summary>
    private static bool TryCreateOrUpdateScheduledTask(string hhmm)
    {
        var crawlerExe = CrawlerLauncher.FindCrawlerExe();
        if (crawlerExe is null) return false;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "schtasks.exe",
                // /F = force overwrite，效果等於 create-or-update
                Arguments =
                    $"/Create /SC DAILY /TN \"TenderSearch.DailyCrawl\" " +
                    $"/TR \"\\\"{crawlerExe}\\\" --mode scheduled\" " +
                    $"/ST {hhmm} /F",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    partial void OnScheduledTimeChanged(string value) { HasUnsavedChanges = true; }
    partial void OnCatchupEnabledChanged(bool value) { HasUnsavedChanges = true; }
    partial void OnRequestDelayMsChanged(int value) { HasUnsavedChanges = true; }
    partial void OnMaxRetriesChanged(int value) { HasUnsavedChanges = true; }
    partial void OnDataRetentionMonthsChanged(int value) { HasUnsavedChanges = true; }
}

public partial class TenderMethodOption : ObservableObject
{
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected = true;

    public TenderMethodOption(string name) { Name = name; }
}
