using System.Text.Json.Serialization;

namespace Tender.Core.Models;

/// <summary>
/// 當日摘要，對應 data/yyyy-MM/yyyy-MM-dd/summary.json。
/// 月份行事曆首頁優先讀取此檔，不解析 tenders.json。
/// </summary>
public sealed record DailySummary
{
    [JsonPropertyName("date")]
    public required string Date { get; init; }

    /// <summary>該日標案總筆數（去重後）。</summary>
    [JsonPropertyName("totalCount")]
    public required int TotalCount { get; init; }

    /// <summary>最近一次該日爬蟲執行狀態。</summary>
    [JsonPropertyName("lastRunStatus")]
    public required RunStatus LastRunStatus { get; init; }

    /// <summary>最近一次該日爬蟲執行完成時間（成功或失敗皆更新）。</summary>
    [JsonPropertyName("lastRunAt")]
    public required DateTimeOffset LastRunAt { get; init; }

    /// <summary>最近一次新增筆數。</summary>
    [JsonPropertyName("insertedCount")]
    public int InsertedCount { get; init; }

    /// <summary>最近一次更新筆數（同 sourcePk 重新出現）。</summary>
    [JsonPropertyName("updatedCount")]
    public int UpdatedCount { get; init; }

    /// <summary>最近一次略過筆數（已存在且無變更）。</summary>
    [JsonPropertyName("skippedCount")]
    public int SkippedCount { get; init; }

    /// <summary>失敗或部分失敗的錯誤摘要，成功且無部分錯誤時為 null。</summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}
