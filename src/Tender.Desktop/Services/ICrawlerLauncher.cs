using Tender.Core.Models;

namespace Tender.Desktop.Services;

/// <summary>
/// 進度事件，與 Tender.Crawler.Reporting.ProgressEvent 對應的 JSON 結構。
/// 兩端各自定義避免桌面層需要 reference Crawler 組件。
/// </summary>
public sealed record CrawlerProgressEvent(
    string Stage,
    string Message,
    int? PageNumber,
    double? PercentComplete);

public interface ICrawlerLauncher
{
    /// <summary>
    /// 啟動 Tender.Crawler.exe 子程序並等待結束。
    /// 透過 stdout JSON Lines 解析 ProgressEvent 並轉發給呼叫端。
    /// </summary>
    /// <returns>子程序的 Exit Code（0=success, 1=network, 2=parse, 3=io, 4=locked, 5=invalid args）。</returns>
    Task<int> LaunchAsync(
        CrawlerMode mode,
        DateOnly targetDate,
        IProgress<CrawlerProgressEvent>? progress,
        CancellationToken ct = default);
}
