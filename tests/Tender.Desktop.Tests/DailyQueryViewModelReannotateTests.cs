using FluentAssertions;
using Moq;
using Tender.Core.Clock;
using Tender.Core.DateConversion;
using Tender.Core.Keywords;
using Tender.Core.Models;
using Tender.Core.Search;
using Tender.Desktop.Services;
using Tender.Desktop.ViewModels;
using Tender.Storage.Repositories;
using Xunit;

namespace Tender.Desktop.Tests;

/// <summary>
/// 迴歸測試：使用者在「篩選類別設定」事後新增的關鍵字（例 TEST1/交流），
/// 上方篩選按鈕也要找得到舊資料。
///
/// Bug 起因：篩選按鈕比對的是 TenderItem.MatchedKeywords，而該欄位是爬蟲當下算好寫進
/// tenders.json 的。事後新增關鍵字後，舊資料的 MatchedKeywords 不含新關鍵字 → 按鈕篩選 0 筆。
/// 修正：DailyQueryViewModel.LoadAsync 載入時以最新 KeywordSet 重新標註 MatchedKeywords。
/// </summary>
public sealed class DailyQueryViewModelReannotateTests
{
    private static TenderItem MakeItem(string pk, string tenderName, string agencyName) =>
        new()
        {
            SourcePk = pk,
            AgencyName = agencyName,
            TenderName = tenderName,
            TenderMethod = "公開取得報價單或企劃書",
            AnnouncementDate = "115/06/16",
            DetailUrl = $"https://example/{pk}",
            // 刻意留空：模擬「爬蟲當下沒有此關鍵字」的舊資料
            MatchedKeywords = Array.Empty<string>(),
            CreatedAt = DateTimeOffset.UnixEpoch,
            LastSeenAt = DateTimeOffset.UnixEpoch,
        };

    private static DailyQueryViewModel Build(
        IReadOnlyList<TenderItem> items,
        KeywordSet keywordSet,
        out DateOnly date)
    {
        date = new DateOnly(2026, 6, 16);
        var theDate = date;

        var tenderRepo = new Mock<ITenderRepository>();
        tenderRepo.Setup(r => r.LoadAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyTenderSnapshot
            {
                Date = theDate.ToString("yyyy-MM-dd"),
                GeneratedAt = DateTimeOffset.UnixEpoch,
                Source = "test",
                Items = items,
            });

        var keywordsRepo = new Mock<IKeywordsRepository>();
        keywordsRepo.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(keywordSet);

        var userMarksRepo = new Mock<IUserMarksRepository>();
        userMarksRepo.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserMarks { Marks = Array.Empty<UserMark>() });

        var savedSearchesRepo = new Mock<ISavedSearchesRepository>();
        savedSearchesRepo.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavedSearchSet());

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.Now).Returns(new DateTimeOffset(theDate.ToDateTime(TimeOnly.MinValue)));

        var vm = new DailyQueryViewModel(
            tenderRepo.Object,
            Mock.Of<IBrowserLauncher>(),
            new SearchService(new TaiwanDateConverter()),
            keywordsRepo.Object,
            new KeywordMatcher(),
            clock.Object,
            Mock.Of<IExcelExporter>(),
            Mock.Of<IXlsmExporter>(),
            Mock.Of<IIsmsXlsmExporter>(),
            Mock.Of<IEsgXlsmExporter>(),
            Mock.Of<ISaveFileDialogService>(),
            userMarksRepo.Object,
            savedSearchesRepo.Object);
        vm.SetDates(theDate, null);
        return vm;
    }

    private static KeywordSet KeywordSetWith(string groupName, string keyword, string targetField) =>
        new()
        {
            Groups = new[]
            {
                new KeywordGroup
                {
                    Name = groupName,
                    Color = "#806040",
                    Items = new[]
                    {
                        new KeywordItem { Keyword = keyword, TargetField = targetField, Enabled = true },
                    },
                },
            },
        };

    [Fact]
    public async Task LoadAsync_ReannotatesMatchedKeywords_FromCurrentKeywordSet()
    {
        // 舊資料 MatchedKeywords 為空，但標案名稱含「交流」
        var items = new[]
        {
            MakeItem("a", "115年跆拳道隊赴泰國移地訓練交流計畫", "新北市立育林國民中學"),
            MakeItem("b", "資訊系統維護案", "某機關"),
        };
        var vm = Build(items, KeywordSetWith("TEST1", "交流", "any"), out _);

        await vm.LoadCommand.ExecuteAsync(null);

        // 載入後應已用最新 KeywordSet 重新標註
        vm.AllItems.Single(i => i.SourcePk == "a").Item.MatchedKeywords.Should().Contain("交流");
        vm.AllItems.Single(i => i.SourcePk == "b").Item.MatchedKeywords.Should().BeEmpty();
    }

    [Fact]
    public async Task ActivatingNewlyAddedKeywordButton_FiltersOldData()
    {
        var items = new[]
        {
            MakeItem("a", "115年跆拳道隊赴泰國移地訓練交流計畫", "新北市立育林國民中學"),
            MakeItem("b", "資訊系統維護案", "某機關"),
        };
        var vm = Build(items, KeywordSetWith("TEST1", "交流", "any"), out _);
        await vm.LoadCommand.ExecuteAsync(null);

        // 勾選 TEST1/交流 按鈕
        var button = vm.KeywordGroups
            .Single(g => g.Name == "TEST1")
            .Buttons.Single(b => b.Keyword == "交流");
        button.IsActive = true;

        vm.FilteredItems.Should().ContainSingle()
            .Which.SourcePk.Should().Be("a");
    }
}
