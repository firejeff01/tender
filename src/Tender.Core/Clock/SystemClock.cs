namespace Tender.Core.Clock;

/// <summary>
/// 正式執行環境使用的系統時鐘，回傳真實時間。
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;

    public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}
