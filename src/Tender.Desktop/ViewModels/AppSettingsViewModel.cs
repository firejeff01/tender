using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tender.Core.Constants;
using Tender.Core.Models;
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

            // 偵測排程時間是否變更，若有變更則嘗試同步 Task Scheduler
            var oldSettings = await _repo.LoadAsync(ct);
            var scheduledTimeChanged = oldSettings.ScheduledTime != ScheduledTime;

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

            if (scheduledTimeChanged)
            {
                var taskUpdated = TryUpdateScheduledTask(ScheduledTime);
                StatusMessage = taskUpdated
                    ? $"儲存成功，排程時間已更新為 {ScheduledTime}"
                    : $"儲存成功，但更新 Task Scheduler 失敗（任務可能不存在或無權限），請手動執行「建立每日排程」捷徑";
            }
            else
            {
                StatusMessage = "儲存成功";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"儲存失敗：{ex.Message}";
        }
    }

    /// <summary>
    /// 透過 schtasks /Change 修改既有任務的觸發時間。
    /// 若任務不存在 / 沒權限會回傳 false。
    /// </summary>
    private static bool TryUpdateScheduledTask(string hhmm)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Change /TN \"TenderSearch.DailyCrawl\" /ST {hhmm}",
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
