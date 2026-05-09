using FluentAssertions;
using Tender.Core.DateConversion;
using Tender.Core.Models;
using Tender.Core.Search;
using Xunit;

namespace Tender.Core.Tests;

public sealed class SearchServiceTests
{
    private readonly SearchService _sut = new(new TaiwanDateConverter());
    private static readonly DateOnly Today = new(2026, 5, 8);

    private static TenderItem MakeItem(
        string sourcePk,
        string tenderName = "標案",
        string agencyName = "機關",
        string tenderMethod = "公開招標",
        string? procurementType = "勞務類",
        string announcementDate = "115/05/08",
        string? bidDeadline = "115/05/12",
        long? budgetAmount = 1_000_000,
        string[]? matchedKeywords = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new TenderItem
        {
            SourcePk = sourcePk,
            TenderName = tenderName,
            AgencyName = agencyName,
            TenderMethod = tenderMethod,
            ProcurementType = procurementType,
            AnnouncementDate = announcementDate,
            BidDeadline = bidDeadline,
            BudgetAmount = budgetAmount,
            DetailUrl = "https://example.com",
            MatchedKeywords = matchedKeywords ?? Array.Empty<string>(),
            CreatedAt = now,
            LastSeenAt = now,
        };
    }

    private static readonly IReadOnlyList<TenderItem> SampleItems = new[]
    {
        MakeItem("A001", "AI 系統建置", "臺北市政府", "公開招標", "勞務類", "115/05/01", "115/05/10", 500_000, new[] { "AI", "系統" }),
        MakeItem("A002", "無障礙網站評估", "高雄市政府", "公開取得電子報價單", "勞務類", "115/05/03", "115/05/15", 200_000, new[] { "無障礙" }),
        MakeItem("A003", "智慧倉儲系統", "經濟部", "公開招標", "財物類", "115/05/06", "115/04/30", 5_000_000, new[] { "智慧", "倉儲" }),
        MakeItem("A004", "ESG 永續報告書", "原住民族委員會", "公開招標", "勞務類", "115/05/08", null, null, new[] { "ESG" }),
    };

    // ---- KeywordQuery AND 邏輯 ----
    [Fact]
    public void Search_KeywordQuery_SingleToken_FiltersCorrectly()
    {
        var criteria = new SearchCriteria { KeywordQuery = "AI" };
        var result = _sut.Search(SampleItems, criteria, SortKey.None, SortDirection.Ascending, Today);
        result.Should().ContainSingle(x => x.SourcePk == "A001");
    }

    [Fact]
    public void Search_KeywordQuery_MultipleTokensAndLogic()
    {
        // "AI 系統" → 必須同時含 AI 和 系統
        var criteria = new SearchCriteria { KeywordQuery = "AI 系統" };
        var result = _sut.Search(SampleItems, criteria, SortKey.None, SortDirection.Ascending, Today);
        result.Should().ContainSingle(x => x.SourcePk == "A001");
    }

    [Fact]
    public void Search_KeywordQuery_NoMatch_ReturnsEmpty()
    {
        var criteria = new SearchCriteria { KeywordQuery = "XXXXXX不存在" };
        var result = _sut.Search(SampleItems, criteria, SortKey.None, SortDirection.Ascending, Today);
        result.Should().BeEmpty();
    }

    // ---- ActiveKeywordButtons OR 邏輯 ----
    [Fact]
    public void Search_ActiveKeywordButtons_OrLogic()
    {
        var criteria = new SearchCriteria { ActiveKeywordButtons = new[] { "AI", "ESG" } };
        var result = _sut.Search(SampleItems, criteria, SortKey.None, SortDirection.Ascending, Today);
        result.Should().HaveCount(2)
            .And.Contain(x => x.SourcePk == "A001")
            .And.Contain(x => x.SourcePk == "A004");
    }

    // ---- TenderMethod 篩選 ----
    [Fact]
    public void Search_TenderMethod_FiltersCorrectly()
    {
        var criteria = new SearchCriteria { TenderMethod = "公開取得電子報價單" };
        var result = _sut.Search(SampleItems, criteria, SortKey.None, SortDirection.Ascending, Today);
        result.Should().ContainSingle(x => x.SourcePk == "A002");
    }

    // ---- ProcurementType 篩選 ----
    [Fact]
    public void Search_ProcurementType_FiltersCorrectly()
    {
        var criteria = new SearchCriteria { ProcurementType = "財物類" };
        var result = _sut.Search(SampleItems, criteria, SortKey.None, SortDirection.Ascending, Today);
        result.Should().ContainSingle(x => x.SourcePk == "A003");
    }

