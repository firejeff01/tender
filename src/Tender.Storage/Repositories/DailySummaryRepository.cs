using System.Text.Json;
using Tender.Core.Models;
using Tender.Storage.Atomic;
using Tender.Storage.Paths;

namespace Tender.Storage.Repositories;

public interface IDailySummaryRepository
{
    Task<DailySummary?> LoadAsync(DateOnly date, CancellationToken ct = default);

    /// <summary>原子寫入 summary.json。</summary>
    Task SaveAsync(DailySummary summary, CancellationToken ct = default);
}

public sealed class DailySummaryRepository : IDailySummaryRepository
{
    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IDataPaths _paths;
    private readonly IAtomicJsonWriter _writer;

    public DailySummaryRepository(IDataPaths paths, IAtomicJsonWriter writer)
    {
        _paths = paths;
        _writer = writer;
    }

    public async Task<DailySummary?> LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var filePath = _paths.SummaryFile(date);
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            return JsonSerializer.Deserialize<DailySummary>(json, _readOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(DailySummary summary, CancellationToken ct = default)
    {
        var date = DateOnly.ParseExact(summary.Date, "yyyy-MM-dd");
        await _writer.WriteAsync(_paths.SummaryFile(date), summary, ct);
    }
}
