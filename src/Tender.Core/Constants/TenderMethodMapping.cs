namespace Tender.Core.Constants;

/// <summary>
/// 招標方式業務名稱與政府電子採購網實際 option/value 的對應表。
/// Phase 2 PoC 驗證結果（2026-05-09）：
///   解析 https://web.pcc.gov.tw/prkms/tender/common/advanced/readTenderAdvanced 頁面的 select 元素，
///   確認三種目標招標方式的正確 option value。
/// </summary>
public static class TenderMethodMapping
{
    /// <summary>
    /// 業務名稱 → 政府網站 option value（已於 PoC 2026-05-09 確認）。
    /// </summary>
    public static IReadOnlyDictionary<string, string> BusinessNameToOptionValue { get; } =
        new Dictionary<string, string>
        {
            { "公開招標", "TENDER_WAY_1" },
            { "公開取得電子報價單", "TENDER_WAY_12" },
            { "經公開評選或公開徵求之限制性招標", "TENDER_WAY_4" },
        };

    /// <summary>網站文字 → 業務名稱反向對應（用於解析時正規化）。</summary>
    public static string NormalizeFromWebText(string webText)
    {
        foreach (var key in BusinessNameToOptionValue.Keys)
        {
            if (webText.Contains(key, StringComparison.OrdinalIgnoreCase))
                return key;
        }
        return webText;
    }
}
