using Tender.Core.Clock;
using Tender.Core.DateConversion;
using Tender.Core.Exceptions;
using Tender.Core.Keywords;
using Tender.Core.Models;
using Tender.Crawler.Parsing;
using Tender.Crawler.Reporting;
using Tender.Crawler.Spider;
using Tender.Storage.Paths;
using Tender.Storage.Repositories;

namespace Tender.Crawler.Application;

/// <summary>
/// 協調完整爬蟲流程：
///   取得 crawler.lock → FetchAsync → Parse → AnnotateKeywords
///   → MergeDailySnapshotAsync → GenerateSummary → AppendRun → [錯誤] AppendErrorLog
/// </summary>
public sealed class CrawlerOrchestrator : ICrawlerOrchestrator
{
    private readonly ICrawler _crawler;
    private readonly ITenderParser _parser;
    private readonly IKeywordMatcher _keywordMatcher;
    private readonly ITenderRepository _tenderRepo;
    private readonly ICrawlRunLogRepository _crawlRunLogRepo;
    private readonly IDailySummaryService _summaryService;
    private readonly IErrorLogWriter _errorLogWriter;
    private readonly IDataPaths _dataPaths;
    private readonly IClock _clock;
    private readonly IProgressReporter _progress;
    private readonly IKeywordsRepository _keywordsRepo;
    private readonly ITaiwanDateConverter _dateConverter;

    // AppSettings 預設招標方式（若未提供則使用此預設）
    private static readonly IReadOnlyList<string> DefaultTenderMethods = new[]
    {
        "公開招標",
        "公開取得電子報價單",
        "經公開評選或公開徵求之限制性招標",
    };

    public CrawlerOrchestrator(
        ICrawler crawler,
        ITenderParser parser,
        IKeywordMatcher keywordMatcher,
        ITenderRepository tenderRepo,
        ICrawlRunLogRepository crawlRunLogRepo,
        IDailySummaryService summaryService,
        IErrorLogWriter errorLogWriter,
        IDataPaths dataPaths,
        IClock clock,
        IProgressReporter progress,
        IKeywordsRepository keywordsRepo,
        ITaiwanDateConverter dateConverter)
    {
        _crawler = crawler;
        _parser = parser;
        _keywordMatcher = keywordMatcher;
        _tenderRepo = tenderRepo;
        _crawlRunLogRepo = crawlRunLogRepo;
        _summaryService = summaryService;
        _errorLogWriter = errorLogWriter;
        _dataPaths = dataPaths;
        _clock = clock;
        _progress = progress;
        _keywordsRepo = keywordsRepo;
        _dateConverter = dateConverter;
    }

