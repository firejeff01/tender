using FluentAssertions;
using Tender.Core.Keywords;
using Tender.Core.Models;
using Xunit;

namespace Tender.Core.Tests;

public sealed class KeywordMatcherTests
{
    private readonly KeywordMatcher _sut = new();

    private static TenderItem CreateItem(string tenderName, string agencyName = "測試機關")
    {
        var now = DateTimeOffset.UtcNow;
        return new TenderItem
        {
            SourcePk = "TEST001",
            AgencyName = agencyName,
            TenderName = tenderName,
            TenderMethod = "公開招標",
            AnnouncementDate = "115/05/08",
            DetailUrl = "https://example.com",
            CreatedAt = now,
            LastSeenAt = now,
        };
    }

    private static KeywordSet CreateKeywordSet(IReadOnlyList<KeywordGroup> groups)
        => new() { Groups = groups };

    // ---- 資訊系統 group ----
    [Theory]
    [InlineData("數位轉型計畫", "數位")]
    [InlineData("智慧系統建置案", "系統")]
    [InlineData("資訊管理平台", "管理")]
    [InlineData("政府資訊網站服務", "服務")]
    public void Match_資訊系統_HitsCorrectKeyword(string tenderName, string expectedKeyword)
    {
        var item = CreateItem(tenderName);
        var kws = CreateKeywordSet(new[]
        {
            new KeywordGroup
            {
                Name = "資訊系統",
                Items = new[]
                {
                    new KeywordItem { Keyword = expectedKeyword, TargetField = "tenderName", Enabled = true }
                }
            }
        });

        var result = _sut.Match(item, kws);
        result.Should().Contain(expectedKeyword);
    }

    // ---- XR/AI group ----
    [Theory]
    [InlineData("AI 輔助診斷系統", "AI")]
    [InlineData("虛擬實境教育平台", "虛擬")]
    [InlineData("人工智慧應用建置", "人工智慧")]
    [InlineData("AR 導覽服務採購", "AR")]
    public void Match_XRAI_HitsCorrectKeyword(string tenderName, string expectedKeyword)
    {
        var item = CreateItem(tenderName);
        var kws = CreateKeywordSet(new[]
        {
            new KeywordGroup
            {
                Name = "XR/AI",
                Items = new[]
                {
                    new KeywordItem { Keyword = expectedKeyword, TargetField = "tenderName", Enabled = true }
                }
            }
        });

        var result = _sut.Match(item, kws);
        result.Should().Contain(expectedKeyword);
    }

    // ---- 資安/無障礙 group ----
    [Theory]
    [InlineData("ISMS 認證輔導案", "ISMS")]
    [InlineData("資訊安全管理建置", "資訊安全")]
    [InlineData("無障礙網站評估", "無障礙")]
    public void Match_資安無障礙_HitsCorrectKeyword(string tenderName, string expectedKeyword)
    {
        var item = CreateItem(tenderName);
        var kws = CreateKeywordSet(new[]
        {
            new KeywordGroup
            {
                Name = "資安/無障礙",
                Items = new[]
                {
                    new KeywordItem { Keyword = expectedKeyword, TargetField = "tenderName", Enabled = true }
                }
            }
        });

        var result = _sut.Match(item, kws);
        result.Should().Contain(expectedKeyword);
    }

    // ---- ESG/碳管理 group ----
    [Theory]
    [InlineData("ESG 永續報告書建置", "ESG")]
    [InlineData("淨零排放計畫執行", "淨零")]
    [InlineData("碳盤查服務採購", "碳盤查")]
    public void Match_ESG_HitsCorrectKeyword(string tenderName, string expectedKeyword)
    {
        var item = CreateItem(tenderName);
        var kws = CreateKeywordSet(new[]
        {
            new KeywordGroup
            {
                Name = "ESG/碳管理",
                Items = new[]
                {
                    new KeywordItem { Keyword = expectedKeyword, TargetField = "tenderName", Enabled = true }
                }
            }
        });

        var result = _sut.Match(item, kws);
        result.Should().Contain(expectedKeyword);
    }

    // ---- 業務雜項 group ----
    [Theory]
    [InlineData("雲端服務採購案", "雲端")]
    [InlineData("無人機巡檢案", "無人")]
    [InlineData("共同供應契約維運", "共同供應契約")]
    public void Match_業務雜項_HitsCorrectKeyword(string tenderName, string expectedKeyword)
    {
        var item = CreateItem(tenderName);
        var kws = CreateKeywordSet(new[]
        {
            new KeywordGroup
            {
                Name = "業務雜項",
                Items = new[]
                {
                    new KeywordItem { Keyword = expectedKeyword, TargetField = "tenderName", Enabled = true }
                }
            }
        });

        var result = _sut.Match(item, kws);
        result.Should().Contain(expectedKeyword);
    }

