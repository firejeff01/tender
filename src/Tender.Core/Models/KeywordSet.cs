using System.Text.Json.Serialization;

namespace Tender.Core.Models;

public sealed record KeywordSet
{
    [JsonPropertyName("groups")]
    public required IReadOnlyList<KeywordGroup> Groups { get; init; }
}

public sealed record KeywordGroup
{
    /// <summary>分類名稱（資訊系統／XR/AI／資安/無障礙／ESG/碳管理／業務雜項／智慧管考／倉儲自動化／地區／指定機關）。</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("items")]
    public required IReadOnlyList<KeywordItem> Items { get; init; }
}

public sealed record KeywordItem
{
    [JsonPropertyName("keyword")]
    public required string Keyword { get; init; }

    /// <summary>
    /// 命中欄位：tenderName / agencyName / any（兩者皆比對）。
    /// 地區關鍵字採 any（沿用 Excel 巨集行為）。
    /// </summary>
    [JsonPropertyName("targetField")]
    public required string TargetField { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;
}
