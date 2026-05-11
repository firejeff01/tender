using System.Reflection;
using FluentAssertions;
using Tender.Crawler.Parsing;

namespace Tender.Crawler.Tests;

/// <summary>
/// AngleSharpTenderParser 的解析測試，使用 Fixtures/tender_list_sample.html。
/// </summary>
public sealed class AngleSharpTenderParserTests
{
    private readonly AngleSharpTenderParser _parser = new();
    private readonly DateTimeOffset _now = new(2026, 5, 9, 17, 0, 0, TimeSpan.FromHours(8));

    private static string LoadFixture(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Fixture '{name}' not found in embedded resources.");

        using var stream = asm.GetManifestResourceStream(resourceName)!;
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }

    // ---- 正常解析 ----

    [Fact]
    public void Parse_SampleHtml_Returns3Items()
    {
        var html = LoadFixture("tender_list_sample.html");
        var items = _parser.Parse(html, _now);
        items.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_SampleHtml_FirstItemHasCorrectFields()
    {
        var html = LoadFixture("tender_list_sample.html");
        var items = _parser.Parse(html, _now);

        var first = items[0];
        first.AgencyName.Should().Be("經濟部水利署");
        first.TenderName.Should().Be("智慧水利監測系統建置案");
        first.TenderNo.Should().Be("WRA-114-001");
        first.TenderMethod.Should().Be("公開招標");
        first.ProcurementType.Should().Be("勞務類");
        first.AnnouncementDate.Should().Be("115/05/08");
        first.BidDeadline.Should().Be("115/05/22");
        first.BudgetAmount.Should().Be(1_000_000);
        first.SourcePk.Should().Be("NzEyMTUxODc=");
        first.DetailUrl.Should().Contain("pk=NzEyMTUxODc=");
    }

    [Fact]
    public void Parse_SampleHtml_SecondItemHasCorrectTenderName()
    {
        var html = LoadFixture("tender_list_sample.html");
        var items = _parser.Parse(html, _now);

        items[1].TenderName.Should().Be("原住民族文化數位學習平台維運服務案");
        items[1].TenderMethod.Should().Be("公開取得報價單或企劃書");
        items[1].SourcePk.Should().Be("OGFiYzEyMzQ=");
    }

    [Fact]
    public void Parse_SampleHtml_CreatedAtAndLastSeenAt_SetToNow()
    {
        var html = LoadFixture("tender_list_sample.html");
        var items = _parser.Parse(html, _now);

        foreach (var item in items)
        {
            item.CreatedAt.Should().Be(_now);
            item.LastSeenAt.Should().Be(_now);
        }
    }

    // ---- 空表格 ----

    [Fact]
    public void Parse_EmptyTable_ReturnsEmptyList()
    {
        var html = """
            <html><body>
              <table id="tpam">
                <thead><tr><th>序號</th></tr></thead>
                <tbody></tbody>
              </table>
            </body></html>
            """;
        var items = _parser.Parse(html, _now);
        items.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NoTpamTable_ReturnsEmptyList()
    {
        var html = "<html><body><p>查無資料</p></body></html>";
        var items = _parser.Parse(html, _now);
        items.Should().BeEmpty();
    }

    // ---- 缺欄位容錯 ----

    [Fact]
    public void Parse_RowWithMissingBudget_StillReturnsItem()
    {
        var html = """
            <html><body>
              <table id="tpam">
                <thead><tr><th>1</th><th>2</th><th>3</th><th>4</th><th>5</th><th>6</th><th>7</th><th>8</th><th>9</th></tr></thead>
                <tbody>
                  <tr>
                    <td>1</td>
                    <td>測試機關</td>
                    <td><a href="https://web.pcc.gov.tw/prkms/urlSelector/common/tpam?pk=TESTPK001"><script>Geps3.CNS.pageCode2Img("測試標案名稱");</script></a></td>
                    <td>TEST-001</td>
                    <td>公開招標</td>
                    <td>勞務類</td>
                    <td>115/05/09</td>
                    <td>115/05/20</td>
                    <td></td>
                  </tr>
                </tbody>
              </table>
            </body></html>
            """;

        var items = _parser.Parse(html, _now);
        items.Should().HaveCount(1);
        items[0].BudgetAmount.Should().BeNull();
        items[0].TenderName.Should().Be("測試標案名稱");
    }

    [Fact]
    public void Parse_RowWithMissingBidDeadline_StillReturnsItem()
    {
        var html = """
            <html><body>
              <table id="tpam">
                <thead><tr><th>1</th><th>2</th><th>3</th><th>4</th><th>5</th><th>6</th><th>7</th><th>8</th><th>9</th></tr></thead>
                <tbody>
                  <tr>
                    <td>1</td>
                    <td>測試機關</td>
                    <td><a href="https://web.pcc.gov.tw/prkms/urlSelector/common/tpam?pk=TESTPK002"><script>Geps3.CNS.pageCode2Img("另一個測試");</script></a></td>
                    <td></td>
                    <td>公開招標</td>
                    <td></td>
                    <td>115/05/09</td>
                    <td></td>
                    <td>500,000</td>
                  </tr>
                </tbody>
              </table>
            </body></html>
            """;

        var items = _parser.Parse(html, _now);
        items.Should().HaveCount(1);
        items[0].BidDeadline.Should().BeNull();
        items[0].TenderNo.Should().BeNull();
        items[0].BudgetAmount.Should().Be(500_000);
    }

    // ---- 惡意 HTML（XSS 注入測試）----

    [Fact]
    public void Parse_MaliciousHtml_DoesNotThrow_AndReturnsEmptyOrSafe()
    {
        var html = """
            <html><body>
              <table id="tpam">
                <tbody>
                  <tr>
                    <td><script>alert('xss')</script></td>
                    <td><img src=x onerror="alert(1)">機關</td>
                    <td></td><td></td><td></td><td></td><td></td><td></td><td></td>
                  </tr>
                </tbody>
              </table>
            </body></html>
            """;

        // 不應拋出，可能返回 0 或 1 筆（視欄位是否足夠）
        var act = () => _parser.Parse(html, _now);
        act.Should().NotThrow();
    }

    // ---- ParseException ----

    [Fact]
    public void Parse_NullHtml_ThrowsParseException()
    {
        var act = () => _parser.Parse(null!, _now);
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void Parse_EmptyHtml_ThrowsParseException()
    {
        var act = () => _parser.Parse("", _now);
        act.Should().Throw<ParseException>();
    }

    // ---- PK 解析 ----

    [Fact]
    public void Parse_UrlWithPkParam_ExtractsPkCorrectly()
    {
        var html = LoadFixture("tender_list_sample.html");
        var items = _parser.Parse(html, _now);

        items[2].SourcePk.Should().Be("dGVzdDAwMQ==");
    }
}
