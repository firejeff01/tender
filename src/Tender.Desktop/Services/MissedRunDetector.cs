using Tender.Core.Clock;
using Tender.Core.Models;
using Tender.Storage.Repositories;

namespace Tender.Desktop.Services;

public sealed class MissedRunDetector : IMissedRunDetector
{
    private readonly IDailySummaryRepository _summaryRepo;
    private readonly ICrawlerLauncher _launcher;
    private readonly IClock _clock;
    private readonly IAppSettingsRepository _settingsRepo;

    public MissedRunDetector(
        IDailySummaryRepository summaryRepo,
        ICrawlerLauncher launcher,
        IClock clock,
        IAppSettingsRepository settingsRepo)
    {
        _summaryRepo = summaryRepo;
        _launcher = launcher;
        _clock = clock;
        _settingsRepo = settingsRepo;
    }

    public async Task<MissedRunResult> CheckAndCatchupAsync(
        IProgress<CrawlerProgressEvent>? progress = null,
        CancellationToken ct = default)
    {
        var now = _clock.Now.LocalDateTime;
        var today = DateOnly.FromDateTime(now);

        // 讀取使用者設定
        AppSettings settings;
        try { settings = await _settingsRepo.LoadAsync(ct); }
        catch { settings = new AppSettings(); }

        if (!settings.CatchupEnabled)
            return new MissedRunResult(false, false, "已停用啟動補跑（應用設定中）");

        // 解析排程時間 HH:mm
        var scheduledHour = 17;
        var scheduledMinute = 0;
        if (TimeOnly.TryParseExact(settings.ScheduledTime, "HH:mm", out var t))
        {
            scheduledHour = t.Hour;
            scheduledMinute = t.Minute;
        }
        var scheduledNow = new DateTime(today.Year, today.Month, today.Day, scheduledHour, scheduledMinute, 0);

        if (now < scheduledNow)
            return new MissedRunResult(false, false, $"{settings.ScheduledTime} 前不偵測（現在 {now:HH:mm}）");

        DailySummary? summary;
        try
        {
            summary = await _summaryRepo.LoadAsync(today, ct);
        }
        catch
        {
            // 損毀也算 missed，補跑會覆蓋
            summary = null;
        }

        if (summary != null && summary.LastRunStatus == RunStatus.Success)
            return new MissedRunResult(false, false, "今日已成功執行");

        try
        {
            var exitCode = await _launcher.LaunchAsync(CrawlerMode.Catchup, today, progress, ct);
            return new MissedRunResult(
                WasMissed: true,
                CatchupTriggered: true,
                Reason: exitCode == 0 ? "補跑成功" : $"補跑結束 exit={exitCode}");
        }
        catch (Exception ex)
        {
            return new MissedRunResult(true, false, $"補跑失敗：{ex.Message}");
        }
    }
}
