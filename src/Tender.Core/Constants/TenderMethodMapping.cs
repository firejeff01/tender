namespace Tender.Core.Constants;

/// <summary>
/// 招標方式業務名稱與政府電子採購網實際 option/value 的對應表。
/// 驗證日期 2026-05-11：解析 https://web.pcc.gov.tw/prkms/tender/common/advanced/readTenderAdvanced
/// 頁面的 select[name=tenderWay] 元素，確認三種目標招標方式的正確 option value。
/// 註：政府網站同時存在「公開取得電子報價單」(TENDER_WAY_12) 與
///     「公開取得報價單或企劃書」(TENDER_WAY_2) 兩個不同項目；本系統採用後者。
/// </summary>
public static class TenderMethodMapping
{
    /// <summary>
    /// 業務名稱 → 政府網站 option value（已於 2026-05-11 確認）。
    /// </summary>
    public static IReadOnlyDictionary<string, string> BusinessNameToOptionValue { get; } =
        new Dictionary<string, string>
        {
            { "公開招標", "TENDER_WAY_1" },
            { "公開取得報價單或企劃書", "TENDER_WAY_2" },
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

    /// <summary>
    /// 已重新命名的舊業務名稱 → 目前業務名稱。
    /// 讀取使用者既有的 settings.json 時用，避免重新命名後勾選狀態遺失。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> LegacyNameAliases =
        new Dictionary<string, string>
        {
            // 2026-05-11：PM 訂正，原預設「公開取得電子報價單」(TENDER_WAY_12)
            // 改為「公開取得報價單或企劃書」(TENDER_WAY_2)，前者為政府網站另一個招標方式。
            { "公開取得電子報價單", "公開取得報價單或企劃書" },
        };

    /// <summary>若 name 是已重新命名的舊名稱，回傳新名稱；否則原值返回。</summary>
    public static string MigrateLegacyName(string name)
        => LegacyNameAliases.TryGetValue(name, out var current) ? current : name;
}