    // ---- 智慧管考 group ----
    [Theory]
    [InlineData("管考系統建置案", "管考")]
    [InlineData("智慧巡檢平台", "智慧")]
    [InlineData("計畫管理表單系統", "計畫")]
    public void Match_智慧管考_HitsCorrectKeyword(string tenderName, string expectedKeyword)
    {
        var item = CreateItem(tenderName);
        var kws = CreateKeywordSet(new[]
        {
            new KeywordGroup
            {
                Name = "智慧管考",
                Items = new[]
                {
                    new KeywordItem { Keyword = expectedKeyword, TargetField = "tenderName", Enabled = true }
                }
            }
        });

        var result = _sut.Match(item, kws);
        result.Should().Contain(expectedKeyword);
    }

    // ---- 倉儲自動化 group ----
    [Theory]
    [InlineData("倉儲自動化系統建置", "倉儲")]
    [InlineData("WMS 倉儲管理系統", "WMS")]
    [InlineData("物料管理系統採購", "物料管理")]
    public void Match_倉儲自動化_HitsCorrectKeyword(string tenderName, string expectedKeyword)
    {
        var item = CreateItem(tenderName);
        var kws = CreateKeywordSet(new[]
        {
            new KeywordGroup
            {
                Name = "倉儲自動化",
                Items = new[]
                {
                    new KeywordItem { Keyword = expectedKeyword, TargetField = "tenderName", Enabled = true }
                }
            }
        });

        var result = _sut.Match(item, kws);
        result.Should().Contain(expectedKeyword);
    }

    // ---- 地區 group（any 欄位）----
    [Theory]
    [InlineData("高雄智慧城市計畫", "高雄", "測試機關")]
    [InlineData("臺北市政府資訊案", "臺北", "臺北市政府")]
    [InlineData("一般標案名稱", "屏東", "屏東縣政府")]  // agencyName 命中
    public void Match_地區_HitsCorrectKeyword(string tenderName, string expectedKeyword, string agencyName)
    {
        var item = CreateItem(tenderName, agencyName);
        var kws = CreateKeywordSet(new[]
        {
            new KeywordGroup
            {
                Name = "地區",
                Items = new[]
                {
                    new KeywordItem { Keyword = expectedKeyword, TargetField = "any", Enabled = true }
                }
            }
        });

        var result = _sut.Match(item, kws);
        result.Should().Contain(expectedKeyword);
    }

    // ---- 指定機關 group（agencyName）----
    [Fact]
    public void Match_指定機關_HitsAgencyName()
    {
        var item = CreateItem("資訊系統建置", "經濟部水利署");
        var kws = CreateKeywordSet(new[]
        {
            new KeywordGroup
            {
                Name = "指定機關",
                Items = new[]
                {
                    new KeywordItem { Keyword = "經濟部水利署", TargetField = "agencyName", Enabled = true }
                }
            }
        });

        var result = _sut.Match(item, kws);
        result.Should().Contain("經濟部水利署");
    }

    // ---- 停用關鍵字不應命中 ----
    [Fact]
    public void Match_DisabledKeyword_ShouldNotHit()
    {
        var item = CreateItem("AI 智慧系統");
        var kws = CreateKeywordSet(new[]
        {
            new KeywordGroup
            {
                Name = "XR/AI",
                Items = new[]
                {
                    new KeywordItem { Keyword = "AI", TargetField = "tenderName", Enabled = false }
                }
            }
        });

        var result = _sut.Match(item, kws);
        result.Should().BeEmpty("disabled keywords should not match");
    }

    // ---- 無命中時回傳空集合 ----
    [Fact]
    public void Match_NoMatch_ReturnsEmpty()
    {
        var item = CreateItem("完全不相關的標案名稱");
        var kws = CreateKeywordSet(new[]
        {
            new KeywordGroup
            {
                Name = "資訊系統",
                Items = new[]
                {
                    new KeywordItem { Keyword = "XXXXXX不可能存在", TargetField = "tenderName", Enabled = true }
                }
            }
        });

        _sut.Match(item, kws).Should().BeEmpty();
    }

    // ---- IsMatch ----
    [Theory]
    [InlineData("AI 輔助診斷", "測試機關", "AI", "tenderName", true)]
    [InlineData("一般標案", "測試機關", "AI", "tenderName", false)]
    [InlineData("一般標案", "AI 機關", "AI", "agencyName", true)]
    [InlineData("AI 標案", "AI 機關", "AI", "any", true)]
    [InlineData("一般標案", "一般機關", "AI", "any", false)]
    public void IsMatch_VariousScenarios(
        string tenderName, string agencyName, string keyword, string targetField, bool expected)
    {
        var item = CreateItem(tenderName, agencyName);
        _sut.IsMatch(item, keyword, targetField).Should().Be(expected);
    }
}
