using System.Text.Json;
using Tender.Core.Exceptions;
using Tender.Core.Models;
using Tender.Storage.Atomic;
using Tender.Storage.Paths;

namespace Tender.Storage.Repositories;

public sealed class TenderRepository : ITenderRepository
{
    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IDataPaths _paths;
    private readonly IAtomicJsonWriter _writer;

    public TenderRepository(IDataPaths paths, IAtomicJsonWriter writer)
    {
        _paths = paths;
        _writer = writer;
    }

    public async Task<DailyTenderSnapshot?> LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var filePath = _paths.TendersFile(date);
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            var snapshot = JsonSerializer.Deserialize<DailyTenderSnapshot>(json, _readOptions);
            return snapshot;
        }
        catch (JsonException ex)
        {
            throw new CorruptedDataException(filePath, $"Failed to deserialize tenders.json: {ex.Message}", ex);
        }
    }

    public async Task<MergeResult> MergeDailySnapshotAsync(
        DateOnly date,
        IReadOnlyList<TenderItem> incomingItems,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        // 讀取既有快照
        DailyTenderSnapshot? existing = null;
        try
        {
            existing = await LoadAsync(date, ct);
        }
        catch (CorruptedDataException)
        {
            // 損毀的既有檔案視為不存在，重新建立
        }

        var existingDict = existing?.Items
            .ToDictionary(x => x.SourcePk, StringComparer.Ordinal)
            ?? new Dictionary<string, TenderItem>(StringComparer.Ordinal);

        int insertedCount = 0;
        int updatedCount = 0;
        int skippedCount = 0;

        // Replace 策略：最終快照只包含本次爬蟲結果，不保留舊資料中本次未出現的記錄。
        // 仍比對 existingDict 以保留 CreatedAt 並計算 insert/update/skip 數量。
        var merged = new Dictionary<string, TenderItem>(incomingItems.Count, StringComparer.Ordinal);

        foreach (var incoming in incomingItems)
        {
            if (existingDict.TryGetValue(incoming.SourcePk, out var existingItem))
            {
                if (HasChanges(existingItem, incoming))
                {
                    merged[incoming.SourcePk] = incoming with
                    {
                        CreatedAt = existingItem.CreatedAt,
                        LastSeenAt = now,
                    };
                    updatedCount++;
                }
                else
                {
                    merged[incoming.SourcePk] = existingItem with { LastSeenAt = now };
                    skippedCount++;
                }
            }
            else
            {
                merged[incoming.SourcePk] = incoming with
                {
                    CreatedAt = now,
                    LastSeenAt = now,
                };
                insertedCount++;
            }
        }

        var snapshot = new DailyTenderSnapshot
        {
            Date = date.ToString("yyyy-MM-dd"),
            GeneratedAt = now,
            Source = "https://web.pcc.gov.tw/pis/",
            Items = merged.Values.ToList().AsReadOnly(),
        };

        await _writer.WriteAsync(_paths.TendersFile(date), snapshot, ct);

        return new MergeResult(insertedCount, updatedCount, skippedCount);
    }

    public bool Exists(DateOnly date)
        => File.Exists(_paths.TendersFile(date));

    private static bool HasChanges(TenderItem existing, TenderItem incoming)
    {
        return existing.TenderName != incoming.TenderName ||
               existing.AgencyName != incoming.AgencyName ||
               existing.TenderMethod != incoming.TenderMethod ||
               existing.ProcurementType != incoming.ProcurementType ||
               existing.AnnouncementDate != incoming.AnnouncementDate ||
               existing.BidDeadline != incoming.BidDeadline ||
               existing.BudgetAmount != incoming.BudgetAmount ||
               existing.DetailUrl != incoming.DetailUrl;
    }
}
