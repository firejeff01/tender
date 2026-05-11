using System.Text.Json.Serialization;

namespace Tender.Core.Models;

/// <summary>
/// 單筆標案資料，對應 tenders.json 內 items[] 元素。
/// 同日內以 SourcePk 為唯一鍵。
/// </summary>
public sealed record TenderItem
{
    /// <summary>
    /// 政府電子採購網標案唯一識別碼，建議取自 detail URL 的 pk 參數。
    /// 例：「NzEyMTUxODc=」（base64 字串）
    /// </summary>
    [JsonPropertyName("sourcePk")]
    public required string SourcePk { get; init; }

    /// <summary>機關名稱。</summary>
    [JsonPropertyName("agencyName")]
    public required string AgencyName { get; init; }

    /// <summary>機關代碼。</summary>
    [JsonPropertyName("agencyCode")]
    public string? AgencyCode { get; init; }

    /// <summary>標案名稱。</summary>
    [JsonPropertyName("tenderName")]
    public required string TenderName { get; init; }

    /// <summary>標案案號。</summary>
    [JsonPropertyName("tenderNo")]
    public string? TenderNo { get; init; }

    /// <summary>
    /// 招標方式（保留來源網站文字，例：「公開招標」、「公開取得報價單或企劃書」）。
    /// 業務分類請參考 TenderMethod enum 的對應表。
    /// </summary>
    [JsonPropertyName("tenderMethod")]
    public required string TenderMethod { get; init; }

    /// <summary>採購性質（例：「財物類」、「勞務類」、「工程類」）。</summary>
    [JsonPropertyName("procurementType")]
    public string? ProcurementType { get; init; }

    /// <summary>
    /// 公告日期，民國年格式（例：「115/05/08」）。保留來源格式以利匯出再用 Excel 篩選。
    /// 解析為西元年請使用 ITaiwanDateConverter。
    /// </summary>
    [JsonPropertyName("announcementDate")]
    public required string AnnouncementDate { get; init; }

    /// <summary>截止投標日期，民國年格式。</summary>
    [JsonPropertyName("bidDeadline")]
    public string? BidDeadline { get; init; }

    /// <summary>預算金額，單位為新台幣元。null 表示未公告金額或解析失敗。</summary>
    [JsonPropertyName("budgetAmount")]
    public long? BudgetAmount { get; init; }

    /// <summary>標案詳情頁 URL。例：https://web.pcc.gov.tw/prkms/urlSelector/common/tpam?pk=...</summary>
    [JsonPropertyName("detailUrl")]
    public required string DetailUrl { get; init; }

    /// <summary>
    /// 命中關鍵字清單，由 IKeywordMatcher 在去重後標註。
    /// </summary>
    [JsonPropertyName("matchedKeywords")]
    public IReadOnlyList<string> MatchedKeywords { get; init; } = Array.Empty<string>();

    /// <summary>該筆首次寫入此日 tenders.json 的時間。</summary>
    [JsonPropertyName("createdAt")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>該筆最近一次被擷取到的時間。同日重抓時更新此值，CreatedAt 不變。</summary>
    [JsonPropertyName("lastSeenAt")]
    public required DateTimeOffset LastSeenAt { get; init; }
}
