using FluentAssertions;
using Tender.Core.Models;
using Tender.Storage.Atomic;
using Tender.Storage.Paths;
using Tender.Storage.Repositories;
using Xunit;

namespace Tender.Storage.Tests;

/// <summary>
/// 每個測試使用獨立臨時目錄，測試後清理。
/// </summary>
public sealed class RepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IDataPaths _paths;
    private readonly IAtomicJsonWriter _writer;

    public RepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "tender-tests-repos", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _paths = new LocalAppDataPaths(_tempDir);
        _writer = new AtomicJsonWriter();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static TenderItem MakeItem(string pk, DateTimeOffset? createdAt = null)
    {
        var t = createdAt ?? DateTimeOffset.UtcNow;
        return new TenderItem
        {
            SourcePk = pk,
            AgencyName = $"Agency_{pk}",
            TenderName = $"Tender_{pk}",
            TenderMethod = "公開招標",
            AnnouncementDate = "115/05/08",
            DetailUrl = $"https://example.com/{pk}",
            CreatedAt = t,
            LastSeenAt = t,
        };
    }

    // ========== DailySummaryRepository ==========

    [Fact]
    public async Task DailySummaryRepository_SaveAndLoad_RoundTrip()
    {
        var repo = new DailySummaryRepository(_paths, _writer);
        var date = new DateOnly(2026, 5, 8);
        var now = new DateTimeOffset(2026, 5, 8, 17, 5, 30, TimeSpan.FromHours(8));

        var summary = new DailySummary
        {
            Date = "2026-05-08",
            TotalCount = 42,
            LastRunStatus = RunStatus.Success,
            LastRunAt = now,
            InsertedCount = 40,
            UpdatedCount = 2,
            SkippedCount = 0,
        };

        await repo.SaveAsync(summary);
        var loaded = await repo.LoadAsync(date);

        loaded.Should().NotBeNull();
        loaded!.TotalCount.Should().Be(42);
        loaded.LastRunStatus.Should().Be(RunStatus.Success);
        loaded.InsertedCount.Should().Be(40);
        loaded.Date.Should().Be("2026-05-08");
    }

    [Fact]
    public async Task DailySummaryRepository_LoadMissing_ReturnsNull()
    {
        var repo = new DailySummaryRepository(_paths, _writer);
        var result = await repo.LoadAsync(new DateOnly(2099, 1, 1));
        result.Should().BeNull();
    }

    // ========== CrawlRunLogRepository ==========

    [Fact]
    public async Task CrawlRunLogRepository_AppendRun_CreatesFile()
    {
        var repo = new CrawlRunLogRepository(_paths, _writer);
        var date = new DateOnly(2026, 5, 8);
        var now = DateTimeOffset.UtcNow;

        var run = new CrawlRun
        {
            RunId = "20260508-170000",
            TargetDate = "2026-05-08",
            TriggerSource = TriggerSource.Scheduled,
            StartedAt = now,
            FinishedAt = now.AddMinutes(5),
            Status = RunStatus.Success,
            InsertedCount = 55,
        };

        await repo.AppendRunAsync(date, run);

        var loaded = await repo.LoadAsync(date);
        loaded.Should().NotBeNull();
        loaded!.Runs.Should().ContainSingle();
        loaded.Runs[0].RunId.Should().Be("20260508-170000");
        loaded.Runs[0].TriggerSource.Should().Be(TriggerSource.Scheduled);
    }

    [Fact]
    public async Task CrawlRunLogRepository_AppendRun_AccumulatesRuns()
    {
        var repo = new CrawlRunLogRepository(_paths, _writer);
        var date = new DateOnly(2026, 5, 8);
        var t1 = new DateTimeOffset(2026, 5, 8, 17, 0, 0, TimeSpan.FromHours(8));
        var t2 = new DateTimeOffset(2026, 5, 8, 18, 0, 0, TimeSpan.FromHours(8));

        var run1 = new CrawlRun { RunId = "R1", TargetDate = "2026-05-08", TriggerSource = TriggerSource.Scheduled, StartedAt = t1, FinishedAt = t1.AddMinutes(5), Status = RunStatus.Success };
        var run2 = new CrawlRun { RunId = "R2", TargetDate = "2026-05-08", TriggerSource = TriggerSource.Manual, StartedAt = t2, FinishedAt = t2.AddMinutes(3), Status = RunStatus.Success };

        await repo.AppendRunAsync(date, run1);
        await repo.AppendRunAsync(date, run2);

        var loaded = await repo.LoadAsync(date);
        loaded!.Runs.Should().HaveCount(2);
        loaded.Runs[0].RunId.Should().Be("R1");
        loaded.Runs[1].RunId.Should().Be("R2");
    }

    // ========== ErrorLogWriter ==========

    [Fact]
    public async Task ErrorLogWriter_Append_CreatesFileWithEntry()
    {
        var writer = new ErrorLogWriter(_paths);
        var date = new DateOnly(2026, 5, 8);

        var entry = new ErrorLogEntry(
            Timestamp: DateTimeOffset.UtcNow,
            Severity: "error",
            Source: "crawler",
            RunId: "20260508-170000",
            Message: "Test error",
            ExceptionDetail: null,
            Page: null);

        await writer.AppendAsync(date, entry);

        var filePath = _paths.ErrorsLogFile(date);
        File.Exists(filePath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(filePath);
        lines.Should().HaveCount(1);
        lines[0].Should().Contain("Test error");
    }

    [Fact]
    public async Task ErrorLogWriter_AppendMultiple_AccumulatesLines()
    {
        var writer = new ErrorLogWriter(_paths);
        var date = new DateOnly(2026, 5, 8);
        var ts = DateTimeOffset.UtcNow;

        await writer.AppendAsync(date, new ErrorLogEntry(ts, "warning", "crawler", null, "Line 1", null, 1));
        await writer.AppendAsync(date, new ErrorLogEntry(ts, "error", "crawler", null, "Line 2", null, 2));

        var lines = await File.ReadAllLinesAsync(_paths.ErrorsLogFile(date));
        lines.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ErrorLogWriter_DifferentDates_WriteToSeparateFiles()
    {
        var writer = new ErrorLogWriter(_paths);
        var date1 = new DateOnly(2026, 5, 7);
        var date2 = new DateOnly(2026, 5, 8);
        var ts = DateTimeOffset.UtcNow;

        await writer.AppendAsync(date1, new ErrorLogEntry(ts, "error", "crawler", null, "Day1 error", null, null));
        await writer.AppendAsync(date2, new ErrorLogEntry(ts, "error", "crawler", null, "Day2 error", null, null));

        var content1 = await File.ReadAllTextAsync(_paths.ErrorsLogFile(date1));
        var content2 = await File.ReadAllTextAsync(_paths.ErrorsLogFile(date2));

        content1.Should().Contain("Day1 error");
        content2.Should().Contain("Day2 error");
        content1.Should().NotContain("Day2 error");
        content2.Should().NotContain("Day1 error");
    }

    // ========== AppSettingsRepository ==========

    [Fact]
    public async Task AppSettingsRepository_SaveAndLoad_RoundTrip()
    {
        var repo = new AppSettingsRepository(_paths, _writer);

        var settings = new AppSettings
        {
            ScheduledTime = "16:30",
            MaxRetries = 5,
            RequestDelayMs = 2000,
        };

        await repo.SaveAsync(settings);
        var loaded = await repo.LoadAsync();

        loaded.ScheduledTime.Should().Be("16:30");
        loaded.MaxRetries.Should().Be(5);
        loaded.RequestDelayMs.Should().Be(2000);
    }

    [Fact]
    public async Task AppSettingsRepository_MissingFile_ReturnsDefaults()
    {
        var repo = new AppSettingsRepository(_paths, _writer);
        var loaded = await repo.LoadAsync();
        loaded.ScheduledTime.Should().Be("17:00");
        loaded.MaxRetries.Should().Be(3);
    }

    [Fact]
    public async Task AppSettingsRepository_MigratesLegacyTenderMethodName()
    {
        // 模擬 2026-05-11 改名前產出的 settings.json：保留舊字串「公開取得電子報價單」
        var legacyJson = """
            {
              "scheduledTime": "17:00",
              "catchupEnabled": true,
              "updateCheckEnabled": true,
              "requestDelayMs": 1500,
              "maxRetries": 3,
              "dataRetentionMonths": 6,
              "targetTenderMethods": [
                "公開招標",
                "公開取得電子報價單",
                "經公開評選或公開徵求之限制性招標"
              ]
            }
            """;
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.AppSettingsFile)!);
        await File.WriteAllTextAsync(_paths.AppSettingsFile, legacyJson, System.Text.Encoding.UTF8);

        var repo = new AppSettingsRepository(_paths, _writer);
        var loaded = await repo.LoadAsync();

        loaded.TargetTenderMethods.Should().BeEquivalentTo(new[]
        {
            "公開招標",
            "公開取得報價單或企劃書",
            "經公開評選或公開徵求之限制性招標",
        });
    }

    // ========== UserMarksRepository ==========

    [Fact]
    public async Task UserMarksRepository_SaveAndLoad_RoundTrip()
    {
        var repo = new UserMarksRepository(_paths, _writer);

        var marks = new UserMarks
        {
            Marks = new[]
            {
                new UserMark { SourcePk = "PK001", IsFavorite = true, IsRead = false, Note = "重要" }
            }
        };

        await repo.SaveAsync(marks);
        var loaded = await repo.LoadAsync();

        loaded.Marks.Should().HaveCount(1);
        loaded.Marks[0].SourcePk.Should().Be("PK001");
        loaded.Marks[0].IsFavorite.Should().BeTrue();
        loaded.Marks[0].Note.Should().Be("重要");
    }

    // ========== KeywordsRepository ==========

    [Fact]
    public async Task KeywordsRepository_MissingFile_ReturnsDefaultKeywords()
    {
        var repo = new KeywordsRepository(_paths, _writer);
        var loaded = await repo.LoadAsync();

        loaded.Groups.Should().NotBeEmpty("default keywords should be loaded from embedded resource");
        loaded.Groups.Any(g => g.Name == "資訊系統").Should().BeTrue();
    }

    [Fact]
    public async Task KeywordsRepository_SaveAndLoad_RoundTrip()
    {
        var repo = new KeywordsRepository(_paths, _writer);

        var kws = new KeywordSet
        {
            Groups = new[]
            {
                new KeywordGroup
                {
                    Name = "測試分類",
                    Items = new[]
                    {
                        new KeywordItem { Keyword = "測試關鍵字", TargetField = "tenderName", Enabled = true }
                    }
                }
            }
        };

        await repo.SaveAsync(kws);
        var loaded = await repo.LoadAsync();

        loaded.Groups.Should().HaveCount(1);
        loaded.Groups[0].Name.Should().Be("測試分類");
        loaded.Groups[0].Items[0].Keyword.Should().Be("測試關鍵字");
    }
}
