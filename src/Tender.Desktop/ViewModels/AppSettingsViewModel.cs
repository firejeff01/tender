using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tender.Core.Constants;
using Tender.Core.Models;
using Tender.Desktop.Services;
using Tender.Storage.Paths;
using Tender.Storage.Repositories;

namespace Tender.Desktop.ViewModels;

public partial class AppSettingsViewModel : ObservableObject
{
    private readonly IAppSettingsRepository _repo;
    private readonly IScheduledTaskInstaller _taskInstaller;
    private readonly IDataPaths _paths;

    [ObservableProperty]
    private string _scheduledTime = "17:00";

    [ObservableProperty]
    private bool _catchupEnabled = true;

    [ObservableProperty]
    private bool _updateCheckEnabled = true;

    [ObservableProperty]
    private int _requestDelayMs = 1500;

    [ObservableProperty]
    private int _maxRetries = 3;

    [ObservableProperty]
    private int _dataRetentionMonths = 6;

    [ObservableProperty]
    private string _dataRoot = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    /// <summary>登入時 IDataPaths 回報的 DataRoot，用於 Save 時判斷是否需要搬移。</summary>
    private string _initialDataRoot = string.Empty;

    public string DefaultDataRoot => _paths.DefaultDataRoot;

    public ObservableCollection<TenderMethodOption> TenderMethodOptions { get; } = new();

    public AppSettingsViewModel(
        IAppSettingsRepository repo,
        IScheduledTaskInstaller taskInstaller,
        IDataPaths paths)
    {
        _repo = repo;
        _taskInstaller = taskInstaller;
        _paths = paths;
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
            UpdateCheckEnabled = s.UpdateCheckEnabled;
            RequestDelayMs = s.RequestDelayMs;
            MaxRetries = s.MaxRetries;
            DataRetentionMonths = s.DataRetentionMonths;

            _initialDataRoot = _paths.DataRoot;
            DataRoot = _initialDataRoot;

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
    private void BrowseDataRoot()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "選擇資料儲存位置",
            InitialDirectory = Directory.Exists(DataRoot) ? DataRoot : _paths.DefaultDataRoot,
        };
        if (dlg.ShowDialog() == true)
            DataRoot = dlg.FolderName;
    }

    [RelayCommand]
    private void ResetDataRoot() => DataRoot = _paths.DefaultDataRoot;

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

            // 處理 DataRoot 變更：先正規化、檢查、詢問搬移
            var newRoot = string.IsNullOrWhiteSpace(DataRoot)
                ? _paths.DefaultDataRoot
                : Path.GetFullPath(DataRoot);
            var rootChanged = !string.Equals(newRoot, _initialDataRoot, StringComparison.OrdinalIgnoreCase);

            if (rootChanged && IsNestedPath(_initialDataRoot, newRoot))
            {
                StatusMessage = "新路徑不可為舊路徑的子目錄（反之亦然）";
                return;
            }

            // 先存設定到目前（舊）位置，搬移時會跟著一起過去
            var settingsToSave = new AppSettings
            {
                ScheduledTime = ScheduledTime,
                CatchupEnabled = CatchupEnabled,
                UpdateCheckEnabled = UpdateCheckEnabled,
                RequestDelayMs = RequestDelayMs,
                MaxRetries = MaxRetries,
                DataRetentionMonths = DataRetentionMonths,
                TargetTenderMethods = selected.AsReadOnly(),
            };
            await _repo.SaveAsync(settingsToSave, ct);

            if (rootChanged)
            {
                var migrate = AskMigrate(_initialDataRoot, newRoot);
                if (migrate == MessageBoxResult.Cancel)
                {
                    StatusMessage = "已取消變更資料儲存位置";
                    return;
                }
                if (migrate == MessageBoxResult.Yes)
                {
                    try
                    {
                        MigrateDirectory(_initialDataRoot, newRoot);
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"搬移失敗，未變更位置：{ex.Message}";
                        return;
                    }
                }

                _paths.ChangeRoot(newRoot);
                _initialDataRoot = _paths.DataRoot;
            }

            HasUnsavedChanges = false;

            // 同步 Task Scheduler：以使用者層級覆寫
            var taskOk = _taskInstaller.EnsureTask(ScheduledTime);
            var baseMsg = taskOk
                ? $"儲存成功，每日排程已設定為 {ScheduledTime}"
                : $"儲存成功，但建立排程失敗（可能找不到 Crawler 執行檔）";
            StatusMessage = rootChanged ? baseMsg + "；資料位置已更新" : baseMsg;
        }
        catch (Exception ex)
        {
            StatusMessage = $"儲存失敗：{ex.Message}";
        }
    }

    /// <summary>
    /// 詢問是否搬移舊 DataRoot 內容到新位置。
    /// 舊位置不存在或空目錄時，直接回 No（不需搬移）。
    /// </summary>
    private static MessageBoxResult AskMigrate(string oldRoot, string newRoot)
    {
        if (!Directory.Exists(oldRoot)) return MessageBoxResult.No;
        var hasContent = Directory.EnumerateFileSystemEntries(oldRoot).Any();
        if (!hasContent) return MessageBoxResult.No;

        return MessageBox.Show(
            $"資料儲存位置將變更為：\n{newRoot}\n\n是否將現有資料從舊位置搬移過去？\n\n舊位置：{oldRoot}\n\n是 = 複製後刪除舊位置\n否 = 保留舊資料，新位置從零開始\n取消 = 不變更位置",
            "資料搬移",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
    }

    private static bool IsNestedPath(string a, string b)
    {
        var fa = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fb = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fa.StartsWith(fb, StringComparison.OrdinalIgnoreCase)
            || fb.StartsWith(fa, StringComparison.OrdinalIgnoreCase);
    }

    private static void MigrateDirectory(string source, string dest)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(dest);

        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, dir);
            Directory.CreateDirectory(Path.Combine(dest, rel));
        }
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            File.Copy(file, Path.Combine(dest, rel), overwrite: true);
        }

        // 搬移完成後刪除舊位置；若刪除失敗（檔案被鎖定）回報錯誤，但複製已經完成
        Directory.Delete(source, recursive: true);
    }

    partial void OnScheduledTimeChanged(string value) { HasUnsavedChanges = true; }
    partial void OnCatchupEnabledChanged(bool value) { HasUnsavedChanges = true; }
    partial void OnUpdateCheckEnabledChanged(bool value) { HasUnsavedChanges = true; }
    partial void OnRequestDelayMsChanged(int value) { HasUnsavedChanges = true; }
    partial void OnMaxRetriesChanged(int value) { HasUnsavedChanges = true; }
    partial void OnDataRetentionMonthsChanged(int value) { HasUnsavedChanges = true; }
    partial void OnDataRootChanged(string value) { HasUnsavedChanges = true; }
}

public partial class TenderMethodOption : ObservableObject
{
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected = true;

    public TenderMethodOption(string name) { Name = name; }
}
