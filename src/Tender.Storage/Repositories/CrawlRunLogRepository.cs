using System.Text.Json;
using Tender.Core.Models;
using Tender.Storage.Atomic;
using Tender.Storage.Paths;

namespace Tender.Storage.Repositories;

public interface ICrawlRunLogRepository
{
    Task<CrawlRunLog?> LoadAsync(DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// 在 crawl-runs.json 末尾新增一筆 run 紀錄（若檔案不存在則建立）。
    /// 採讀取-合併-原子寫入流程。
    /// </summary>
    Task AppendRunAsync(DateOnly date, CrawlRun run, CancellationToken ct = default);
}

public sealed class CrawlRunLogRepository : ICrawlRunLogRepository
{
    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IDataPaths _paths;
    private readonly IAtomicJsonWriter _writer;

    public CrawlRunLogRepository(IDataPaths paths, IAtomicJsonWriter writer)
    {
        _paths = paths;
        _writer = writer;
    }

    public async Task<CrawlRunLog?> LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var filePath = _paths.CrawlRunsFile(date);
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            return JsonSerializer.Deserialize<CrawlRunLog>(json, _readOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task AppendRunAsync(DateOnly date, CrawlRun run, CancellationToken ct = default)
    {
        var existing = await LoadAsync(date, ct);
        var runs = existing?.Runs.ToList() ?? new List<CrawlRun>();
        runs.Add(run);

        var log = new CrawlRunLog
        {
            Date = date.ToString("yyyy-MM-dd"),
            Runs = runs.OrderBy(r => r.StartedAt).ToList().AsReadOnly(),
        };

        await _writer.WriteAsync(_paths.CrawlRunsFile(date), log, ct);
    }
}
