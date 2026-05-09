namespace Tender.Core.DateConversion;

public interface ITaiwanDateConverter
{
    /// <summary>「115/05/08」 → DateOnly(2026,5,8)。解析失敗回傳 null。</summary>
    DateOnly? RocToDateOnly(string rocDate);

    /// <summary>DateOnly(2026,5,8) → 「115/05/08」。</summary>
    string DateOnlyToRoc(DateOnly date);

    /// <summary>判斷民國年字串是否在指定西元年區間內（含端點）。</summary>
    bool IsRocDateInRange(string rocDate, DateOnly from, DateOnly to);
}
