using System.Text.Json.Serialization;

namespace Tender.Core.Models;

/// <summary>
/// 單次爬蟲執行紀錄。
/// </summary>
public sealed record CrawlRun
{
    /// <summary>執行 ID，建議格式 yyyyMMdd-HHmmss（例：「20260508-170000」）。</summary>
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    /// <summary>本次目標查詢日期（公告日期條件）。</summary>
    [JsonPropertyName("targetDate")]
    public required string TargetDate { get; init; }

    /// <summary>觸發來源：scheduled/catchup/manual/manual-redo。</summary>
    [JsonPropertyName("triggerSource")]
    public required TriggerSource TriggerSource { get; init; }

    [JsonPropertyName("startedAt")]
    public required DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("finishedAt")]
    public required DateTimeOffset FinishedAt { get; init; }

    [JsonPropertyName("status")]
    public required RunStatus Status { get; init; }

    [JsonPropertyName("insertedCount")]
    public int InsertedCount { get; init; }

    [JsonPropertyName("updatedCount")]
    public int UpdatedCount { get; init; }

    [JsonPropertyName("skippedCount")]
    public int SkippedCount { get; init; }

    /// <summary>錯誤摘要（失敗時必填，部分失敗時填提示，成功時為 null）。</summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 該日所有執行紀錄的集合，對應 crawl-runs.json。
/// </summary>
public sealed record CrawlRunLog
{
    [JsonPropertyName("date")]
    public required string Date { get; init; }

    /// <summary>該日所有 run 紀錄，依 StartedAt 升冪排列。</summary>
    [JsonPropertyName("runs")]
    public required IReadOnlyList<CrawlRun> Runs { get; init; }
}
