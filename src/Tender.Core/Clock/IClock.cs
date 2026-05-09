namespace Tender.Core.Clock;

/// <summary>
/// 時鐘抽象，便於測試時注入固定時間。
/// </summary>
public interface IClock
{
    /// <summary>取得目前時間（含時區）。</summary>
    DateTimeOffset Now { get; }

    /// <summary>取得今日日期（以系統/注入時區為準）。</summary>
    DateOnly Today { get; }
}
