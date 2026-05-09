using FluentAssertions;
using Tender.Core.Models;
using Tender.Crawler.Application;
using Tender.Storage.Atomic;
using Tender.Storage.Paths;
using Tender.Storage.Repositories;

namespace Tender.Crawler.Tests;

/// <summary>
/// DailySummaryService 單元測試：驗證 GenerateAsync 正確讀取 tenders.json 並寫入 summary.json。
/// </summary>
public sealed class DailySummaryServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly IDataPaths _dataPaths;
    private readonly AtomicJsonWriter _writer;
    private readonly TenderRepository _tenderRepo;
    private readonly DailySummaryRepository _summaryRepo;
    private readonly DailySummaryService _service;

    private static readonly DateTimeOffset _now =
        new(2026, 5, 8, 17, 5, 0, TimeSpan.FromHours(8));

    public DailySummaryServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"SummaryTests_{Guid.NewGuid():N}");
        _dataPaths = new LocalAppDataPaths(_tempRoot);
        _writer = new AtomicJsonWriter();
        _tenderRepo = new TenderRepository(_dataPaths, _writer);
        _summaryRepo = new DailySummaryRepository(_dataPaths, _writer);
        _service = new DailySummaryService(_tenderRepo, _summaryRepo);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private static CrawlRun MakeRun(RunStatus status, int inserted = 5, int updated = 2, int skipped = 1,
        string? errorMessage = null)
    {
        return new CrawlRun
        {
            RunId = "20260508-170000",
            TargetDate = "2026-05-08",
            TriggerSource = TriggerSource.Scheduled,
            StartedAt = _now.AddMinutes(-5),
            FinishedAt = _now,
            Status = status,
            InsertedCount = inserted,
            UpdatedCount = updated,
            SkippedCount = skipped,
            ErrorMessage = errorMessage,
        };
    }

    private static TenderItem MakeTender(string pk) => new()
    {
        SourcePk = pk,
        AgencyName = "測試機關",
        TenderName = "測試標案",
        TenderMethod = "公開招標",
        AnnouncementDate = "115/05/08",
        DetailUrl = $"https://web.pcc.gov.tw/prkms/urlSelector/common/tpam?pk={pk}",
        CreatedAt = _now,
        LastSeenAt = _now,
    };

    // ---- 正常流程 ----

    [Fact]
    public async Task GenerateAsync_Success_TotalCountFromTendersJson()
    {
        var date = new DateOnly(2026, 5, 8);
        var items = Enumerable.Range(1, 10).Select(i => MakeTender($"PK{i:D3}")).ToList();
        await _tenderRepo.MergeDailySnapshotAsync(date, items, _now);

        var run = MakeRun(RunStatus.Success, inserted: 10, updated: 0, skipped: 0);
        await _service.GenerateAsync(date, run);

        var summary = await _summaryRepo.LoadAsync(date);
        summary.Should().NotBeNull();
        summary!.TotalCount.Should().Be(10, "totalCount 應等於 tenders.json 的 Items.Count");
        summary.LastRunStatus.Should().Be(RunStatus.Success);
        summary.InsertedCount.Should().Be(10);
        summary.UpdatedCount.Should().Be(0);
        summary.SkippedCount.Should().Be(0);
        summary.ErrorMessage.Should().BeNull();
        summary.LastRunAt.Should().Be(_now);
    }

    [Fact]
    public async Task GenerateAsync_Failed_TotalCountZeroWhenNoTendersFile()
    {
        var date = new DateOnly(2026, 5, 8);
        var run = MakeRun(RunStatus.Failed, inserted: 0, errorMessage: "Connection timeout");
        await _service.GenerateAsync(date, run);

        var summary = await _summaryRepo.LoadAsync(date);
        summary!.TotalCount.Should().Be(0);
        summary.LastRunStatus.Should().Be(RunStatus.Failed);
        summary.ErrorMessage.Should().Be("Connection timeout");
    }

    [Fact]
    public async Task GenerateAsync_Summary_ContainsLastRunAt()
    {
        var date = new DateOnly(2026, 5, 8);
        var run = MakeRun(RunStatus.Success);
        await _service.GenerateAsync(date, run);

        var summary = await _summaryRepo.LoadAsync(date);
        summary!.LastRunAt.Should().Be(_now);
    }

    [Fact]
    public async Task GenerateAsync_MergedCounts_PassedFromRun()
    {
        var date = new DateOnly(2026, 5, 8);
        var run = MakeRun(RunStatus.Success, inserted: 5, updated: 2, skipped: 3);
        await _service.GenerateAsync(date, run);

        var summary = await _summaryRepo.LoadAsync(date);
        summary!.InsertedCount.Should().Be(5);
        summary.UpdatedCount.Should().Be(2);
        summary.SkippedCount.Should().Be(3);
    }

    // ---- 對應 crawl_logging.feature: 摘要應呈現當次新增/更新/略過筆數 ----

    [Fact]
    public async Task GenerateAsync_SummaryCountsMatchRunCounts()
    {
        var date = new DateOnly(2026, 5, 8);

        // 預先放入既有 3 筆
        var existing = Enumerable.Range(1, 3).Select(i => MakeTender($"PK{i:D3}")).ToList();
        await _tenderRepo.MergeDailySnapshotAsync(date, existing, _now);

        // 模擬 run 結果
        var run = MakeRun(RunStatus.Success, inserted: 5, updated: 2, skipped: 3);
        await _service.GenerateAsync(date, run);

        var summary = await _summaryRepo.LoadAsync(date);
        // TotalCount 從 tenders.json 實際筆數（3 筆，因為 merge 後還是 3）
        summary!.TotalCount.Should().Be(3);
        // InsertedCount/UpdatedCount/SkippedCount 來自 run
        summary.InsertedCount.Should().Be(5);
        summary.UpdatedCount.Should().Be(2);
        summary.SkippedCount.Should().Be(3);
    }
}
