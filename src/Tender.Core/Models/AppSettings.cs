using System.Text.Json.Serialization;

namespace Tender.Core.Models;

public sealed record AppSettings
{
    /// <summary>排程執行時間，格式 HH:mm。預設 17:00。</summary>
    [JsonPropertyName("scheduledTime")]
    public string ScheduledTime { get; init; } = "17:00";

    /// <summary>是否啟用開機補跑。預設 true。</summary>
    [JsonPropertyName("catchupEnabled")]
    public bool CatchupEnabled { get; init; } = true;

    /// <summary>爬蟲引擎：httpclient（首版預設）或 playwright（PoC 失敗時切換）。</summary>
    [JsonPropertyName("crawlerEngine")]
    public string CrawlerEngine { get; init; } = "httpclient";

    /// <summary>爬蟲分頁間隔毫秒數，遵守禮貌爬取。</summary>
    [JsonPropertyName("requestDelayMs")]
    public int RequestDelayMs { get; init; } = 1500;

    /// <summary>最大重試次數。</summary>
    [JsonPropertyName("maxRetries")]
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// 預設要擷取的招標方式（業務名稱）。實際送網站的 option/value 由 TenderMethodMapping 決定。
    /// </summary>
    [JsonPropertyName("targetTenderMethods")]
    public IReadOnlyList<string> TargetTenderMethods { get; init; } = new[]
    {
        "公開招標",
        "公開取得電子報價單",
        "經公開評選或公開徵求之限制性招標"
    };
}