    public async Task<CrawlRun> RunAsync(CrawlerArgs args, CancellationToken ct = default)
    {
        var startedAt = _clock.Now;
        var runId = startedAt.ToString("yyyyMMdd-HHmmss");
        var targetDate = args.TargetDate;
        var triggerSource = MapTriggerSource(args.Mode);

        _progress.Report(new ProgressEvent("start", $"Starting crawl for {targetDate:yyyy-MM-dd} [{triggerSource}]", null, 0));

        // 取得 crawler.lock
        await using var lockHandle = await TryAcquireLockAsync(ct);
        if (lockHandle == null)
        {
            var skippedRun = new CrawlRun
            {
                RunId = runId,
                TargetDate = targetDate.ToString("yyyy-MM-dd"),
                TriggerSource = triggerSource,
                StartedAt = startedAt,
                FinishedAt = _clock.Now,
                Status = RunStatus.Skipped,
                ErrorMessage = "another run in progress",
            };
            await SafeAppendRunAsync(targetDate, skippedRun, ct);
            _progress.Report(new ProgressEvent("skipped", "Another run is already in progress.", null, 100));
            return skippedRun;
        }

        // 讀取關鍵字設定
        KeywordSet keywordSet;
        try
        {
            keywordSet = await _keywordsRepo.LoadAsync(ct);
        }
        catch
        {
            keywordSet = new KeywordSet { Groups = Array.Empty<Core.Models.KeywordGroup>() };
        }

        var tenderMethods = DefaultTenderMethods;

        try
        {
            // ---- Step 1: Fetch ----
            _progress.Report(new ProgressEvent("fetching", $"Fetching tender data for {targetDate:yyyy-MM-dd}...", null, 10));

            IReadOnlyList<FetchedPage> pages;
            try
            {
                pages = await _crawler.FetchAsync(targetDate, tenderMethods, ct);
            }
            catch (CrawlNetworkException ex)
            {
                return await HandleNetworkFailureAsync(
                    runId, targetDate, triggerSource, startedAt, ex, ct);
            }

            _progress.Report(new ProgressEvent("fetching", $"Fetched {pages.Count} page(s).", null, 30));

            // ---- Step 2: Parse ----
            _progress.Report(new ProgressEvent("parsing", "Parsing fetched pages...", null, 40));

            var allItems = new List<TenderItem>();
            var parseErrors = new List<(int page, string msg)>();

            for (int i = 0; i < pages.Count; i++)
            {
                var fetchedPage = pages[i];

                if (fetchedPage.Error != null)
                {
                    // Fetch 階段就失敗的頁面（如 5xx）
                    parseErrors.Add((fetchedPage.PageNumber, fetchedPage.Error.Message));
                    await SafeAppendErrorLogAsync(targetDate, runId, "warning",
                        $"Fetch error on page {fetchedPage.PageNumber}: {fetchedPage.Error.Message}",
                        fetchedPage.Error, fetchedPage.PageNumber, ct);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(fetchedPage.Html))
                    continue;

                try
                {
                    var items = _parser.Parse(fetchedPage.Html, startedAt);
                    allItems.AddRange(items);
                    _progress.Report(new ProgressEvent("parsing",
                        $"Page {fetchedPage.PageNumber}: {items.Count} items parsed.",
                        fetchedPage.PageNumber,
                        30 + (i + 1) * 20.0 / pages.Count));
                }
                catch (ParseException ex)
                {
                    parseErrors.Add((ex.PageNumber, ex.Message));
                    await SafeAppendErrorLogAsync(targetDate, runId, "warning",
                        $"ParseException on page {ex.PageNumber}: {ex.Message}",
                        ex, ex.PageNumber, ct);
                }
            }

            // ---- Step 2.5: Filter by announcement date ----
            // 政府電子採購網的查詢結果偶爾會夾帶非當日（如預告未來日）的標案，
            // 在此防呆：只保留 announcementDate 與 targetDate（民國）一致的 row。
            var rocDate = _dateConverter.DateOnlyToRoc(targetDate);
            var beforeFilter = allItems.Count;
            allItems = allItems.Where(i => i.AnnouncementDate == rocDate).ToList();
            var droppedCount = beforeFilter - allItems.Count;
            if (droppedCount > 0)
            {
                Console.Error.WriteLine($"[Orchestrator] Dropped {droppedCount} item(s) with announcementDate != {rocDate}");
                await SafeAppendErrorLogAsync(targetDate, runId, "warning",
                    $"Dropped {droppedCount} item(s) with mismatched announcement date (expected {rocDate})",
                    null, null, ct);
            }

            // ---- Step 3: Annotate Keywords ----
            _progress.Report(new ProgressEvent("annotating", "Annotating matched keywords...", null, 60));

            var annotatedItems = AnnotateKeywords(allItems, keywordSet);

            // ---- Step 4: Merge into daily snapshot ----
            _progress.Report(new ProgressEvent("merging", "Merging into daily snapshot...", null, 70));

            MergeResult mergeResult;
            try
            {
                mergeResult = await _tenderRepo.MergeDailySnapshotAsync(
                    targetDate, annotatedItems, startedAt, ct);
            }
            catch (IOException ex)
            {
                return await HandleIoFailureAsync(
                    runId, targetDate, triggerSource, startedAt, ex, ct);
            }

            _progress.Report(new ProgressEvent("merging",
                $"Merge done: inserted={mergeResult.InsertedCount}, updated={mergeResult.UpdatedCount}, skipped={mergeResult.SkippedCount}",
                null, 80));

            // ---- Step 5: 決定最終狀態 ----
            string? errorMessage = null;
            if (parseErrors.Count > 0)
            {
                var pageNums = string.Join(", ", parseErrors.Select(e => e.page));
                errorMessage = $"部分頁面解析失敗（第 {pageNums} 頁）";
            }

            var finishedAt = _clock.Now;
            var run = new CrawlRun
            {
                RunId = runId,
                TargetDate = targetDate.ToString("yyyy-MM-dd"),
                TriggerSource = triggerSource,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                Status = RunStatus.Success,
                InsertedCount = mergeResult.InsertedCount,
                UpdatedCount = mergeResult.UpdatedCount,
                SkippedCount = mergeResult.SkippedCount,
                ErrorMessage = errorMessage,
            };

            // ---- Step 6: Generate summary ----
            _progress.Report(new ProgressEvent("summary", "Generating daily summary...", null, 90));
            try
            {
                await _summaryService.GenerateAsync(targetDate, run, ct);
            }
            catch (Exception ex)
            {
                await SafeAppendErrorLogAsync(targetDate, runId, "error",
                    $"Failed to write summary.json: {ex.Message}", ex, null, ct);
            }

            // ---- Step 7: Append run log ----
            await SafeAppendRunAsync(targetDate, run, ct);

            _progress.Report(new ProgressEvent("done", $"Crawl completed. {mergeResult.InsertedCount} new items.", null, 100));

            return run;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await HandleUnexpectedFailureAsync(
                runId, targetDate, triggerSource, startedAt, ex, ct);
        }
    }

