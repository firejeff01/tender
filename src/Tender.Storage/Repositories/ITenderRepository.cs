using Tender.Core.Models;

namespace Tender.Storage.Repositories;

/// <summary>
/// 當日 tenders.json 讀寫，含合併、去重、原子替換。
/// </summary>
public interface ITenderRepository
{
    /// <summary>讀取指定日的快照，不存在回傳 null，損毀拋 CorruptedDataException。</summary>
    Task<DailyTenderSnapshot?> LoadAsync(DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// 將新爬到的標案合併進該日快照。
    /// - 以 SourcePk 去重。
    /// - 既有 + 新項目分類為 inserted/updated/skipped。
    /// - 寫入採暫存檔加原子替換（先寫 .tmp 再 File.Move）。
    /// </summary>
    Task<MergeResult> MergeDailySnapshotAsync(
        DateOnly date,
        IReadOnlyList<TenderItem> incomingItems,
        DateTimeOffset now,
        CancellationToken ct = default);

    /// <summary>檢查指定日的 tenders.json 是否存在。</summary>
    bool Exists(DateOnly date);
}

public sealed record MergeResult(int InsertedCount, int UpdatedCount, int SkippedCount);
