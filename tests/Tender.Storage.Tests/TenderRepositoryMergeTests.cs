using FluentAssertions;
using Tender.Core.Models;
using Tender.Storage.Atomic;
using Tender.Storage.Paths;
using Tender.Storage.Repositories;
using Xunit;

namespace Tender.Storage.Tests;

/// <summary>
/// MergeDailySnapshotAsync 的各種合併情境測試。
/// 對應 daily_crawl.feature Scenario「同一天內重複執行爬蟲不可產生重複標案」。
/// </summary>
public sealed class TenderRepositoryMergeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IDataPaths _paths;
    private readonly IAtomicJsonWriter _writer;
    private readonly TenderRepository _repo;
    private static readonly DateTimeOffset BaseTime =
        new(2026, 5, 8, 17, 0, 0, TimeSpan.FromHours(8));

    public TenderRepositoryMergeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "tender-tests-merge", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _paths = new LocalAppDataPaths(_tempDir);
        _writer = new AtomicJsonWriter();
        _repo = new TenderRepository(_paths, _writer);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static TenderItem MakeItem(string pk, string tenderName = "標案", DateTimeOffset? at = null)
    {
        var t = at ?? BaseTime;
        return new TenderItem
        {
            SourcePk = pk,
            AgencyName = "機關",
            TenderName = tenderName,
            TenderMethod = "公開招標",
            AnnouncementDate = "115/05/08",
            DetailUrl = "https://example.com/" + pk,
            CreatedAt = t,
            LastSeenAt = t,
        };
    }

    // ---- 全新快照（檔案不存在）----
    [Fact]
    public async Task Merge_EmptyStart_AllInserted()
    {
        var date = new DateOnly(2026, 5, 8);
        var incoming = new[] { MakeItem("PK001"), MakeItem("PK002"), MakeItem("PK003") };

        var result = await _repo.MergeDailySnapshotAsync(date, incoming, BaseTime);

        result.InsertedCount.Should().Be(3);
        result.UpdatedCount.Should().Be(0);
        result.SkippedCount.Should().Be(0);

        var loaded = await _repo.LoadAsync(date);
        loaded!.Items.Should().HaveCount(3);
    }

    // ---- 重複執行，已存在不變則 Skipped ----
    [Fact]
    public async Task Merge_SameItemsAgain_AllSkipped()
    {
        var date = new DateOnly(2026, 5, 8);
        var items = new[] { MakeItem("PK001"), MakeItem("PK002") };

        await _repo.MergeDailySnapshotAsync(date, items, BaseTime);
        var secondResult = await _repo.MergeDailySnapshotAsync(date, items, BaseTime.AddHours(1));

        secondResult.InsertedCount.Should().Be(0);
        secondResult.UpdatedCount.Should().Be(0);
        secondResult.SkippedCount.Should().Be(2);
    }

    // ---- 重複執行，部分新增、部分更新、部分略過 ----
    [Fact]
    public async Task Merge_MixedScenario_CorrectCounts()
    {
        var date = new DateOnly(2026, 5, 8);

        // 先插入 PK001~PK050
        var initial = Enumerable.Range(1, 50)
            .Select(i => MakeItem($"PK{i:D3}"))
            .ToList();
        await _repo.MergeDailySnapshotAsync(date, initial, BaseTime);

        // 第二次：PK001、PK002 內容改動（updated），PK048、PK049 不變（skipped），PK051~053 全新（inserted）
        var second = new[]
        {
            MakeItem("PK001", "更新後標案名稱一"),   // updated
            MakeItem("PK002", "更新後標案名稱二"),   // updated
            MakeItem("PK051"),                         // inserted
            MakeItem("PK052"),                         // inserted
            MakeItem("PK053"),                         // inserted
            MakeItem("PK048"),                         // skipped（TenderName 沒變）
            MakeItem("PK049"),                         // skipped
        };

        var secondTime = BaseTime.AddHours(1.5);
        var result = await _repo.MergeDailySnapshotAsync(date, second, secondTime);

        result.InsertedCount.Should().Be(3);
        result.UpdatedCount.Should().Be(2);
        result.SkippedCount.Should().Be(2);

        var loaded = await _repo.LoadAsync(date);
        loaded!.Items.Should().HaveCount(53, "50 original + 3 new");

        // 所有 SourcePk 唯一
        loaded.Items.Select(x => x.SourcePk).Distinct().Should().HaveCount(53);

        // PK001、PK002 的 LastSeenAt 應更新，CreatedAt 應保留原始值
        var pk001 = loaded.Items.First(x => x.SourcePk == "PK001");
        pk001.LastSeenAt.Should().Be(secondTime);
        pk001.CreatedAt.Should().Be(BaseTime, "CreatedAt should not change on update");

        // PK051 等全新者的 CreatedAt 應為 secondTime
        var pk051 = loaded.Items.First(x => x.SourcePk == "PK051");
        pk051.CreatedAt.Should().Be(secondTime);
    }

    // ---- 去重：SourcePk 在 Items 中只能出現一次 ----
    [Fact]
    public async Task Merge_NoDuplicateSourcePk()
    {
        var date = new DateOnly(2026, 5, 8);
        var initial = new[] { MakeItem("PK001"), MakeItem("PK002") };
        await _repo.MergeDailySnapshotAsync(date, initial, BaseTime);

        // 再次送入已存在的 PK001
        var second = new[] { MakeItem("PK001", "新名稱"), MakeItem("PK003") };
        await _repo.MergeDailySnapshotAsync(date, second, BaseTime.AddHours(1));

        var loaded = await _repo.LoadAsync(date);
        loaded!.Items.Count(x => x.SourcePk == "PK001").Should().Be(1);
    }

    // ---- 跨日資料夾獨立 ----
    [Fact]
    public async Task Merge_CrossDays_IndependentFolders()
    {
        var date1 = new DateOnly(2026, 5, 7);
        var date2 = new DateOnly(2026, 5, 8);

        var items1 = new[] { MakeItem("ABC123"), MakeItem("DAY1_ONLY") };
        var items2 = new[] { MakeItem("ABC123"), MakeItem("DAY2_ONLY") };

        await _repo.MergeDailySnapshotAsync(date1, items1, BaseTime);
        await _repo.MergeDailySnapshotAsync(date2, items2, BaseTime);

        var loaded1 = await _repo.LoadAsync(date1);
        var loaded2 = await _repo.LoadAsync(date2);

        loaded1!.Items.Any(x => x.SourcePk == "ABC123").Should().BeTrue();
        loaded1.Items.Any(x => x.SourcePk == "DAY1_ONLY").Should().BeTrue();
        loaded1.Items.Any(x => x.SourcePk == "DAY2_ONLY").Should().BeFalse();

        loaded2!.Items.Any(x => x.SourcePk == "ABC123").Should().BeTrue();
        loaded2.Items.Any(x => x.SourcePk == "DAY2_ONLY").Should().BeTrue();
        loaded2.Items.Any(x => x.SourcePk == "DAY1_ONLY").Should().BeFalse();
    }

    // ---- 目錄不存在時自動建立 ----
    [Fact]
    public async Task Merge_DirectoryNotExist_AutoCreated()
    {
        var date = new DateOnly(2026, 5, 9);
        var items = new[] { MakeItem("NEW001") };

        await _repo.MergeDailySnapshotAsync(date, items, BaseTime);

        var folder = _paths.DailyFolder(date);
        Directory.Exists(folder).Should().BeTrue();
        File.Exists(_paths.TendersFile(date)).Should().BeTrue();
    }

    // ---- Exists ----
    [Fact]
    public async Task Exists_AfterMerge_ReturnsTrue()
    {
        var date = new DateOnly(2026, 5, 8);
        _repo.Exists(date).Should().BeFalse("file not yet created");

        await _repo.MergeDailySnapshotAsync(date, new[] { MakeItem("X") }, BaseTime);
        _repo.Exists(date).Should().BeTrue();
    }
}