    // ---- AnnouncementDate 區間篩選 ----
    [Fact]
    public void Search_AnnouncementDateFrom_FiltersCorrectly()
    {
        var criteria = new SearchCriteria { AnnouncementDateFrom = "115/05/06" };
        var result = _sut.Search(SampleItems, criteria, SortKey.None, SortDirection.Ascending, Today);
        result.Should().HaveCount(2)
            .And.Contain(x => x.SourcePk == "A003")
            .And.Contain(x => x.SourcePk == "A004");
    }

    [Fact]
    public void Search_AnnouncementDateRange_FiltersCorrectly()
    {
        var criteria = new SearchCriteria
        {
            AnnouncementDateFrom = "115/05/03",
            AnnouncementDateTo = "115/05/06"
        };
        var result = _sut.Search(SampleItems, criteria, SortKey.None, SortDirection.Ascending, Today);
        result.Should().HaveCount(2)
            .And.Contain(x => x.SourcePk == "A002")
            .And.Contain(x => x.SourcePk == "A003");
    }

    // ---- BidDeadline 區間篩選 ----
    [Fact]
    public void Search_BidDeadlineFrom_FiltersCorrectly()
    {
        var criteria = new SearchCriteria { BidDeadlineFrom = "115/05/12" };
        var result = _sut.Search(SampleItems, criteria, SortKey.None, SortDirection.Ascending, Today);
        result.Should().ContainSingle(x => x.SourcePk == "A002");
    }

    // ---- BudgetAmount 區間篩選 ----
    [Fact]
    public void Search_BudgetMin_FiltersCorrectly()
    {
        var criteria = new SearchCriteria { BudgetMin = 1_000_000 };
        var result = _sut.Search(SampleItems, criteria, SortKey.None, SortDirection.Ascending, Today);
        result.Should().ContainSingle(x => x.SourcePk == "A003");
    }

    [Fact]
    public void Search_BudgetMax_FiltersCorrectly()
    {
        var criteria = new SearchCriteria { BudgetMax = 300_000 };
        var result = _sut.Search(SampleItems, criteria, SortKey.None, SortDirection.Ascending, Today);
        result.Should().ContainSingle(x => x.SourcePk == "A002");
    }

    // ---- ShowActiveOnly ----
    [Fact]
    public void Search_ShowActiveOnly_FiltersExpiredBidDeadline()
    {
        // A003 的 bidDeadline 是 115/04/30（2026-04-30），早於 today(2026-05-08)，應被篩掉
        // A004 沒有 bidDeadline，也應被篩掉
        var criteria = new SearchCriteria { ShowActiveOnly = true };
        var result = _sut.Search(SampleItems, criteria, SortKey.None, SortDirection.Ascending, Today);
        result.Should().HaveCount(2)
            .And.Contain(x => x.SourcePk == "A001")
            .And.Contain(x => x.SourcePk == "A002");
    }

    // ---- AND 組合篩選 ----
    [Fact]
    public void Search_CombinedCriteria_AndLogic()
    {
        var criteria = new SearchCriteria
        {
            TenderMethod = "公開招標",
            ProcurementType = "勞務類"
        };
        var result = _sut.Search(SampleItems, criteria, SortKey.None, SortDirection.Ascending, Today);
        result.Should().HaveCount(2)
            .And.Contain(x => x.SourcePk == "A001")
            .And.Contain(x => x.SourcePk == "A004");
    }

    // ---- 排序測試 ----
    [Fact]
    public void Search_SortByAgencyName_Ascending()
    {
        var result = _sut.Search(SampleItems, new SearchCriteria(), SortKey.AgencyName, SortDirection.Ascending, Today);
        result.Select(x => x.AgencyName).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Search_SortByBudgetAmount_NullLast()
    {
        // A004 BudgetAmount = null，應排在最後
        var result = _sut.Search(SampleItems, new SearchCriteria(), SortKey.BudgetAmount, SortDirection.Ascending, Today);
        result.Last().SourcePk.Should().Be("A004", "null BudgetAmount should be sorted last");
    }

    [Fact]
    public void Search_SortByBidDeadline_NullLast()
    {
        // A004 BidDeadline = null，應排在最後
        var result = _sut.Search(SampleItems, new SearchCriteria(), SortKey.BidDeadline, SortDirection.Ascending, Today);
        result.Last().SourcePk.Should().Be("A004", "null BidDeadline should be sorted last");
    }

    // ---- 空條件 → 全部回傳 ----
    [Fact]
    public void Search_EmptyCriteria_ReturnsAll()
    {
        var result = _sut.Search(SampleItems, new SearchCriteria(), SortKey.None, SortDirection.Ascending, Today);
        result.Should().HaveCount(SampleItems.Count);
    }
}
