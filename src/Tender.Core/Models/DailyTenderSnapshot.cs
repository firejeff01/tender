using System.Text.Json.Serialization;

namespace Tender.Core.Models;

/// <summary>
/// 當日標案快照，對應 data/yyyy-MM/yyyy-MM-dd/tenders.json。
/// </summary>
public sealed record DailyTenderSnapshot
{
    /// <summary>該快照的資料所屬日期（西元年），例：「2026-05-08」。</summary>
    [JsonPropertyName("date")]
    public required string Date { get; init; }

    /// <summary>本檔案產生時間。</summary>
    [JsonPropertyName("generatedAt")]
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>資料來源 URL（首版固定為政府電子採購網首頁）。</summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>該日標案清單，以 SourcePk 為唯一鍵。</summary>
    [JsonPropertyName("items")]
    public required IReadOnlyList<TenderItem> Items { get; init; }
}
