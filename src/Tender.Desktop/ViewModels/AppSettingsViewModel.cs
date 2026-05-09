using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    public AppSettingsViewModel(IAppSettingsRepository repo)
    {
        _repo = repo;
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
            HasUnsavedChanges = false;
            StatusMessage = "已載入";
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

            var s = new AppSettings
            {
                ScheduledTime = ScheduledTime,
                CatchupEnabled = CatchupEnabled,
                RequestDelayMs = RequestDelayMs,
                MaxRetries = MaxRetries,
            };
            await _repo.SaveAsync(s, ct);
            HasUnsavedChanges = false;
            StatusMessage = "儲存成功";
        }
        catch (Exception ex)
        {
            StatusMessage = $"儲存失敗：{ex.Message}";
        }
    }

    partial void OnScheduledTimeChanged(string value) { HasUnsavedChanges = true; }
    partial void OnCatchupEnabledChanged(bool value) { HasUnsavedChanges = true; }
    partial void OnRequestDelayMsChanged(int value) { HasUnsavedChanges = true; }
    partial void OnMaxRetriesChanged(int value) { HasUnsavedChanges = true; }
}