    // ======================================================================
    // Private helpers
    // ======================================================================

    private IReadOnlyList<TenderItem> AnnotateKeywords(
        IEnumerable<TenderItem> items,
        KeywordSet keywordSet)
    {
        if (!keywordSet.Groups.Any())
            return items.ToList().AsReadOnly();

        return items.Select(item =>
        {
            var matched = _keywordMatcher.Match(item, keywordSet);
            return matched.Count > 0
                ? item with { MatchedKeywords = matched }
                : item;
        }).ToList().AsReadOnly();
    }

    private async Task<CrawlRun> HandleNetworkFailureAsync(
        string runId, DateOnly targetDate, TriggerSource triggerSource,
        DateTimeOffset startedAt, CrawlNetworkException ex, CancellationToken ct)
    {
        await SafeAppendErrorLogAsync(targetDate, runId, "error",
            $"CrawlNetworkException: {ex.Message}", ex, null, ct);

        var run = new CrawlRun
        {
            RunId = runId,
            TargetDate = targetDate.ToString("yyyy-MM-dd"),
            TriggerSource = triggerSource,
            StartedAt = startedAt,
            FinishedAt = _clock.Now,
            Status = RunStatus.Failed,
            ErrorMessage = ex.Message,
        };

        await SafeWriteFailedSummaryAsync(targetDate, run, ct);
        await SafeAppendRunAsync(targetDate, run, ct);
        return run;
    }

    private async Task<CrawlRun> HandleIoFailureAsync(
        string runId, DateOnly targetDate, TriggerSource triggerSource,
        DateTimeOffset startedAt, IOException ex, CancellationToken ct)
    {
        await SafeAppendErrorLogAsync(targetDate, runId, "error",
            $"IOException: {ex.Message}", ex, null, ct);

        var run = new CrawlRun
        {
            RunId = runId,
            TargetDate = targetDate.ToString("yyyy-MM-dd"),
            TriggerSource = triggerSource,
            StartedAt = startedAt,
            FinishedAt = _clock.Now,
            Status = RunStatus.Failed,
            ErrorMessage = ex.Message,
        };

        await SafeWriteFailedSummaryAsync(targetDate, run, ct);
        await SafeAppendRunAsync(targetDate, run, ct);
        return run;
    }

