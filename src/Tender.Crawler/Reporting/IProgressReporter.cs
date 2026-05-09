namespace Tender.Crawler.Reporting;

/// <summary>
/// 將進度事件以 JSON Lines 格式寫到 stdout，供桌面程式解析顯示進度條。
/// </summary>
public interface IProgressReporter
{
    void Report(ProgressEvent evt);
}

public sealed record ProgressEvent(
    string Stage,
    string Message,
    int? PageNumber,
    double? PercentComplete);
