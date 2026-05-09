using Tender.Core.Models;

namespace Tender.Core.Keywords;

/// <summary>
/// 關鍵字命中服務。
/// - 寫入端：爬蟲在去重後對每筆標案標註 matchedKeywords。
/// - 查詢端：日期查詢頁的關鍵字按鈕透過此服務判定是否符合。
/// </summary>
public interface IKeywordMatcher
{
    /// <summary>
    /// 對單筆標案計算所有命中的關鍵字。
    /// 比對規則：採模糊查詢（子字串包含即命中），依 KeywordItem.TargetField 決定比對欄位。
    /// </summary>
    IReadOnlyList<string> Match(TenderItem item, KeywordSet keywordSet);

    /// <summary>
    /// 判定一筆標案是否命中指定關鍵字（用於 UI 篩選按鈕點擊後的判定）。
    /// </summary>
    bool IsMatch(TenderItem item, string keyword, string targetField);
}