    private async Task<CrawlRun> HandleUnexpectedFailureAsync(
        string runId, DateOnly targetDate, TriggerSource triggerSource,
        DateTimeOffset startedAt, Exception ex, CancellationToken ct)
    {
        await SafeAppendErrorLogAsync(targetDate, runId, "error",
            $"{ex.GetType().Name}: {ex.Message}", ex, null, ct);

        var run = new CrawlRun
        {
            RunId = runId,
            TargetDate = targetDate.ToString("yyyy-MM-dd"),
            TriggerSource = triggerSource,
            StartedAt = startedAt,
            FinishedAt = _clock.Now,
            Status = RunStatus.Failed,
            ErrorMessage = ex.Message,
        };

        await SafeWriteFailedSummaryAsync(targetDate, run, ct);
        await SafeAppendRunAsync(targetDate, run, ct);
        return run;
    }

    private async Task SafeWriteFailedSummaryAsync(DateOnly date, CrawlRun run, CancellationToken ct)
    {
        try
        {
            await _summaryService.GenerateAsync(date, run, ct);
        }
        catch (Exception ex)
        {
            await SafeAppendErrorLogAsync(date, run.RunId, "error",
                $"Also failed to write summary.json: {ex.Message}", ex, null, ct);
        }
    }

    private async Task SafeAppendRunAsync(DateOnly date, CrawlRun run, CancellationToken ct)
    {
        try
        {
            await _crawlRunLogRepo.AppendRunAsync(date, run, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Orchestrator] Failed to append run log: {ex.Message}");
        }
    }

    private async Task SafeAppendErrorLogAsync(
        DateOnly date, string runId, string severity,
        string message, Exception? ex, int? page, CancellationToken ct)
    {
        try
        {
            var entry = new Storage.Repositories.ErrorLogEntry(
                Timestamp: _clock.Now,
                Severity: severity,
                Source: "crawler",
                RunId: runId,
                Message: message,
                ExceptionDetail: ex?.ToString(),
                Page: page);

            await _errorLogWriter.AppendAsync(date, entry, ct);
        }
        catch
        {
            // 錯誤日誌寫入失敗不應傳播
        }
    }

    /// <summary>
    /// 嘗試建立 crawler.lock 檔案。回傳 IAsyncDisposable 鎖柄；若已鎖定則回傳 null。
    /// </summary>
    private async Task<IAsyncDisposable?> TryAcquireLockAsync(CancellationToken ct)
    {
        var lockFile = _dataPaths.CrawlerLockFile;
        var lockDir = Path.GetDirectoryName(lockFile);
        if (!string.IsNullOrEmpty(lockDir))
            Directory.CreateDirectory(lockDir);

        FileStream? fs = null;
        try
        {
            // FileMode.CreateNew + FileShare.None：若檔案已存在則 IOException → 另一個 run 進行中
            fs = new FileStream(
                lockFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                useAsync: true);

            await fs.WriteAsync(System.Text.Encoding.UTF8.GetBytes(Environment.ProcessId.ToString()), ct);
            await fs.FlushAsync(ct);

            return new LockHandle(fs, lockFile);
        }
        catch (IOException)
        {
            fs?.Dispose();
            return null;
        }
    }

    private static TriggerSource MapTriggerSource(CrawlerMode mode) => mode switch
    {
        CrawlerMode.Scheduled  => TriggerSource.Scheduled,
        CrawlerMode.Catchup    => TriggerSource.Catchup,
        CrawlerMode.Manual     => TriggerSource.Manual,
        CrawlerMode.ManualRedo => TriggerSource.ManualRedo,
        _                      => TriggerSource.Manual,
    };

    // ======================================================================
    // Inner: LockHandle
    // ======================================================================

    private sealed class LockHandle : IAsyncDisposable
    {
        private readonly FileStream _fs;
        private readonly string _lockPath;

        public LockHandle(FileStream fs, string lockPath)
        {
            _fs = fs;
            _lockPath = lockPath;
        }

        public async ValueTask DisposeAsync()
        {
            await _fs.DisposeAsync();
            try { File.Delete(_lockPath); }
            catch { /* 刪除失敗不阻擋 */ }
        }
    }
}
