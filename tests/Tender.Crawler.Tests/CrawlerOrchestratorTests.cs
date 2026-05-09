using FluentAssertions;
using Moq;
using Tender.Core.Clock;
using Tender.Core.DateConversion;
using Tender.Core.Exceptions;
using Tender.Core.Keywords;
using Tender.Core.Models;
using Tender.Crawler.Application;
using Tender.Crawler.Parsing;
using Tender.Crawler.Reporting;
using Tender.Crawler.Spider;
using Tender.Storage.Atomic;
using Tender.Storage.Paths;
using Tender.Storage.Repositories;

namespace Tender.Crawler.Tests;

/// <summary>
/// CrawlerOrchestrator 整合測試。
/// 使用 Mock ICrawler/ITenderParser/IClock，Real Storage（temp dir）。
/// 對應 daily_crawl.feature 與 crawl_logging.feature 的核心 Scenario。
/// </summary>
public sealed class CrawlerOrchestratorTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly IDataPaths _dataPaths;
    private readonly AtomicJsonWriter _writer;
    private readonly TenderRepository _tenderRepo;
    private readonly DailySummaryRepository _summaryRepo;
    private readonly CrawlRunLogRepository _crawlRunLogRepo;
    private readonly ErrorLogWriter _errorLogWriter;
    private readonly KeywordsRepository _keywordsRepo;

    private readonly Mock<ICrawler> _crawlerMock = new();
    private readonly Mock<ITenderParser> _parserMock = new();
    private readonly Mock<IClock> _clockMock = new();
    private readonly Mock<IProgressReporter> _progressMock = new();

    private readonly IKeywordMatcher _keywordMatcher = new KeywordMatcher();
    private readonly ITaiwanDateConverter _dateConverter = new TaiwanDateConverter();

    public CrawlerOrchestratorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"TenderTests_{Guid.NewGuid():N}");
        _dataPaths = new LocalAppDataPaths(_tempRoot);
        _writer = new AtomicJsonWriter();
        _tenderRepo = new TenderRepository(_dataPaths, _writer);
        _summaryRepo = new DailySummaryRepository(_dataPaths, _writer);
        _crawlRunLogRepo = new CrawlRunLogRepository(_dataPaths, _writer);
        _errorLogWriter = new ErrorLogWriter(_dataPaths);
        _keywordsRepo = new KeywordsRepository(_dataPaths, _writer);

        // 預設時鐘
        _clockMock.Setup(c => c.Now)
            .Returns(new DateTimeOffset(2026, 5, 8, 17, 0, 0, TimeSpan.FromHours(8)));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); }
        catch { /* ignore */ }
    }

    private CrawlerOrchestrator CreateOrchestrator()
    {
        var summaryService = new DailySummaryService(_tenderRepo, _summaryRepo);
        return new CrawlerOrchestrator(
            _crawlerMock.Object,
            _parserMock.Object,
            _keywordMatcher,
            _tenderRepo,
            _crawlRunLogRepo,
            summaryService,
            _errorLogWriter,
            _dataPaths,
            _clockMock.Object,
            _progressMock.Object,
            _keywordsRepo,
            _dateConverter);
    }

    // 建立 N 筆測試標案；announcementDate 採 targetDate 對應的民國格式（如 2026/5/8 → "115/05/08"）。
    private List<TenderItem> MakeTenders(int count, string pkPrefix = "PK", DateOnly? targetDate = null)
    {
        var date = targetDate ?? new DateOnly(2026, 5, 8);
        var rocDate = _dateConverter.DateOnlyToRoc(date);
        var now = new DateTimeOffset(2026, 5, 8, 17, 0, 0, TimeSpan.FromHours(8));
        return Enumerable.Range(1, count).Select(i => new TenderItem
        {
            SourcePk = $"{pkPrefix}{i:D3}",
            AgencyName = $"機關 {i}",
            TenderName = $"標案 {i}",
            TenderMethod = "公開招標",
            AnnouncementDate = rocDate,
            DetailUrl = $"https://web.pcc.gov.tw/prkms/urlSelector/common/tpam?pk={pkPrefix}{i:D3}",
            CreatedAt = now,
            LastSeenAt = now,
        }).ToList();
    }

    private void SetupCrawlerReturns(IReadOnlyList<TenderItem> items, DateOnly? targetDate = null)
    {
        var pageHtml = "<html><body><table id=\"tpam\"><tbody></tbody></table></body></html>";
        var pages = new List<FetchedPage> { new(1, "https://web.pcc.gov.tw", pageHtml, null) };

        if (targetDate.HasValue)
            _crawlerMock.Setup(c => c.FetchAsync(targetDate.Value, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pages);
        else
            _crawlerMock.Setup(c => c.FetchAsync(It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pages);

        _parserMock.Setup(p => p.Parse(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(items.ToList().AsReadOnly());
    }

    // =====================================================================
    // Scenario: 每日 17:00 排程準時執行並成功擷取當天標案
    // =====================================================================
    [Fact]
    public async Task RunAsync_Scheduled_Success_CreatesAllFiles()
    {
        var targetDate = new DateOnly(2026, 5, 8);
        var tenders = MakeTenders(5);
        SetupCrawlerReturns(tenders, targetDate);

        var orchestrator = CreateOrchestrator();
        var run = await orchestrator.RunAsync(new CrawlerArgs
        {
            Mode = CrawlerMode.Scheduled,
            TargetDate = targetDate,
        });

        // 驗證 run 結果
        run.Status.Should().Be(RunStatus.Success);
        run.TriggerSource.Should().Be(TriggerSource.Scheduled);
        run.InsertedCount.Should().Be(5);

        // 驗證 tenders.json 存在
        _tenderRepo.Exists(targetDate).Should().BeTrue();
        var snapshot = await _tenderRepo.LoadAsync(targetDate);
        snapshot.Should().NotBeNull();
        snapshot!.Items.Should().HaveCount(5);
        snapshot.Date.Should().Be("2026-05-08");

        // 驗證 summary.json
        var summary = await _summaryRepo.LoadAsync(targetDate);
        summary.Should().NotBeNull();
        summary!.LastRunStatus.Should().Be(RunStatus.Success);
        summary.TotalCount.Should().Be(5);

        // 驗證 crawl-runs.json
        var log = await _crawlRunLogRepo.LoadAsync(targetDate);
        log.Should().NotBeNull();
        log!.Runs.Should().HaveCount(1);
        log.Runs[0].TriggerSource.Should().Be(TriggerSource.Scheduled);
        log.Runs[0].Status.Should().Be(RunStatus.Success);
    }

    // =====================================================================
    // Scenario: 使用者按下「立即更新」按鈕成功擷取當天標案
    // =====================================================================
    [Fact]
    public async Task RunAsync_Manual_LastRunAt_EqualsClockNow()
    {
        var targetDate = new DateOnly(2026, 5, 8);
        var expectedNow = new DateTimeOffset(2026, 5, 8, 10, 30, 0, TimeSpan.FromHours(8));
        _clockMock.Setup(c => c.Now).Returns(expectedNow);

        SetupCrawlerReturns(MakeTenders(3), targetDate);

        var orchestrator = CreateOrchestrator();
        await orchestrator.RunAsync(new CrawlerArgs { Mode = CrawlerMode.Manual, TargetDate = targetDate });

        var summary = await _summaryRepo.LoadAsync(targetDate);
        summary!.LastRunAt.Should().Be(expectedNow);

        var log = await _crawlRunLogRepo.LoadAsync(targetDate);
        log!.Runs.Last().TriggerSource.Should().Be(TriggerSource.Manual);
    }

    // =====================================================================
    // Scenario: 使用者對過去日期執行「重新抓取此日」
    // =====================================================================
    [Fact]
    public async Task RunAsync_ManualRedo_UsesTargetDateFolder()
    {
        var targetDate = new DateOnly(2026, 5, 5);
        var today = new DateOnly(2026, 5, 8);

        // 預先建立既有資料
        var existing = MakeTenders(1, "EXISTING-", targetDate);
        existing[0] = existing[0] with { SourcePk = "EXISTING-001" };
        await _tenderRepo.MergeDailySnapshotAsync(targetDate, existing, _clockMock.Object.Now);

        // Mock 回傳 4 筆（其中 1 筆是既有）
        var incoming = MakeTenders(4, "NEW-", targetDate);
        incoming[0] = incoming[0] with { SourcePk = "EXISTING-001" };

        SetupCrawlerReturns(incoming, targetDate);

        var orchestrator = CreateOrchestrator();
        var run = await orchestrator.RunAsync(new CrawlerArgs
        {
            Mode = CrawlerMode.ManualRedo,
            TargetDate = targetDate,
        });

        run.TriggerSource.Should().Be(TriggerSource.ManualRedo);

        // 寫入路徑應為 targetDate，非 today
        _tenderRepo.Exists(targetDate).Should().BeTrue();
        _tenderRepo.Exists(today).Should().BeFalse();

        // sourcePk "EXISTING-001" 只出現一次
        var snapshot = await _tenderRepo.LoadAsync(targetDate);
        snapshot!.Items.Where(i => i.SourcePk == "EXISTING-001").Should().HaveCount(1);

        var log = await _crawlRunLogRepo.LoadAsync(targetDate);
        log!.Runs.Last().TriggerSource.Should().Be(TriggerSource.ManualRedo);
    }

    // =====================================================================
    // Scenario: 同一天內重複執行爬蟲不可產生重複標案
    // =====================================================================
    [Fact]
    public async Task RunAsync_Dedup_MergesCorrectly()
    {
        var targetDate = new DateOnly(2026, 5, 8);
        var now = _clockMock.Object.Now;

        // 建立既有 50 筆（PK001~PK050）
        var existing = MakeTenders(50, "PK");
        await _tenderRepo.MergeDailySnapshotAsync(targetDate, existing, now);

        // 新回傳 7 筆（2 更新 + 3 全新 + 2 無變更）
        var incoming = new List<TenderItem>
        {
            existing[0] with { TenderName = "更新後標案1", CreatedAt = now, LastSeenAt = now },  // PK001 更新
            existing[1] with { TenderName = "更新後標案2", CreatedAt = now, LastSeenAt = now },  // PK002 更新
            MakeTenders(1, "PK")[0] with { SourcePk = "PK051" },  // 新
            MakeTenders(1, "PK")[0] with { SourcePk = "PK052" },  // 新
            MakeTenders(1, "PK")[0] with { SourcePk = "PK053" },  // 新
            existing[47],  // PK048 無變更
            existing[48],  // PK049 無變更
        };

        SetupCrawlerReturns(incoming, targetDate);

        var orchestrator = CreateOrchestrator();
        var run = await orchestrator.RunAsync(new CrawlerArgs { Mode = CrawlerMode.Manual, TargetDate = targetDate });

        // 總數 = 50 + 3 新 = 53
        var snapshot = await _tenderRepo.LoadAsync(targetDate);
        snapshot!.Items.Should().HaveCount(53);

        // 每個 SourcePk 唯一
        snapshot.Items.Select(i => i.SourcePk).Distinct().Should().HaveCount(53);

        // Run 紀錄
        run.InsertedCount.Should().Be(3);
        run.UpdatedCount.Should().Be(2);
        run.SkippedCount.Should().Be(2);

        var log = await _crawlRunLogRepo.LoadAsync(targetDate);
        var lastRun = log!.Runs.Last();
        lastRun.InsertedCount.Should().Be(3);
        lastRun.UpdatedCount.Should().Be(2);
        lastRun.SkippedCount.Should().Be(2);
    }

    // =====================================================================
    // Scenario: 跨日資料分開保存
    // =====================================================================
    [Fact]
    public async Task RunAsync_CrossDay_DataSavedSeparately()
    {
        var date1 = new DateOnly(2026, 5, 7);
        var date2 = new DateOnly(2026, 5, 8);

        // 各自的 targetDate 對應 announcementDate（避免被 Orchestrator 過濾掉）
        var tenderForDate1 = MakeTenders(1, "ABC", date1);
        tenderForDate1[0] = tenderForDate1[0] with { SourcePk = "ABC123" };
        var tenderForDate2 = MakeTenders(1, "ABC", date2);
        tenderForDate2[0] = tenderForDate2[0] with { SourcePk = "ABC123" };

        // 第一天
        SetupCrawlerReturns(tenderForDate1, date1);
        var orchestrator = CreateOrchestrator();
        await orchestrator.RunAsync(new CrawlerArgs { Mode = CrawlerMode.Scheduled, TargetDate = date1 });

        // 第二天
        SetupCrawlerReturns(tenderForDate2, date2);
        await orchestrator.RunAsync(new CrawlerArgs { Mode = CrawlerMode.Scheduled, TargetDate = date2 });

        var snap1 = await _tenderRepo.LoadAsync(date1);
        var snap2 = await _tenderRepo.LoadAsync(date2);

        snap1!.Items.Should().Contain(i => i.SourcePk == "ABC123");
        snap2!.Items.Should().Contain(i => i.SourcePk == "ABC123");
        snap1.Items.Should().HaveCount(1);
        snap2.Items.Should().HaveCount(1);
    }

    // =====================================================================
    // Scenario: 政府電子採購網無法連線
    // =====================================================================
    [Fact]
    public async Task RunAsync_NetworkException_WriteFailedStatus()
    {
        var targetDate = new DateOnly(2026, 5, 8);

        _crawlerMock.Setup(c => c.FetchAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CrawlNetworkException("Connection timeout"));

        var orchestrator = CreateOrchestrator();
        var run = await orchestrator.RunAsync(new CrawlerArgs
        {
            Mode = CrawlerMode.Scheduled,
            TargetDate = targetDate,
        });

        run.Status.Should().Be(RunStatus.Failed);
        run.ErrorMessage.Should().Contain("Connection timeout");

        var summary = await _summaryRepo.LoadAsync(targetDate);
        summary!.LastRunStatus.Should().Be(RunStatus.Failed);
        summary.ErrorMessage.Should().Contain("Connection timeout");

        var log = await _crawlRunLogRepo.LoadAsync(targetDate);
        log!.Runs.Last().Status.Should().Be(RunStatus.Failed);

        // errors.log 應存在
        var errorsPath = _dataPaths.ErrorsLogFile(targetDate);
        File.Exists(errorsPath).Should().BeTrue();
        var errorsContent = await File.ReadAllTextAsync(errorsPath);
        errorsContent.Should().Contain("error");
        errorsContent.Should().Contain("crawler");
    }

    // =====================================================================
    // Scenario: 單頁解析失敗，整批任務不中斷
    // =====================================================================
    [Fact]
    public async Task RunAsync_SinglePageParseFails_OtherPagesSucceed()
    {
        var targetDate = new DateOnly(2026, 5, 8);
        var goodHtml = "<html><body><table id=\"tpam\"><tbody><tr><td>1</td></tr></tbody></table></body></html>";
        var badHtml = "<html><body><p>bad page</p></body></html>";

        // 5 頁：第 3 頁觸發 ParseException
        var pages = new List<FetchedPage>
        {
            new(1, "url", goodHtml, null),
            new(2, "url", goodHtml, null),
            new(3, "url", badHtml, null),
            new(4, "url", goodHtml, null),
            new(5, "url", goodHtml, null),
        };

        _crawlerMock.Setup(c => c.FetchAsync(targetDate, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pages);

        int callCount = 0;
        _parserMock.Setup(p => p.Parse(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns<string, DateTimeOffset>((html, now) =>
            {
                callCount++;
                if (html == badHtml)
                    throw new ParseException(3, "Test parse error on page 3");
                // 每頁回傳 4 筆
                return MakeTenders(4, $"P{callCount}").AsReadOnly();
            });

        var orchestrator = CreateOrchestrator();
        var run = await orchestrator.RunAsync(new CrawlerArgs
        {
            Mode = CrawlerMode.Scheduled,
            TargetDate = targetDate,
        });

        // 4 頁 × 4 筆 = 16 筆
        var snapshot = await _tenderRepo.LoadAsync(targetDate);
        snapshot!.Items.Should().HaveCount(16);

        run.Status.Should().Be(RunStatus.Success);
        run.ErrorMessage.Should().Contain("部分頁面解析失敗");

        // errors.log 含第 3 頁的 warning
        var errorsPath = _dataPaths.ErrorsLogFile(targetDate);
        var errorsContent = await File.ReadAllTextAsync(errorsPath);
        errorsContent.Should().Contain("warning");
        errorsContent.Should().Contain("3");

        var summary = await _summaryRepo.LoadAsync(targetDate);
        summary!.LastRunStatus.Should().Be(RunStatus.Success);
        summary.ErrorMessage.Should().Contain("部分頁面解析失敗");
    }

    // =====================================================================
    // Scenario: 同日多次執行皆累積於 crawl-runs.json
    // =====================================================================
    [Fact]
    public async Task RunAsync_MultipleRunsSameDay_AccumulatesInCrawlRunLog()
    {
        var targetDate = new DateOnly(2026, 5, 8);
        SetupCrawlerReturns(MakeTenders(5), targetDate);

        var orchestrator = CreateOrchestrator();

        // 第一次（Scheduled）
        var clockSeq = 0;
        _clockMock.Setup(c => c.Now).Returns(() =>
            clockSeq++ == 0
                ? new DateTimeOffset(2026, 5, 8, 17, 0, 0, TimeSpan.FromHours(8))
                : new DateTimeOffset(2026, 5, 8, 17, 1, 0, TimeSpan.FromHours(8)));

        await orchestrator.RunAsync(new CrawlerArgs { Mode = CrawlerMode.Scheduled, TargetDate = targetDate });

        // 第二次（Manual）
        _clockMock.Setup(c => c.Now).Returns(new DateTimeOffset(2026, 5, 8, 18, 30, 0, TimeSpan.FromHours(8)));
        await orchestrator.RunAsync(new CrawlerArgs { Mode = CrawlerMode.Manual, TargetDate = targetDate });

        var log = await _crawlRunLogRepo.LoadAsync(targetDate);
        log!.Runs.Should().HaveCount(2);
        log.Runs[0].RunId.Should().NotBe(log.Runs[1].RunId);
        log.Runs[1].TriggerSource.Should().Be(TriggerSource.Manual);
    }

    // =====================================================================
    // Scenario: 同日資料夾尚未存在時，第一次寫入需自動建立
    // =====================================================================
    [Fact]
    public async Task RunAsync_DailyFolderNotExist_AutoCreatesFolder()
    {
        var targetDate = new DateOnly(2026, 5, 8);
        SetupCrawlerReturns(MakeTenders(3), targetDate);

        var dailyFolder = _dataPaths.DailyFolder(targetDate);
        Directory.Exists(dailyFolder).Should().BeFalse("pre-condition: folder should not exist");

        var orchestrator = CreateOrchestrator();
        await orchestrator.RunAsync(new CrawlerArgs { Mode = CrawlerMode.Scheduled, TargetDate = targetDate });

        // 確認月份資料夾層級正確（DailyFolder 需要完整日期）
        // 月份資料夾應存在
        var monthFolder = Path.Combine(_tempRoot, "2026-05");
        Directory.Exists(monthFolder).Should().BeTrue();
        Directory.Exists(dailyFolder).Should().BeTrue();

        File.Exists(_dataPaths.TendersFile(targetDate)).Should().BeTrue();
        File.Exists(_dataPaths.SummaryFile(targetDate)).Should().BeTrue();
        File.Exists(_dataPaths.CrawlRunsFile(targetDate)).Should().BeTrue();
    }

    // =====================================================================
    // Scenario: 磁碟寫入失敗（IOException）
    // =====================================================================
    [Fact]
    public async Task RunAsync_IoExceptionOnMerge_WriteFailedRunAndErrorLog()
    {
        var targetDate = new DateOnly(2026, 5, 8);
        var pageHtml = "<html><body><table id=\"tpam\"><tbody></tbody></table></body></html>";
        var pages = new List<FetchedPage> { new(1, "url", pageHtml, null) };

        _crawlerMock.Setup(c => c.FetchAsync(It.IsAny<DateOnly>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pages);
        _parserMock.Setup(p => p.Parse(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(MakeTenders(5).AsReadOnly());

        // FaultyAtomicJsonWriter：寫 tenders.json 時拋 IOException
        var faultyWriter = new FaultyAtomicJsonWriter(throwOnFirst: true);
        var faultyTenderRepo = new TenderRepository(_dataPaths, faultyWriter);

        var summaryService = new DailySummaryService(faultyTenderRepo, _summaryRepo);
        var orchestrator = new CrawlerOrchestrator(
            _crawlerMock.Object,
            _parserMock.Object,
            _keywordMatcher,
            faultyTenderRepo,
            _crawlRunLogRepo,
            summaryService,
            _errorLogWriter,
            _dataPaths,
            _clockMock.Object,
            _progressMock.Object,
            _keywordsRepo,
            _dateConverter);

        var run = await orchestrator.RunAsync(new CrawlerArgs { Mode = CrawlerMode.Scheduled, TargetDate = targetDate });

        run.Status.Should().Be(RunStatus.Failed);

        var log = await _crawlRunLogRepo.LoadAsync(targetDate);
        log!.Runs.Last().Status.Should().Be(RunStatus.Failed);

        var errorsPath = _dataPaths.ErrorsLogFile(targetDate);
        File.Exists(errorsPath).Should().BeTrue();
        var errorsContent = await File.ReadAllTextAsync(errorsPath);
        errorsContent.Should().Contain("error");
    }

    // =====================================================================
    // Scenario: errors.log 跨日不互相覆蓋
    // =====================================================================
    [Fact]
    public async Task ErrorLog_CrossDay_DoesNotOverwrite()
    {
        var date1 = new DateOnly(2026, 5, 7);
        var date2 = new DateOnly(2026, 5, 8);

        // 預先在 date1 建立 errors.log
        await _errorLogWriter.AppendAsync(date1, new ErrorLogEntry(
            _clockMock.Object.Now, "warning", "crawler", "run1", "前日錯誤行 A", null, null));
        await _errorLogWriter.AppendAsync(date1, new ErrorLogEntry(
            _clockMock.Object.Now, "warning", "crawler", "run1", "前日錯誤行 B", null, null));

        var originalContent = await File.ReadAllTextAsync(_dataPaths.ErrorsLogFile(date1));

        // 對 date2 寫入
        await _errorLogWriter.AppendAsync(date2, new ErrorLogEntry(
            _clockMock.Object.Now, "error", "crawler", "run2", "新日錯誤", null, null));

        // date1 的 errors.log 不應有改變
        var afterContent = await File.ReadAllTextAsync(_dataPaths.ErrorsLogFile(date1));
        afterContent.Should().Be(originalContent);

        // date2 的 errors.log 應存在
        File.Exists(_dataPaths.ErrorsLogFile(date2)).Should().BeTrue();
    }

    // =====================================================================
    // Scenario: 同一標案在連續兩天都出現，分別保留
    // =====================================================================
    [Fact]
    public async Task RunAsync_SamePkAcrossDays_SavedSeparately()
    {
        var date1 = new DateOnly(2026, 5, 7);
        var date2 = new DateOnly(2026, 5, 8);

        var tenderForDate1 = MakeTenders(1, "ABC", date1);
        tenderForDate1[0] = tenderForDate1[0] with { SourcePk = "ABC123" };
        var tenderForDate2 = MakeTenders(1, "ABC", date2);
        tenderForDate2[0] = tenderForDate2[0] with { SourcePk = "ABC123" };

        SetupCrawlerReturns(tenderForDate1, date1);
        var orchestrator = CreateOrchestrator();
        await orchestrator.RunAsync(new CrawlerArgs { Mode = CrawlerMode.Scheduled, TargetDate = date1 });

        SetupCrawlerReturns(tenderForDate2, date2);
        await orchestrator.RunAsync(new CrawlerArgs { Mode = CrawlerMode.Scheduled, TargetDate = date2 });

        var snap1 = await _tenderRepo.LoadAsync(date1);
        var snap2 = await _tenderRepo.LoadAsync(date2);

        snap1!.Items.Should().Contain(i => i.SourcePk == "ABC123");
        snap2!.Items.Should().Contain(i => i.SourcePk == "ABC123");
    }
}

// =====================================================================
// FaultyAtomicJsonWriter：模擬磁碟故障的 IAtomicJsonWriter
// =====================================================================
internal sealed class FaultyAtomicJsonWriter : IAtomicJsonWriter
{
    private readonly bool _throwOnFirst;
    private int _callCount;

    public FaultyAtomicJsonWriter(bool throwOnFirst = true)
    {
        _throwOnFirst = throwOnFirst;
    }

    public Task WriteAsync<T>(string finalPath, T data, CancellationToken ct = default)
    {
        _callCount++;
        if (_throwOnFirst)
            throw new IOException("No space left on device (simulated)");
        return Task.CompletedTask;
    }
}
