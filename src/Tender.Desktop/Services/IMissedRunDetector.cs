namespace Tender.Desktop.Services;

public sealed record MissedRunResult(bool WasMissed, bool CatchupTriggered, string? Reason);

public interface IMissedRunDetector
{
    /// <summary>
    /// 在桌面程式啟動時呼叫，偵測今日是否有 missed run。
    /// 17:00 已過但 summary.json 不存在或 status != success → 觸發 Catchup。
    /// </summary>
    Task<MissedRunResult> CheckAndCatchupAsync(
        IProgress<CrawlerProgressEvent>? progress = null,
        CancellationToken ct = default);
}
