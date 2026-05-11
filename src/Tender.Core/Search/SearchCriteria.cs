namespace Tender.Core.Search;

/// <summary>
/// 搜尋條件值物件，由 Desktop 的 ViewModel 組裝後傳入 SearchService。
/// 多項條件之間採 AND 邏輯。
/// </summary>
public sealed record SearchCriteria
{
    /// <summary>使用者搜尋框輸入字串，以空白分割為多個關鍵字（AND 命中、模糊查詢）。</summary>
    public string? KeywordQuery { get; init; }

    /// <summary>
    /// 若為 true，KeywordQuery 只比對 TenderName（標題）；
    /// 否則同時比對 TenderName 與 AgencyName（預設行為）。
    /// </summary>
    public bool KeywordTitleOnly { get; init; }

    /// <summary>
    /// 反向搜尋；僅在 KeywordTitleOnly = true 時有效。
    /// 啟用後保留「TenderName 不包含任一 token」的標案；多 token 任一命中即排除。
    /// </summary>
    public bool KeywordExclude { get; init; }

    /// <summary>關鍵字按鈕命中項（沿用 Excel 既有，多個按鈕之間採 OR 命中）。</summary>
    public IReadOnlyList<string> ActiveKeywordButtons { get; init; } = Array.Empty<string>();

    /// <summary>招標方式（業務名稱），null 為不篩選。</summary>
    public string? TenderMethod { get; init; }

    /// <summary>採購性質，null 為不篩選。</summary>
    public string? ProcurementType { get; init; }

    /// <summary>公告日期區間（含端點），格式為民國年「115/05/06」。</summary>
    public string? AnnouncementDateFrom { get; init; }
    public string? AnnouncementDateTo { get; init; }

    /// <summary>截止投標日期區間（含端點），格式為民國年。</summary>
    public string? BidDeadlineFrom { get; init; }
    public string? BidDeadlineTo { get; init; }

    /// <summary>預算金額區間（含端點），單位為元。</summary>
    public long? BudgetMin { get; init; }
    public long? BudgetMax { get; init; }

    /// <summary>是否只看尚未截止（bidDeadline >= today）。</summary>
    public bool ShowActiveOnly { get; init; }
}

public enum SortKey
{
    None,
    AgencyName,
    TenderName,
    AnnouncementDate,
    BidDeadline,
    BudgetAmount,
}

public enum SortDirection { Ascending, Descending }
